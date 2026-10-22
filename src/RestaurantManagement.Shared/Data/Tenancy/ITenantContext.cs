namespace RestaurantManagement.Shared.Data.Tenancy
{
    /// <summary>
    /// The organization and location the current request works in. The organization comes from an
    /// open tenant scope first, then from the sign-in token; never from the request body or URL.
    /// </summary>
    public interface ITenantContext
    {
        Guid? OrganizationId { get; }
        Guid? LocationId { get; }
    }
}
