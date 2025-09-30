namespace RestaurantManagement.Api.Models.Auth
{
    public class RefreshRequest
    {
        public string RefreshToken { get; set; } = null!;
        public string Email { get; set; } = null!; // or UserId
    }
}