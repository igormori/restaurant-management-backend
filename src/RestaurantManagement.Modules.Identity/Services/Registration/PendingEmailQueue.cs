namespace RestaurantManagement.Modules.Identity.Services.Registration
{
    // Queue-wide limits shared by both email processors.
    public static class PendingEmailQueue
    {
        public const int MaxAttempts = 5;
        public const int BatchSize = 100;
        public static readonly TimeSpan FailedRetention = TimeSpan.FromDays(7);
    }
}
