namespace RestaurantManagement.Modules.Identity.Services.Sessions
{
    public interface IRefreshTokenCleanupService
    {
        Task<int> PurgeExpiredAsync(DateTime utcNow, CancellationToken cancellationToken);
    }
}
