namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// Read-only map from role name to the permissions that role grants.
    /// </summary>
    public static class RolePermissions
    {
        public static IReadOnlySet<string> GetPermissions(string role) => throw new NotImplementedException();

        public static bool HasPermission(string role, string permission) => throw new NotImplementedException();
    }
}
