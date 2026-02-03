using RestaurantManagement.Shared;
using RestaurantManagement.Modules.Organization.Models;

namespace RestaurantManagement.Modules.Organization.Services
{
    public interface ILocationService
    {
        Task<LocationResponse> CreateLocationAsync(Guid userId, Guid organizationId, CreateLocationRequest request);
        Task<LocationResponse> EditLocationAsync(Guid userId, Guid locationId, EditLocationRequest request);
        Task DeleteLocationAsync(Guid userId, Guid locationId);
        Task<List<LocationResponse>> GetLocationsByOrganizationAsync(Guid userId, Guid organizationId);
    }
}
