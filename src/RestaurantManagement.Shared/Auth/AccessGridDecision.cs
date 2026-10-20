namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// Pure access decisions over one user's access grid in one organization. The Owner passes
    /// every question with no cells.
    /// </summary>
    public static class AccessGridDecision
    {
        /// <summary>True when the grid gives at least the needed level in the module at that location.</summary>
        public static bool HasLevelAt(AccessGridDto grid, AccessModule module, AccessLevel needed, Guid locationId)
            => throw new NotImplementedException();

        /// <summary>
        /// True when the grid gives at least the needed level in the module at every one of the
        /// locations. An empty set passes only the Owner.
        /// </summary>
        public static bool HasLevelAtAll(AccessGridDto grid, AccessModule module, AccessLevel needed, IReadOnlyCollection<Guid> locationIds)
            => throw new NotImplementedException();

        /// <summary>True when the user is the Owner or has any cell at the location.</summary>
        public static bool WorksAt(AccessGridDto grid, Guid locationId)
            => throw new NotImplementedException();

        /// <summary>True when the user is the Owner or has any cell at all.</summary>
        public static bool HasAnyAccess(AccessGridDto grid)
            => throw new NotImplementedException();
    }
}
