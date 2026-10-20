namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// Request-scoped access check. Reads the token user's grid in the token organization from
    /// storage once per request and refuses with 403 when the grid does not allow the action.
    /// </summary>
    public interface IAccessGuard
    {
        /// <summary>Only the Owner passes.</summary>
        Task RequireOwnerAsync();

        /// <summary>The level in the module at one location.</summary>
        Task RequireAtLocationAsync(AccessModule module, AccessLevel level, Guid locationId);

        /// <summary>The level in the module at every location of the set (an empty set passes only the Owner).</summary>
        Task RequireAtLocationsAsync(AccessModule module, AccessLevel level, IReadOnlyCollection<Guid> locationIds);

        /// <summary>The level in the module at every non-closed location of the token organization.</summary>
        Task RequireAtEveryLocationAsync(AccessModule module, AccessLevel level);

        /// <summary>Any access at all: the Owner or any cell.</summary>
        Task RequireAnyAccessAsync();
    }
}
