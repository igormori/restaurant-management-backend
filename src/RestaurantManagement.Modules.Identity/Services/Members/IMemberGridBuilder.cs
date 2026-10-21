using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Services
{
    /// <summary>The roles chosen and the final levels of one location (None is never stored, so never listed).</summary>
    public record BuiltGrid(IReadOnlyList<OrganizationRole> Roles, IReadOnlyDictionary<AccessModule, AccessLevel> Levels);

    /// <summary>
    /// Builds the final grid of one location from role presets (higher level per cell wins) and the
    /// changed cells on top. Shared by editing a member and by invitations.
    /// </summary>
    public interface IMemberGridBuilder
    {
        Task<BuiltGrid> BuildAsync(
            Guid organizationId, IReadOnlyCollection<string> roleNames, IReadOnlyDictionary<AccessModule, AccessLevel> changedLevels);
    }
}
