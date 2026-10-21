namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IPasswordResetEmailProcessor
    {
        // The count of handled requests lets callers and tests see whether a pass did any work.
        Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken);
    }
}
