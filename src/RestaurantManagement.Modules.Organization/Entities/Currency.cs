namespace RestaurantManagement.Modules.Organization.Entities
{
    /// <summary>
    /// The currency an organization sells in, chosen at creation. Numbering starts at 1 so the
    /// default value of the enum is never a real currency (no country default).
    /// </summary>
    public enum Currency
    {
        BRL = 1,
        USD = 2,
        CAD = 3
    }
}
