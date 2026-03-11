using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Services;

public class LabSetupService : ILabSetupService
{
    private readonly StasisDbContext _context;

    public LabSetupService(StasisDbContext context)
    {
        _context = context;
    }

    // Freezers

    public async Task<List<Freezer>> GetAllFreezersAsync()
    {
        return await _context.Freezers
            .OrderBy(f => f.FreezerName)
            .Include(f => f.Racks)
            .ToListAsync();
    }

    public async Task<Freezer?> GetFreezerByIdAsync(int id)
    {
        return await _context.Freezers
            .Include(f => f.Racks)
            .FirstOrDefaultAsync(f => f.FreezerID == id);
    }

    public async Task AddFreezerAsync(Freezer freezer)
    {
        _context.Freezers.Add(freezer);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateFreezerAsync(Freezer freezer)
    {
        _context.Freezers.Update(freezer);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteFreezerAsync(int id)
    {
        var freezer = await _context.Freezers
            .Include(f => f.Racks)
            .FirstOrDefaultAsync(f => f.FreezerID == id);
        if (freezer == null) return false;
        if (freezer.Racks.Count > 0) return false; // has racks, cannot delete
        _context.Freezers.Remove(freezer);
        await _context.SaveChangesAsync();
        return true;
    }

    // Racks

    public async Task<List<Rack>> GetAllRacksAsync()
    {
        return await _context.Racks
            .Include(r => r.Freezer)
            .OrderBy(r => r.Freezer!.FreezerName)
            .ThenBy(r => r.RackName)
            .ToListAsync();
    }

    public async Task<Rack?> GetRackByIdAsync(int id)
    {
        return await _context.Racks
            .Include(r => r.Freezer)
            .FirstOrDefaultAsync(r => r.RackID == id);
    }

    public async Task AddRackAsync(Rack rack)
    {
        _context.Racks.Add(rack);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRackAsync(Rack rack)
    {
        _context.Racks.Update(rack);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteRackAsync(int id)
    {
        var rack = await _context.Racks
            .Include(r => r.Boxes)
            .FirstOrDefaultAsync(r => r.RackID == id);
        if (rack == null) return false;
        if (rack.Boxes.Count > 0) return false; // has boxes, cannot delete
        _context.Racks.Remove(rack);
        await _context.SaveChangesAsync();
        return true;
    }
}
