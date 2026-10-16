namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IRefreshTokenCleanupService
    {
        Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken);
    }
}
