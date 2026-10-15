using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Membership and active-organization checks for a user whose Roles are loaded.
    internal class OrganizationMembership
    {
        private readonly IOrganizationLookup _organizationLookup;

        public OrganizationMembership(IOrganizationLookup organizationLookup)
        {
            _organizationLookup = organizationLookup;
        }

        public async Task<List<OrganizationChoice>> GetActiveChoicesAsync(User user)
        {
            var organizationIds = user.Roles
                .Select(r => r.OrganizationId)
                .Distinct()
                .ToList();
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
            if (!IsMemberOf(user, organizationId))
                return false;

            var organizations = await _organizationLookup.GetOrganizationsAsync(new[] { organizationId });
            var organization = organizations?.FirstOrDefault(o => o.Id == organizationId);

            return organization is { IsActive: true };
        }

        public static bool IsMemberOf(User user, Guid organizationId)
        {
            return user.Roles.Any(r => r.OrganizationId == organizationId);
        }
    }
}
