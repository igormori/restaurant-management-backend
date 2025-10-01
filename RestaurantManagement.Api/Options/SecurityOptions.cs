namespace RestaurantManagement.Api.Options
{
    public class SecurityOptions
    {
        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 15;
    }
}