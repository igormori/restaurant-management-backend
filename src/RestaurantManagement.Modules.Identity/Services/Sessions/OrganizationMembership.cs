using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Membership and active-organization checks, read from the user's organization memberships.
    internal class OrganizationMembership
    {
        private readonly IdentityDbContext _db;
        private readonly IOrganizationLookup _organizationLookup;

        public OrganizationMembership(IdentityDbContext db, IOrganizationLookup organizationLookup)
        {
            _db = db;
            _organizationLookup = organizationLookup;
        }

        public async Task<List<Guid>> GetOrganizationIdsAsync(Guid userId)
        {
            return await _db.OrganizationMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.OrganizationId)
                .ToListAsync();
        }

        public async Task<List<OrganizationChoice>> GetActiveChoicesAsync(IReadOnlyCollection<Guid> organizationIds)
        {
            if (organizationIds.Count == 0)
                return new List<OrganizationChoice>();

            var organizations = await _organizationLookup.GetOrganizationsAsync(organizationIds);

            return (organizations ?? new List<OrganizationSummaryDto>())
                .Where(o => o.IsActive)
                .Select(o => new OrganizationChoice { Id = o.Id, Name = o.Name })
                .ToList();
        }

        public async Task<bool> CanSelectAsync(User user, Guid organizationId)
        {
            if (!await IsMemberOfAsync(user.Id, organizationId))
                return false;

            var organizations = await _organizationLookup.GetOrganizationsAsync(new[] { organizationId });
            var organization = organizations?.FirstOrDefault(o => o.Id == organizationId);

            return organization is { IsActive: true };
        }

        public async Task<bool> IsMemberOfAsync(Guid userId, Guid organizationId)
        {
            return await _db.OrganizationMembers.AnyAsync(m => m.UserId == userId && m.OrganizationId == organizationId);
        }
    }
}
