namespace RestaurantManagement.Modules.Organization.Services
{
    public class BusinessDayCalculator : IBusinessDayCalculator
    {
        public DateOnly GetBusinessDate(DateTime utcMoment, string timeZoneId, TimeOnly cutoff)
        {
            var zone = ResolveZone(timeZoneId);
            var utc = DateTime.SpecifyKind(utcMoment, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);

            var date = DateOnly.FromDateTime(local);
            return TimeOnly.FromDateTime(local) < cutoff ? date.AddDays(-1) : date;
        }

        private static TimeZoneInfo ResolveZone(string? timeZoneId)
        {
            // Windows-style names contain spaces; only IANA names are accepted.
            if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Contains(' '))
                throw new ArgumentException("Unknown IANA time zone.", nameof(timeZoneId));

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new ArgumentException("Unknown IANA time zone.", nameof(timeZoneId), ex);
            }
        }
    }
}
