namespace RestaurantManagement.Modules.Identity.Models
{
    public class AuthResponse
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
    }
}