namespace RestaurantManagement.Shared.Services.AuditLog
{
    /// <summary>
    /// Fixed list of audited actions. Stored as text, so new actions need no migration.
    /// </summary>
    public static class AuditActions
    {
        public const string MemberRoleChanged = "member.role_changed";
        public const string SettingsChanged = "settings.changed";
        public const string LocationStatusChanged = "location.status_changed";
    }
}
