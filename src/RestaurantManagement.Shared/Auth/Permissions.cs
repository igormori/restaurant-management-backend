namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// Fixed list of permissions defined by the product. Each value is also the name of the
    /// access policy that checks it.
    /// </summary>
    public static class Permissions
    {
        public const string MembersManage = "members.manage";
        public const string LocationsManage = "locations.manage";
        public const string SettingsManage = "settings.manage";
        public const string MenuManage = "menu.manage";
        public const string ReportsView = "reports.view";

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
            new[] { MembersManage, LocationsManage, SettingsManage, MenuManage, ReportsView });
    }
}
