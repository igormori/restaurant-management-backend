namespace RestaurantManagement.Shared.Services.AuditLog
{
    /// <summary>
    /// Fixed list of audited actions. Stored as text, so new actions need no migration.
    /// </summary>
    public static class AuditActions
    {
        public const string MemberRoleChanged = "member.role_changed";
        public const string MemberAccessChanged = "member.access_changed";
        public const string MemberInvited = "member.invited";
        public const string MemberInvitationCancelled = "member.invitation_cancelled";
        public const string SettingsChanged = "settings.changed";
        public const string LocationStatusChanged = "location.status_changed";

        public const string PasswordReset = "password.reset";
        public const string PasswordChanged = "password.changed";

        public static readonly IReadOnlySet<string> All = new HashSet<string>
        {
            MemberRoleChanged, MemberAccessChanged, MemberInvited, MemberInvitationCancelled, SettingsChanged, LocationStatusChanged, PasswordReset, PasswordChanged
        };
    }
}
