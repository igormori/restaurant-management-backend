namespace RestaurantManagement.Modules.Identity.Models
{
    // Stub (tester): validation and password rules are part of T7.
    public class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
