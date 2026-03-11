using STASIS.Models;

namespace STASIS.Services;

public interface ILabSetupService
{
    // Freezers
    Task<List<Freezer>> GetAllFreezersAsync();
    Task<Freezer?> GetFreezerByIdAsync(int id);
    Task AddFreezerAsync(Freezer freezer);
    Task UpdateFreezerAsync(Freezer freezer);
    Task<bool> DeleteFreezerAsync(int id);

    // Racks
    Task<List<Rack>> GetAllRacksAsync();
    Task<Rack?> GetRackByIdAsync(int id);
    Task AddRackAsync(Rack rack);
    Task UpdateRackAsync(Rack rack);
    Task<bool> DeleteRackAsync(int id);
}
