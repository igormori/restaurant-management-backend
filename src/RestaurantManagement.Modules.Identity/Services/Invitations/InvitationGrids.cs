using System.Text.Json;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Invitations;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Modules.Identity.Services.Members;

namespace RestaurantManagement.Modules.Identity.Services.Invitations
{
    /// <summary>The location an invitation covers, its display name and the grid built for it.</summary>
    public record LocationGridPlan(Guid LocationId, string LocationName, BuiltGrid Grid);

    /// <summary>Reads, stores and describes the per-location grids of an invitation.</summary>
    public static class InvitationGrids
    {
        public static Dictionary<AccessModule, AccessLevel> ParseLevels(
            Dictionary<string, string> levels, IStringLocalizer<SharedResource> localizer)
        {
            var parsed = new Dictionary<AccessModule, AccessLevel>();
            foreach (var (moduleName, levelName) in levels)
            {
                if (!Enum.TryParse<AccessModule>(moduleName, true, out var module) || !Enum.IsDefined(module)
                    || !Enum.TryParse<AccessLevel>(levelName, true, out var level) || !Enum.IsDefined(level))
                    throw new BusinessException(localizer["ValidationFailed"].Value, 400);

                parsed[module] = level;
            }

            return parsed;
        }

        public static void AddRows(IdentityDbContext db, OrganizationInvitation invitation, IEnumerable<LocationGridPlan> plans)
        {
            foreach (var plan in plans)
            {
                foreach (var role in plan.Grid.Roles)
                    db.OrganizationInvitationRoles.Add(new OrganizationInvitationRole
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = invitation.OrganizationId,
                        InvitationId = invitation.Id,
                        LocationId = plan.LocationId,
                        RoleId = role.Id,
                        CreatedAt = invitation.CreatedAt,
                        UpdatedAt = invitation.CreatedAt
                    });
                foreach (var (module, level) in plan.Grid.Levels)
                    db.OrganizationInvitationLevels.Add(new OrganizationInvitationLevel
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = invitation.OrganizationId,
                        InvitationId = invitation.Id,
                        LocationId = plan.LocationId,
                        Module = module,
                        Level = level,
                        CreatedAt = invitation.CreatedAt,
                        UpdatedAt = invitation.CreatedAt
                    });
            }
        }

        public static string Describe(string email, IEnumerable<LocationGridPlan> plans)
            => JsonSerializer.Serialize(new
            {
                Email = email,
                Locations = plans.Select(p => new
                {
                    p.LocationId,
                    Roles = p.Grid.Roles.Select(r => r.Name).OrderBy(n => n).ToList(),
                    Levels = p.Grid.Levels.OrderBy(l => l.Key).ToDictionary(l => l.Key.ToString(), l => l.Value.ToString())
                })
            });
    }
}
