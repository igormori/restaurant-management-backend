using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Services.Roles
{
    /// <summary>
    /// Who may create, edit or rename a role. Runs before any write and refuses with 403: no organization
    /// in the token, not a member, or a non-Owner without Members = Manage at every non-closed location
    /// (AccessDenied); or any current or proposed level above the caller's level for that module at every
    /// non-closed location (CannotGrantAboveOwnLevel). The Owner passes everything. A create has no current
    /// levels and a rename has no proposed levels (pass empty dictionaries).
    /// </summary>
    public interface IRoleManagementRules
    {
        Task RequireAsync(
            IReadOnlyDictionary<AccessModule, AccessLevel> currentLevels,
            IReadOnlyDictionary<AccessModule, AccessLevel> proposedLevels);
    }
}
