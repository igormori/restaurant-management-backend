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
        {
            if (grid.IsOwner)
            {
                return true;
            }

            // The higher level wins when the same module and location appear twice.
            var held = grid.Cells
                .Where(cell => cell.Module == module && cell.LocationId == locationId)
                .Select(cell => cell.Level)
                .DefaultIfEmpty(AccessLevel.None)
                .Max();

            return held >= needed;
        }

        /// <summary>
        /// True when the grid gives at least the needed level in the module at every one of the
        /// locations. An empty set passes only the Owner.
        /// </summary>
        public static bool HasLevelAtAll(AccessGridDto grid, AccessModule module, AccessLevel needed, IReadOnlyCollection<Guid> locationIds)
        {
            if (grid.IsOwner)
            {
                return true;
            }

            return locationIds.Count > 0
                && locationIds.All(locationId => HasLevelAt(grid, module, needed, locationId));
        }

        /// <summary>True when the user is the Owner or has any cell at the location.</summary>
        public static bool WorksAt(AccessGridDto grid, Guid locationId)
            => grid.IsOwner || grid.Cells.Any(cell => cell.LocationId == locationId && cell.Level > AccessLevel.None);

        /// <summary>True when the user is the Owner or has any cell at all.</summary>
        public static bool HasAnyAccess(AccessGridDto grid)
            => grid.IsOwner || grid.Cells.Any(cell => cell.Level > AccessLevel.None);
    }
}
