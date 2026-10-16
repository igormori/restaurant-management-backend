namespace RestaurantManagement.Shared.Services.AuditLog
{
    /// <summary>
    /// Records audit log entries. Record only: no operation changes or removes an entry.
    /// </summary>
    public interface IAuditLog
    {
        Task RecordAsync(AuditEntryDto entry, CancellationToken cancellationToken = default);
    }
}
