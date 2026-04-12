using System.Globalization;
using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Services;

public class SampleService : ISampleService
{
    private readonly StasisDbContext _context;
    private readonly IAuditService _auditService;

    public SampleService(StasisDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<(List<Specimen> Specimens, int TotalCount)> GetSpecimensAsync(string? searchString, int? studyId, int? sampleTypeId, string? participantId, int pageIndex, int pageSize)
    {
        var query = _context.Specimens
            .Include(s => s.Study)
            .Include(s => s.SampleType)
            .Include(s => s.Box)
                .ThenInclude(b => b!.Rack)
                .ThenInclude(r => r!.Compartment)
                .ThenInclude(c => c!.Freezer)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchString))
        {
            query = query.Where(s => s.BarcodeID.Contains(searchString));
        }

        if (studyId.HasValue)
        {
            query = query.Where(s => s.StudyID == studyId.Value);
        }

        if (sampleTypeId.HasValue)
        {
            query = query.Where(s => s.SampleTypeID == sampleTypeId.Value);
        }

        if (!string.IsNullOrEmpty(participantId))
        {
            query = query.Where(s => s.ParticipantID != null && s.ParticipantID.Contains(participantId));
        }

        var totalCount = await query.CountAsync();

        var specimens = await query.Skip((pageIndex - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

        return (specimens, totalCount);
    }

    public async Task<List<Study>> GetAllStudies()
    {
        return await _context.Studies.OrderBy(s => s.StudyCode).ToListAsync();
    }

    public async Task<List<SampleType>> GetAllSampleTypes()
    {
        return await _context.SampleTypes.OrderBy(st => st.TypeName).ToListAsync();
    }

    public async Task<List<VisitType>> GetAllVisitTypes()
    {
        return await _context.VisitTypes.OrderBy(v => v.VisitTypeName).ToListAsync();
    }

    public async Task<(int Row, int? Col)?> GetNextAvailablePosition(int boxId, IEnumerable<(int Row, int? Col)>? claimedPositions = null)
    {
        var box = await _context.Boxes.FindAsync(boxId);
        if (box == null) return null;

        var occupied = await _context.Specimens
            .Where(s => s.BoxID == boxId && s.PositionRow != null)
            .Select(s => new { s.PositionRow, s.PositionCol })
            .ToListAsync();

        // Normalise col to 1 for hashing — Filter Paper Binder boxes use null col (linear),
        // so null and col=1 cannot coexist in the same box. Safe to treat them as equivalent.
        var occupiedSet = occupied
            .Select(p => (p.PositionRow!.Value, p.PositionCol ?? 1))
            .ToHashSet();

        // Also treat in-batch claimed positions as occupied
        if (claimedPositions != null)
        {
            foreach (var c in claimedPositions)
                occupiedSet.Add((c.Row, c.Col ?? 1));
        }

        // Determine grid dimensions from BoxType
        int rows, cols;
        bool isLinear = false;

        switch (box.BoxType)
        {
            case "81-slot":
                rows = 9; cols = 9;
                break;
            case "100-slot":
                rows = 10; cols = 10;
                break;
            case "Filter Paper Binder":
                rows = 100; cols = 1; isLinear = true;
                break;
            default:
                // Unknown box type — returns null (same as "box full"). Caller cannot distinguish.
                // The three types above are the only supported types per the DB check constraint.
                return null;
        }

        // Scan sequentially for first free slot
        for (int r = 1; r <= rows; r++)
        {
            for (int c = 1; c <= cols; c++)
            {
                if (!occupiedSet.Contains((r, c)))
                    return isLinear ? (r, (int?)null) : (r, c);
            }
        }

        return null; // box is full
    }

    public async Task AddSpecimensBatch(IEnumerable<Specimen> specimens, string userId)
    {
        var specimenList = specimens.ToList();
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var specimen in specimenList)
            {
                _context.Specimens.Add(specimen);
            }
            await _context.SaveChangesAsync();

            // Audit writes share the same scoped DbContext as this service, so they are
            // inside the same transaction. If the transaction is rolled back, audit rows
            // are rolled back too — no orphaned entries.
            foreach (var specimen in specimenList)
            {
                await _auditService.LogChangeAsync(
                    "tbl_Specimens", specimen.SpecimenID.ToString(),
                    "Created", null, specimen.BarcodeID, userId);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddVisitType(string name, string userId)
    {
        var visitType = new VisitType { VisitTypeName = name };
        _context.VisitTypes.Add(visitType);
        await _context.SaveChangesAsync();
        await _auditService.LogChangeAsync("tbl_VisitTypes", visitType.VisitTypeID.ToString(),
            "Created", null, visitType.VisitTypeName, userId);
    }

    public async Task UpdateVisitType(int id, string name, string userId)
    {
        var existing = await _context.VisitTypes.FindAsync(id)
            ?? throw new KeyNotFoundException($"VisitType {id} not found.");
        var oldName = existing.VisitTypeName;
        existing.VisitTypeName = name;
        await _context.SaveChangesAsync();
        if (oldName != name)
            await _auditService.LogChangeAsync("tbl_VisitTypes", id.ToString(),
                "VisitTypeName", oldName, name, userId);
    }

    public async Task DeleteVisitType(int id, string userId)
    {
        var visitType = await _context.VisitTypes
            .Include(v => v.Specimens)
            .FirstOrDefaultAsync(v => v.VisitTypeID == id)
            ?? throw new KeyNotFoundException($"VisitType {id} not found.");
        if (visitType.Specimens.Count > 0)
            throw new InvalidOperationException("Cannot delete visit type — it has specimens associated with it.");
        var name = visitType.VisitTypeName;
        _context.VisitTypes.Remove(visitType);
        await _context.SaveChangesAsync();
        await _auditService.LogChangeAsync("tbl_VisitTypes", id.ToString(),
            "Deleted", name, null, userId);
    }

    public async Task<Specimen?> GetSpecimenByBarcode(string barcode)
    {
        return await _context.Specimens
            .Include(s => s.Study)
            .Include(s => s.SampleType)
            .Include(s => s.Box)
            .ThenInclude(b => b!.Rack)
            .ThenInclude(r => r!.Compartment)
            .ThenInclude(c => c!.Freezer)
            .FirstOrDefaultAsync(s => s.BarcodeID == barcode);
    }

    public async Task<bool> IsBarcodeTaken(string barcode)
    {
        return await _context.Specimens.AnyAsync(s => s.BarcodeID == barcode);
    }

    public async Task<List<(int Row, int Col)>> GetOccupiedPositions(int boxId)
    {
        return await _context.Specimens
            .Where(s => s.BoxID == boxId && s.PositionRow != null && s.PositionCol != null)
            .Select(s => new ValueTuple<int, int>(s.PositionRow!.Value, s.PositionCol!.Value))
            .ToListAsync();
    }

    public async Task AddSpecimen(Specimen specimen)
    {
        _context.Specimens.Add(specimen);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSpecimen(Specimen specimen)
    {
        _context.Entry(specimen).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSpecimen(int id)
    {
        var specimen = await _context.Specimens.FindAsync(id);
        if (specimen != null)
        {
            _context.Specimens.Remove(specimen);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ImportResult> ImportSpecimensFromCsv(Stream csvStream)
    {
        var result = new ImportResult();
        var lines = new List<string>();

        using (var reader = new StreamReader(csvStream))
        {
            while (await reader.ReadLineAsync() is { } line)
                lines.Add(line);
        }

        if (lines.Count == 0)
            return result;

        // Skip header row
        var headerLine = lines[0].ToLowerInvariant();
        var startIndex = headerLine.Contains("barcode") ? 1 : 0;

        result.TotalRows = lines.Count - startIndex;

        // Pre-load lookup data
        var studies = await _context.Studies.ToListAsync();
        var sampleTypes = await _context.SampleTypes.ToListAsync();
        var boxes = await _context.Boxes.ToListAsync();
        var existingBarcodes = await _context.Specimens
            .Select(s => s.BarcodeID)
            .ToListAsync();
        var existingBarcodeSet = new HashSet<string>(existingBarcodes, StringComparer.OrdinalIgnoreCase);

        // Track barcodes within this import to detect intra-file duplicates
        var importBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Track positions claimed within this import
        var claimedPositions = new HashSet<string>();

        for (int i = startIndex; i < lines.Count; i++)
        {
            var row = ParseCsvLine(lines[i]);
            var importRow = new ImportRow { LineNumber = i + 1 };

            if (row.Length < 1 || string.IsNullOrWhiteSpace(row[0]))
            {
                importRow.Error = "BarcodeID is required.";
                result.ErrorRows.Add(importRow);
                continue;
            }

            importRow.BarcodeID = row[0].Trim();
            importRow.LegacyID = row.Length > 1 ? row[1].Trim() : null;
            importRow.StudyCode = row.Length > 2 ? row[2].Trim() : null;
            importRow.SampleType = row.Length > 3 ? row[3].Trim() : null;
            importRow.CollectionDate = row.Length > 4 ? row[4].Trim() : null;
            importRow.BoxLabel = row.Length > 5 ? row[5].Trim() : null;
            importRow.PositionRow = row.Length > 6 ? row[6].Trim() : null;
            importRow.PositionCol = row.Length > 7 ? row[7].Trim() : null;
            importRow.ParticipantID = row.Length > 8 ? row[8].Trim() : null;
            importRow.CellCount = row.Length > 9 ? row[9].Trim() : null;

            // Validate barcode uniqueness
            if (existingBarcodeSet.Contains(importRow.BarcodeID))
            {
                importRow.Error = $"Barcode '{importRow.BarcodeID}' already exists in the database.";
                result.ErrorRows.Add(importRow);
                continue;
            }

            if (!importBarcodes.Add(importRow.BarcodeID))
            {
                importRow.Error = $"Duplicate barcode '{importRow.BarcodeID}' within this import file.";
                result.ErrorRows.Add(importRow);
                continue;
            }

            // Resolve study
            int? studyId = null;
            if (!string.IsNullOrEmpty(importRow.StudyCode))
            {
                var study = studies.FirstOrDefault(s =>
                    s.StudyCode.Equals(importRow.StudyCode, StringComparison.OrdinalIgnoreCase));
                if (study == null)
                {
                    importRow.Error = $"Study code '{importRow.StudyCode}' not found.";
                    result.ErrorRows.Add(importRow);
                    continue;
                }
                studyId = study.StudyID;
            }

            // Resolve sample type
            int? sampleTypeId = null;
            SampleType? resolvedSampleType = null;
            if (!string.IsNullOrEmpty(importRow.SampleType))
            {
                resolvedSampleType = sampleTypes.FirstOrDefault(st =>
                    st.TypeName.Equals(importRow.SampleType, StringComparison.OrdinalIgnoreCase));
                if (resolvedSampleType == null)
                {
                    importRow.Error = $"Sample type '{importRow.SampleType}' not found.";
                    result.ErrorRows.Add(importRow);
                    continue;
                }
                sampleTypeId = resolvedSampleType.SampleTypeID;
            }

            // Parse collection date
            DateTime? collectionDate = null;
            if (!string.IsNullOrEmpty(importRow.CollectionDate))
            {
                if (!DateTime.TryParse(importRow.CollectionDate, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsed))
                {
                    importRow.Error = $"Invalid date format: '{importRow.CollectionDate}'.";
                    result.ErrorRows.Add(importRow);
                    continue;
                }
                collectionDate = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            // Resolve box
            int? boxId = null;
            if (!string.IsNullOrEmpty(importRow.BoxLabel))
            {
                var box = boxes.FirstOrDefault(b =>
                    b.BoxLabel.Equals(importRow.BoxLabel, StringComparison.OrdinalIgnoreCase));
                if (box == null)
                {
                    importRow.Error = $"Box label '{importRow.BoxLabel}' not found.";
                    result.ErrorRows.Add(importRow);
                    continue;
                }
                boxId = box.BoxID;
            }

            // Parse position
            int? posRow = null, posCol = null;
            if (!string.IsNullOrEmpty(importRow.PositionRow) && !string.IsNullOrEmpty(importRow.PositionCol))
            {
                if (!int.TryParse(importRow.PositionRow, out var pr) ||
                    !int.TryParse(importRow.PositionCol, out var pc))
                {
                    importRow.Error = "PositionRow and PositionCol must be integers.";
                    result.ErrorRows.Add(importRow);
                    continue;
                }
                posRow = pr;
                posCol = pc;

                // Check position not already claimed in this import
                if (boxId.HasValue)
                {
                    var posKey = $"{boxId.Value}-{pr}-{pc}";
                    if (!claimedPositions.Add(posKey))
                    {
                        importRow.Error = $"Position ({pr},{pc}) in box '{importRow.BoxLabel}' is used by another row in this file.";
                        result.ErrorRows.Add(importRow);
                        continue;
                    }

                    // Check against DB
                    var occupied = await _context.Specimens.AnyAsync(s =>
                        s.BoxID == boxId.Value && s.PositionRow == pr && s.PositionCol == pc);
                    if (occupied)
                    {
                        importRow.Error = $"Position ({pr},{pc}) in box '{importRow.BoxLabel}' is already occupied.";
                        result.ErrorRows.Add(importRow);
                        continue;
                    }
                }
            }

            // Set filter paper defaults
            int? remainingSpots = null;
            if (resolvedSampleType != null &&
                resolvedSampleType.TypeName.Equals("Filter Paper", StringComparison.OrdinalIgnoreCase))
            {
                remainingSpots = 4;
            }

            var specimen = new Specimen
            {
                BarcodeID = importRow.BarcodeID,
                LegacyID = string.IsNullOrEmpty(importRow.LegacyID) ? null : importRow.LegacyID,
                StudyID = studyId,
                SampleTypeID = sampleTypeId,
                CollectionDate = collectionDate,
                BoxID = boxId,
                PositionRow = posRow,
                PositionCol = posCol,
                RemainingSpots = remainingSpots,
                ParticipantID = string.IsNullOrEmpty(importRow.ParticipantID) ? null : importRow.ParticipantID,
                CellCount = int.TryParse(importRow.CellCount, out var importedCellCount) ? importedCellCount : (int?)null,
                Status = "In-Stock"
            };

            importRow.Specimen = specimen;
            result.SuccessRows.Add(importRow);
        }

        return result;
    }

    public async Task<Specimen?> GetSpecimenByIdAsync(int specimenId)
    {
        return await _context.Specimens
            .Include(s => s.Study)
            .Include(s => s.SampleType)
            .Include(s => s.Box)
                .ThenInclude(b => b!.Rack)
                    .ThenInclude(r => r!.Compartment)
                    .ThenInclude(c => c!.Freezer)
            .Include(s => s.DiscardApproval)
            .FirstOrDefaultAsync(s => s.SpecimenID == specimenId);
    }

    public async Task<Approval> RequestDiscardAsync(List<int> specimenIds, string userId)
    {
        var approval = new Approval
        {
            ApprovalType = "Discard",
            RequestedByUserId = userId,
            RequestedDate = DateTime.UtcNow,
            OverallStatus = "Pending"
        };
        _context.Approvals.Add(approval);
        await _context.SaveChangesAsync();

        // Link specimens to this discard approval
        var specimens = await _context.Specimens
            .Where(s => specimenIds.Contains(s.SpecimenID))
            .ToListAsync();

        foreach (var specimen in specimens)
        {
            specimen.DiscardApprovalID = approval.ApprovalID;
        }
        await _context.SaveChangesAsync();

        await _auditService.LogChangeAsync("tbl_Approvals", approval.ApprovalID.ToString(),
            "Created", null,
            $"Discard request for {specimens.Count} specimen(s)", userId);

        return approval;
    }

    public async Task<List<Approval>> GetPendingDiscardApprovalsAsync()
    {
        return await _context.Approvals
            .Include(a => a.RequestedByUser)
            .Include(a => a.DiscardedSpecimens)
                .ThenInclude(s => s.SampleType)
            .Where(a => a.ApprovalType == "Discard" && a.OverallStatus == "Pending")
            .OrderByDescending(a => a.RequestedDate)
            .ToListAsync();
    }

    public async Task<Approval?> GetDiscardApprovalByIdAsync(int approvalId)
    {
        return await _context.Approvals
            .Include(a => a.RequestedByUser)
            .Include(a => a.EDApproverUser)
            .Include(a => a.RegulatoryApproverUser)
            .Include(a => a.PIApproverUser)
            .Include(a => a.DiscardedSpecimens)
                .ThenInclude(s => s.SampleType)
            .Include(a => a.DiscardedSpecimens)
                .ThenInclude(s => s.Study)
            .Include(a => a.DiscardedSpecimens)
                .ThenInclude(s => s.Box)
            .FirstOrDefaultAsync(a => a.ApprovalID == approvalId);
    }

    public async Task ApproveDiscardAsync(int approvalId, string approverUserId, string level, string status, string? comments)
    {
        var approval = await _context.Approvals.FindAsync(approvalId);
        if (approval == null || approval.ApprovalType != "Discard") return;

        var now = DateTime.UtcNow;

        switch (level.ToLowerInvariant())
        {
            case "ed":
                approval.EDApproverUserId = approverUserId;
                approval.EDApprovalStatus = status;
                approval.EDApprovalDate = now;
                approval.EDComments = comments;
                break;
            case "regulatory":
                approval.RegulatoryApproverUserId = approverUserId;
                approval.RegulatoryApprovalStatus = status;
                approval.RegulatoryApprovalDate = now;
                approval.RegulatoryComments = comments;
                break;
            case "pi":
                approval.PIApproverUserId = approverUserId;
                approval.PIApprovalStatus = status;
                approval.PIApprovalDate = now;
                approval.PIComments = comments;
                break;
        }

        // Discard requires ALL THREE approvals (ED + Regulatory + PI)
        if (approval.EDApprovalStatus == "Rejected" ||
            approval.RegulatoryApprovalStatus == "Rejected" ||
            approval.PIApprovalStatus == "Rejected")
        {
            approval.OverallStatus = "Rejected";
        }
        else if (approval.EDApprovalStatus == "Approved" &&
                 approval.RegulatoryApprovalStatus == "Approved" &&
                 approval.PIApprovalStatus == "Approved")
        {
            approval.OverallStatus = "Approved";
        }

        await _context.SaveChangesAsync();

        await _auditService.LogChangeAsync("tbl_Approvals", approval.ApprovalID.ToString(),
            $"{level}Approval", null, status, approverUserId);
    }

    public async Task ExecuteDiscardAsync(int approvalId, string userId)
    {
        var approval = await _context.Approvals
            .Include(a => a.DiscardedSpecimens)
            .FirstOrDefaultAsync(a => a.ApprovalID == approvalId);

        if (approval == null || approval.OverallStatus != "Approved") return;

        foreach (var specimen in approval.DiscardedSpecimens)
        {
            var oldStatus = specimen.Status;
            specimen.Status = "Discarded";
            specimen.DiscardApprovalID = approvalId;

            await _auditService.LogChangeAsync("tbl_Specimens", specimen.SpecimenID.ToString(),
                "Status", oldStatus, "Discarded", userId);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Specimen?> GetSpecimenDetailAsync(int specimenId)
    {
        return await _context.Specimens
            .Include(s => s.Study)
            .Include(s => s.SampleType)
            .Include(s => s.Box)
                .ThenInclude(b => b!.Rack)
                    .ThenInclude(r => r!.Compartment)
                    .ThenInclude(c => c!.Freezer)
            .Include(s => s.DiscardApproval)
            .Include(s => s.FilterPaperUsages)
                .ThenInclude(u => u.UsedByUser)
            .Include(s => s.ShipmentContents)
                .ThenInclude(sc => sc.Shipment)
            .FirstOrDefaultAsync(s => s.SpecimenID == specimenId);
    }

    public async Task<List<FilterPaperUsage>> GetFilterPaperUsageAsync(int specimenId)
    {
        return await _context.FilterPaperUsages
            .Include(u => u.UsedByUser)
            .Include(u => u.ShipmentContent)
                .ThenInclude(sc => sc!.Shipment)
            .Where(u => u.SpecimenID == specimenId)
            .OrderByDescending(u => u.UsageDate)
            .ToListAsync();
    }

    public async Task<List<string>> GetTakenBarcodesAsync(IEnumerable<string> barcodes)
    {
        var list = barcodes.ToList();
        return await _context.Specimens
            .Where(s => list.Contains(s.BarcodeID))
            .Select(s => s.BarcodeID)
            .ToListAsync();
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
