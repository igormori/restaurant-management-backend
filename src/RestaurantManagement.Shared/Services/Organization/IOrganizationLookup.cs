namespace RestaurantManagement.Shared.Services.Organization
{
    public interface IOrganizationLookup
    {
        Task<bool> OrganizationExistsAsync(Guid organizationId);
        Task<List<Guid>> GetLocationIdsInOrganizationAsync(Guid organizationId, IReadOnlyCollection<Guid> locationIds);
        Task<LocationSummaryDto?> GetLocationAsync(Guid locationId);

        /// <summary>
        /// The IDs of every location of the organization whose status is not Closed (Active,
        /// TemporarilyClosed and UnderConstruction count). Empty for an unknown organization.
        /// </summary>
        Task<List<Guid>> GetNonClosedLocationIdsAsync(Guid organizationId);
        Task<List<OrganizationSummaryDto>> GetOrganizationsAsync(IReadOnlyCollection<Guid> organizationIds);
    }
}
