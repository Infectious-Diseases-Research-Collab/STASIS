using Microsoft.EntityFrameworkCore;
using STASIS.Data;
using STASIS.Models;

namespace STASIS.Services;

public class SampleService : ISampleService
{
    private readonly StasisDbContext _context;

    public SampleService(StasisDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Specimen> Specimens, int TotalCount)> GetSpecimensAsync(string? searchString, int? studyId, int? sampleTypeId, int pageIndex, int pageSize)
    {
        var query = _context.Specimens
            .Include(s => s.Study)
            .Include(s => s.SampleType)
            .Include(s => s.Box)
                .ThenInclude(b => b!.Rack)
                .ThenInclude(r => r!.Freezer)
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

        var totalCount = await query.CountAsync();

        var specimens = await query.Skip((pageIndex - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();

        return (specimens, totalCount);
    }

    public async Task<List<Study>> GetAllStudies()
    {
        return await _context.Studies.ToListAsync();
    }

    public async Task<List<SampleType>> GetAllSampleTypes()
    {
        return await _context.SampleTypes.ToListAsync();
    }

    public async Task<Specimen?> GetSpecimenByBarcode(string barcode)
    {
        return await _context.Specimens
            .Include(s => s.Study)
            .Include(s => s.SampleType)
            .Include(s => s.Box)
            .ThenInclude(b => b!.Rack)
            .ThenInclude(r => r!.Freezer)
            .FirstOrDefaultAsync(s => s.BarcodeID == barcode);
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
}
