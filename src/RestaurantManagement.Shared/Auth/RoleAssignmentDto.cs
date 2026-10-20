namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// One role a user holds in an organization. No location means the role applies to all
    /// locations of the organization, including locations added later.
    /// </summary>
    public class RoleAssignmentDto
    {
        public string Role { get; init; } = string.Empty;

        public Guid? LocationId { get; init; }
    }
}
