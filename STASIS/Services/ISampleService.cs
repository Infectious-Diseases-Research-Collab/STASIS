using STASIS.Models;

namespace STASIS.Services;

public interface ISampleService
{
    Task<(List<Specimen> Specimens, int TotalCount)> GetSpecimensAsync(string? searchString, int? studyId, int? sampleTypeId, int pageIndex, int pageSize);
    Task<List<Study>> GetAllStudies();
    Task<List<SampleType>> GetAllSampleTypes();
    Task<Specimen?> GetSpecimenByBarcode(string barcode);
    Task AddSpecimen(Specimen specimen);
    Task UpdateSpecimen(Specimen specimen);
    Task DeleteSpecimen(int id);
}
