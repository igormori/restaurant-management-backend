namespace RestaurantManagement.Modules.Identity.Models
{
    // Stub (tester): validation and password rules are part of T8. Never add a user ID here.
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
