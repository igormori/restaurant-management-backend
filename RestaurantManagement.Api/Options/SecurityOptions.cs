namespace RestaurantManagement.Api.Options
{
    public class SecurityOptions
    {
        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 15;
        public int VerificationCodeExpiryMinutes { get; set; } = 15;
        public int ResendCooldownSeconds { get; set; } = 60;
    }
}