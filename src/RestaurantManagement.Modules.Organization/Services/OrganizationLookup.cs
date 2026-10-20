using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Entities;
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

        public async Task<List<Guid>> GetNonClosedLocationIdsAsync(Guid organizationId)
        {
            return await _orgDb.Locations
                .Where(l => l.OrganizationId == organizationId && l.Status != LocationStatus.Closed)
                .Select(l => l.Id)
                .ToListAsync();
        }

        public async Task<List<OrganizationSummaryDto>> GetOrganizationsAsync(IReadOnlyCollection<Guid> organizationIds)
        {
            return await _orgDb.Organizations
                .Where(o => organizationIds.Contains(o.Id))
                .Select(o => new OrganizationSummaryDto { Id = o.Id, Name = o.Name, IsActive = o.IsActive })
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
