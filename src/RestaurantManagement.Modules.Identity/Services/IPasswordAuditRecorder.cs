namespace RestaurantManagement.Modules.Identity.Services
{
    /// <summary>Records a password audit entry in every organization the user holds a role in (task T5).</summary>
    public interface IPasswordAuditRecorder
    {
        Task RecordAsync(Guid userId, string action, CancellationToken cancellationToken = default);
    }
}
