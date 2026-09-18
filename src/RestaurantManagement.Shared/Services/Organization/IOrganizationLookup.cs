namespace RestaurantManagement.Shared.Services.Organization
{
    public interface IOrganizationLookup
    {
        Task<bool> OrganizationExistsAsync(Guid organizationId);
        Task<List<Guid>> GetLocationIdsInOrganizationAsync(Guid organizationId, IReadOnlyCollection<Guid> locationIds);
        Task<LocationSummaryDto?> GetLocationAsync(Guid locationId);
    }
}
