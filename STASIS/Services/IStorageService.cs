using STASIS.Models;

namespace STASIS.Services;

public interface IStorageService
{
    Task<List<Freezer>> GetAllFreezers();
    Task<List<Rack>> GetRacksByFreezer(int freezerId);
    Task<List<Box>> GetBoxesByRack(int rackId);
    Task<Box?> GetBoxWithSpecimens(int boxId);
}
