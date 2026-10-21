namespace RestaurantManagement.Modules.Identity.Models.Sessions
{
    public class AuthResponse
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Token { get; set; }
        public List<OrganizationChoice>? Organizations { get; set; }
    }
}