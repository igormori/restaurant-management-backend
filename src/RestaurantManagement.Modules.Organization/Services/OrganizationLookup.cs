using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Organization.Services
{
    public class OrganizationLookup : IOrganizationLookup
    {
        private readonly OrganizationDbContext _orgDb;

        public OrganizationLookup(OrganizationDbContext orgDb)
        {
            _orgDb = orgDb;
        }

        public async Task<bool> OrganizationExistsAsync(Guid organizationId)
        {
            return await _orgDb.Organizations.AnyAsync(o => o.Id == organizationId);
        }

        public async Task<List<Guid>> GetLocationIdsInOrganizationAsync(Guid organizationId, IReadOnlyCollection<Guid> locationIds)
        {
            return await _orgDb.Locations
                .Where(l => locationIds.Contains(l.Id) && l.OrganizationId == organizationId)
                .Select(l => l.Id)
                .ToListAsync();
        }

        public async Task<LocationSummaryDto?> GetLocationAsync(Guid locationId)
        {
            var location = await _orgDb.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
                return null;

            return new LocationSummaryDto
            {
                Id = location.Id,
                OrganizationId = location.OrganizationId
            };
        }
    }
}
