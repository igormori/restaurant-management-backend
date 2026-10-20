namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// Pure access decisions over all of a user's role assignments in one organization.
    /// </summary>
    public static class AccessDecision
    {
        /// <summary>
        /// For actions that belong to the organization as a whole: true when an all-locations
        /// assignment has one of the allowed roles.
        /// </summary>
        public static bool AllowsOrganizationWide(IEnumerable<RoleAssignmentDto> assignments, IReadOnlyCollection<string> allowedRoles)
            => assignments.Any(a => a.LocationId is null && allowedRoles.Contains(a.Role));

        /// <summary>
        /// For actions at one location: true when an all-locations assignment, or an assignment
        /// for that exact location, has one of the allowed roles.
        /// </summary>
        public static bool AllowsAtLocation(IEnumerable<RoleAssignmentDto> assignments, Guid locationId, IReadOnlyCollection<string> allowedRoles)
            => assignments.Any(a => (a.LocationId is null || a.LocationId == locationId) && allowedRoles.Contains(a.Role));
    }
}
