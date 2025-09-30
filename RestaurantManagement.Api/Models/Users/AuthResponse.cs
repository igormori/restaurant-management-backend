namespace RestaurantManagement.Api.Models.Users
{
    public class AuthResponse
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; } 
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
    }
}