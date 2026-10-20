namespace RestaurantManagement.Shared.Auth
{
    /// <summary>
    /// The areas of the product a member's access is set for, one level per location.
    /// </summary>
    public enum AccessModule
    {
        Locations,
        Menus,
        Cashier,
        Orders,
        Kitchen,
        Reports,
        Members
    }
}
