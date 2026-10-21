namespace RestaurantManagement.Shared.Data.Tenancy
{
    /// <summary>
    /// Holds the location selected for the current request (X-Location-Id header), once checked.
    /// Registered per request; null means no location was selected.
    /// </summary>
    public sealed class SelectedLocation
    {
        public Guid? LocationId { get; set; }
    }
}
