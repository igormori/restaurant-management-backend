namespace RestaurantManagement.Api.Models.Users
{
    public class RegisterRequest
    {
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; } 
        public string Password { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
    }
}