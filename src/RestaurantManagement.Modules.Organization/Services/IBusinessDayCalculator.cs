namespace RestaurantManagement.Modules.Organization.Services
{
    /// <summary>
    /// Computes the business day a moment belongs to for a location: the local date in the
    /// location's time zone, minus one day when the local time is before the cutoff.
    /// </summary>
    public interface IBusinessDayCalculator
    {
        /// <param name="utcMoment">The moment, in UTC (Coordinated Universal Time).</param>
        /// <param name="timeZoneId">An IANA (Internet Assigned Numbers Authority) time zone name, e.g. America/Sao_Paulo.</param>
        /// <param name="cutoff">The business-day cutoff time of day, e.g. 04:00.</param>
        DateOnly GetBusinessDate(DateTime utcMoment, string timeZoneId, TimeOnly cutoff);
    }
}
