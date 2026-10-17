namespace RestaurantManagement.Modules.Identity.Services
{
    public interface IPasswordResetEmailProcessor
    {
        /// <summary>One pass over the queued reset requests due at <paramref name="utcNow"/>. Returns how many were handled.</summary>
        Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken);
    }
}
