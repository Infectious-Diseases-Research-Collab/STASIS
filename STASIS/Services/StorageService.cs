using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Services;

public class StorageService : IStorageService
{
    private readonly StasisDbContext _context;

    public StorageService(StasisDbContext context)
    {
        _context = context;
    }

    public async Task<List<Freezer>> GetAllFreezers()
    {
        return await _context.Freezers.ToListAsync();
    }

    public async Task<List<Rack>> GetRacksByFreezer(int freezerId)
    {
        return await _context.Racks.Where(r => r.FreezerID == freezerId).ToListAsync();
    }

    public async Task<List<Box>> GetBoxesByRack(int rackId)
    {
        return await _context.Boxes.Where(b => b.RackID == rackId).ToListAsync();
    }

    public async Task<Box?> GetBoxWithSpecimens(int boxId)
    {
        return await _context.Boxes
            .Include(b => b.Rack)
            .ThenInclude(r => r!.Freezer)
            .Include(b => b.Specimens)
            .ThenInclude(s => s.SampleType)
            .FirstOrDefaultAsync(b => b.BoxID == boxId);
    }
}
