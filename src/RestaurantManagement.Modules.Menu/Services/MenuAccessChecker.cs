using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Menu.Services
{
    internal class MenuAccessChecker
    {
        private static readonly string[] WriteRoles = { Roles.Owner, Roles.Admin };

        private static readonly string[] ReadRoles =
        {
            Roles.Owner, Roles.Admin, Roles.Manager, Roles.Cashier, Roles.Waiter, Roles.Kitchen
        };

        private readonly IUserRoleLookup _userRoleLookup;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuAccessChecker(IUserRoleLookup userRoleLookup, IStringLocalizer<SharedResource> localizer)
        {
            _userRoleLookup = userRoleLookup;
            _localizer = localizer;
        }

        public async Task CheckWriteAsync(Guid userId, Guid organizationId)
        {
            var assignments = await _userRoleLookup.GetRoleAssignmentsAsync(userId, organizationId);

            if (assignments == null || !AccessDecision.AllowsOrganizationWide(assignments, WriteRoles))
                throw Refused();
        }

        public async Task CheckAdminAtLocationAsync(Guid userId, Guid organizationId, Guid? locationId)
        {
            var assignments = await _userRoleLookup.GetRoleAssignmentsAsync(userId, organizationId);
            if (assignments == null)
                throw Refused();

            // A category with no location belongs to the whole organization.
            var allowed = locationId is { } id
                ? AccessDecision.AllowsAtLocation(assignments, id, WriteRoles)
                : AccessDecision.AllowsOrganizationWide(assignments, WriteRoles);
            if (!allowed)
                throw Refused();
        }

        public async Task CheckReadAsync(Guid userId, Guid organizationId)
        {
            var assignments = await _userRoleLookup.GetRoleAssignmentsAsync(userId, organizationId);

            if (assignments == null || !assignments.Any(a => ReadRoles.Contains(a.Role)))
                throw Refused();
        }

        private BusinessException Refused() => new(_localizer["UnauthorizedMessage"].Value, 403);
    }
}
