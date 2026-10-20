namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// What a member may do in a module at a location: None (no access) &lt; Read (see) &lt;
    /// Manage (create, update, delete; includes Read).
    /// </summary>
    public enum AccessLevel
    {
        None = 0,
        Read = 1,
        Manage = 2
    }
}
