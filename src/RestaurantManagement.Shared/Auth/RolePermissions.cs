using System.Collections.Frozen;

namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// Read-only map from role name to the permissions that role grants.
    /// </summary>
    public static class RolePermissions
    {
        private static readonly IReadOnlySet<string> None = FrozenSet<string>.Empty;

        private static readonly FrozenDictionary<string, IReadOnlySet<string>> Map =
            new Dictionary<string, IReadOnlySet<string>>
            {
                [Roles.Owner] = Permissions.All.ToFrozenSet(),
                [Roles.Admin] = Permissions.All.ToFrozenSet(),
                [Roles.Manager] = new[] { Permissions.MenuManage, Permissions.ReportsView }.ToFrozenSet(),
                [Roles.Cashier] = None,
                [Roles.Waiter] = None,
                [Roles.Kitchen] = None
            }.ToFrozenDictionary();

        public static IReadOnlySet<string> GetPermissions(string role) =>
            role is not null && Map.TryGetValue(role, out var permissions) ? permissions : None;

        public static bool HasPermission(string role, string permission) =>
            GetPermissions(role).Contains(permission);
    }
}
