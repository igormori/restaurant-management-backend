namespace RestaurantManagement.Web.RateLimiting
{
    public interface IRateLimitRejectionReporter
    {
        void Report(string endpoint, string clientIp, TimeSpan retryAfter);
    }
}
