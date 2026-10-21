using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Services
{
    /// <summary>The final levels one location is given (None cells are not listed).</summary>
    public record LocationGrant(Guid LocationId, IReadOnlyDictionary<AccessModule, AccessLevel> Levels);

    /// <summary>
    /// Who may grant what. All checks run before any write and refuse with 403: the caller has
    /// Members = Manage at every location involved (the Owner passes); the caller is not the target;
    /// the target is not the Owner; no cell is raised above the caller's own level at that location and
    /// module (the Owner has no cap). A cell the target already holds at that level or higher is not a raise.
    /// The target is null for an invitation (nothing held yet).
    /// </summary>
    public interface IMemberDelegationRules
    {
        Task RequireAsync(Guid? targetUserId, IReadOnlyCollection<LocationGrant> grants);
    }
}
