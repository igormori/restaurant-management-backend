namespace RestaurantManagement.Modules.Identity.Services
{
    // STUB (tester): queue-wide limits shared by both email processors. The implementer moves them here.
    public static class PendingEmailQueue
    {
        public const int MaxAttempts = 5;
        public const int BatchSize = 100;
        public static readonly TimeSpan FailedRetention = TimeSpan.FromDays(7);
    }
}
