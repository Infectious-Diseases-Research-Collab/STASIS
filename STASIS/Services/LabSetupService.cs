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
            .Include(f => f.Compartments)
            .ToListAsync();
    }

    public async Task<Freezer?> GetFreezerByIdAsync(int id)
    {
        return await _context.Freezers
            .Include(f => f.Compartments)
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
            .Include(f => f.Compartments)
            .FirstOrDefaultAsync(f => f.FreezerID == id);
        if (freezer == null) return false;
        if (freezer.Compartments.Count > 0) return false; // has compartments, cannot delete
        _context.Freezers.Remove(freezer);
        await _context.SaveChangesAsync();
        return true;
    }

    // Compartments

    public async Task<List<Compartment>> GetAllCompartmentsAsync()
    {
        return await _context.Compartments
            .Include(c => c.Freezer)
            .Include(c => c.Racks)
            .OrderBy(c => c.Freezer!.FreezerName)
            .ThenBy(c => c.CompartmentName)
            .ToListAsync();
    }

    public async Task<List<Compartment>> GetCompartmentsByFreezerAsync(int freezerId)
    {
        return await _context.Compartments
            .Where(c => c.FreezerID == freezerId)
            .OrderBy(c => c.CompartmentName)
            .ToListAsync();
    }

    public async Task<Compartment?> GetCompartmentByIdAsync(int id)
    {
        return await _context.Compartments
            .Include(c => c.Freezer)
            .Include(c => c.Racks)
            .FirstOrDefaultAsync(c => c.CompartmentID == id);
    }

    public async Task AddCompartmentAsync(Compartment compartment)
    {
        _context.Compartments.Add(compartment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCompartmentAsync(Compartment compartment)
    {
        _context.Compartments.Update(compartment);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteCompartmentAsync(int id)
    {
        var compartment = await _context.Compartments
            .Include(c => c.Racks)
            .FirstOrDefaultAsync(c => c.CompartmentID == id);
        if (compartment == null) return false;
        if (compartment.Racks.Count > 0) return false; // has racks, cannot delete
        _context.Compartments.Remove(compartment);
        await _context.SaveChangesAsync();
        return true;
    }

    // Racks

    public async Task<List<Rack>> GetAllRacksAsync()
    {
        return await _context.Racks
            .Include(r => r.Compartment)
            .ThenInclude(c => c!.Freezer)
            .OrderBy(r => r.Compartment!.Freezer!.FreezerName)
            .ThenBy(r => r.Compartment!.CompartmentName)
            .ThenBy(r => r.RackName)
            .ToListAsync();
    }

    public async Task<Rack?> GetRackByIdAsync(int id)
    {
        return await _context.Racks
            .Include(r => r.Compartment)
            .ThenInclude(c => c!.Freezer)
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
