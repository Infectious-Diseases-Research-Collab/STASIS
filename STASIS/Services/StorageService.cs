using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Services;

public class StorageService : IStorageService
{
    private readonly StasisDbContext _context;
    private readonly IAuditService _auditService;

    public StorageService(StasisDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<List<Freezer>> GetAllFreezers()
    {
        return await _context.Freezers.OrderBy(f => f.FreezerName).ToListAsync();
    }

    public async Task<List<Compartment>> GetCompartmentsByFreezer(int freezerId)
    {
        return await _context.Compartments
            .Where(c => c.FreezerID == freezerId)
            .OrderBy(c => c.CompartmentName)
            .ToListAsync();
    }

    public async Task<List<Rack>> GetRacksByCompartment(int compartmentId)
    {
        return await _context.Racks
            .Where(r => r.CompartmentID == compartmentId)
            .OrderBy(r => r.RackName)
            .ToListAsync();
    }

    public async Task<List<Box>> GetBoxesByRack(int rackId)
    {
        return await _context.Boxes.Where(b => b.RackID == rackId).ToListAsync();
    }

    public async Task<Box?> GetBoxWithSpecimens(int boxId, IReadOnlyCollection<int>? visibleStudyIds)
    {
        var query = ApplyStudyVisibility(_context.Boxes.AsQueryable(), visibleStudyIds)
            .Include(b => b.Rack)
            .ThenInclude(r => r!.Compartment)
            .ThenInclude(c => c!.Freezer)
            .Include(b => b.Specimens)
            .ThenInclude(s => s.SampleType);

        return await query.FirstOrDefaultAsync(b => b.BoxID == boxId);
    }

    public async Task<List<Box>> GetAllBoxesAsync(IReadOnlyCollection<int>? visibleStudyIds)
    {
        var query = ApplyStudyVisibility(_context.Boxes.AsQueryable(), visibleStudyIds)
            .Include(b => b.Rack)
                .ThenInclude(r => r!.Compartment)
                    .ThenInclude(c => c!.Freezer);

        return await query.OrderBy(b => b.BoxLabel).ToListAsync();
    }

    public async Task<List<string>> GetAllBoxLabelsAsync(IReadOnlyCollection<int>? visibleStudyIds)
    {
        var query = ApplyStudyVisibility(_context.Boxes.AsQueryable(), visibleStudyIds);

        return await query
            .OrderBy(b => b.BoxLabel)
            .Select(b => b.BoxLabel)
            .ToListAsync();
    }

    public async Task<List<Box>> SearchBoxesAsync(string? label, int? freezerId, int? rackId, IReadOnlyCollection<int>? visibleStudyIds)
    {
        IQueryable<Box> query = ApplyStudyVisibility(_context.Boxes.AsQueryable(), visibleStudyIds)
            .Include(b => b.Rack)
            .ThenInclude(r => r!.Compartment)
            .ThenInclude(c => c!.Freezer)
            .Include(b => b.Specimens);

        if (!string.IsNullOrEmpty(label))
            query = query.Where(b => b.BoxLabel.Contains(label));

        if (freezerId.HasValue)
            query = query.Where(b => b.Rack != null && b.Rack.Compartment != null && b.Rack.Compartment.FreezerID == freezerId.Value);

        if (rackId.HasValue)
            query = query.Where(b => b.RackID == rackId.Value);

        return await query.OrderBy(b => b.BoxLabel).Take(100).ToListAsync();
    }

    public async Task<Box?> GetBoxByLabelAsync(string label, IReadOnlyCollection<int>? visibleStudyIds)
    {
        var query = ApplyStudyVisibility(_context.Boxes.AsQueryable(), visibleStudyIds)
            .Include(b => b.Rack)
            .ThenInclude(r => r!.Compartment)
            .ThenInclude(c => c!.Freezer)
            .Include(b => b.Specimens)
            .ThenInclude(s => s.SampleType);

        return await query.FirstOrDefaultAsync(b => b.BoxLabel == label);
    }

    // visibleStudyIds: null = unrestricted (System Admin), so the query passes through
    // unmodified. A non-null, empty collection means the caller has zero study assignments and
    // must see zero boxes -- including unassigned ones -- so the query is short-circuited to
    // return nothing rather than falling through to the "StudyID == null is visible" branch
    // below (which would otherwise leak every unassigned box to a zero-access caller). A
    // non-null, non-empty collection filters to boxes in one of those studies, plus any box not
    // yet claimed by a study (StudyID == null) -- unassigned boxes must stay discoverable to
    // anyone with at least one study assignment, or they could never be placed into.
    private static IQueryable<Box> ApplyStudyVisibility(IQueryable<Box> query, IReadOnlyCollection<int>? visibleStudyIds)
    {
        if (visibleStudyIds == null)
            return query;

        if (visibleStudyIds.Count == 0)
            return query.Where(_ => false);

        return query.Where(b => b.StudyID == null || visibleStudyIds.Contains(b.StudyID.Value));
    }

    public async Task MoveSpecimenAsync(int specimenId, int newBoxId, int row, int col, string userId)
    {
        var specimen = await _context.Specimens
            .Include(s => s.Box)
            .FirstOrDefaultAsync(s => s.SpecimenID == specimenId);
        if (specimen == null) return;

        var oldBoxId = specimen.BoxID;
        var oldRow = specimen.PositionRow;
        var oldCol = specimen.PositionCol;

        specimen.BoxID = newBoxId;
        specimen.PositionRow = row;
        specimen.PositionCol = col;
        if (specimen.Status == "Temp")
            specimen.Status = "In-Stock";

        await _context.SaveChangesAsync();

        await _auditService.LogChangeAsync("tbl_Specimens", specimen.SpecimenID.ToString(),
            "Moved", $"Box {oldBoxId} ({oldRow},{oldCol})", $"Box {newBoxId} ({row},{col})", userId);

        // Check if old box is now empty
        if (oldBoxId.HasValue)
            await CheckAndUnassignEmptyBoxAsync(oldBoxId.Value);
    }

    public async Task MoveBoxAsync(int boxId, int newRackId, string userId)
    {
        var box = await _context.Boxes.FindAsync(boxId);
        if (box == null) return;

        var oldRackId = box.RackID;
        box.RackID = newRackId;
        await _context.SaveChangesAsync();

        await _auditService.LogChangeAsync("tbl_Boxes", box.BoxID.ToString(),
            "RackID", oldRackId?.ToString() ?? "null", newRackId.ToString(), userId);
    }

    public async Task ReboxSpecimensAsync(List<(int SpecimenId, int Row, int Col)> placements, int newBoxId, string userId)
    {
        var oldBoxIds = new HashSet<int>();

        foreach (var (specimenId, row, col) in placements)
        {
            var specimen = await _context.Specimens.FindAsync(specimenId);
            if (specimen == null) continue;

            if (specimen.BoxID.HasValue)
                oldBoxIds.Add(specimen.BoxID.Value);

            specimen.BoxID = newBoxId;
            specimen.PositionRow = row;
            specimen.PositionCol = col;
            if (specimen.Status == "Temp")
                specimen.Status = "In-Stock";
        }

        await _context.SaveChangesAsync();

        await _auditService.LogChangeAsync("tbl_Boxes", newBoxId.ToString(),
            "Reboxed", null, $"{placements.Count} specimens placed", userId);

        // Check if any old boxes are now empty
        foreach (var oldBoxId in oldBoxIds)
            await CheckAndUnassignEmptyBoxAsync(oldBoxId);
    }

    public async Task MoveToTempAsync(int specimenId, string userId)
    {
        var specimen = await _context.Specimens.FindAsync(specimenId);
        if (specimen == null) return;

        var tempBox = await _context.Boxes
            .FirstOrDefaultAsync(b => b.BoxCategory == "Temp");
        if (tempBox == null) return;

        var oldBoxId = specimen.BoxID;
        specimen.BoxID = tempBox.BoxID;
        specimen.PositionRow = null;
        specimen.PositionCol = null;
        specimen.Status = "Temp";

        await _context.SaveChangesAsync();

        await _auditService.LogChangeAsync("tbl_Specimens", specimen.SpecimenID.ToString(),
            "MovedToTemp", $"Box {oldBoxId}", $"Temp ({tempBox.BoxLabel})", userId);

        if (oldBoxId.HasValue)
            await CheckAndUnassignEmptyBoxAsync(oldBoxId.Value);
    }

    public async Task<Box> CreateBoxAsync(string label, string boxType, int? rackId, string userId)
    {
        var box = new Box
        {
            BoxLabel = label,
            BoxType = boxType,
            RackID = rackId
        };
        _context.Boxes.Add(box);
        await _context.SaveChangesAsync();
        await _auditService.LogChangeAsync("tbl_Boxes", box.BoxID.ToString(), "BoxLabel", null, label, userId);
        return box;
    }

    public async Task CheckAndUnassignEmptyBoxAsync(int boxId)
    {
        var box = await _context.Boxes
            .Include(b => b.Specimens)
            .FirstOrDefaultAsync(b => b.BoxID == boxId);

        if (box != null && box.Specimens.Count == 0 && box.RackID != null
            && box.BoxCategory == "Standard")
        {
            box.RackID = null;
            await _context.SaveChangesAsync();
        }
    }
}
