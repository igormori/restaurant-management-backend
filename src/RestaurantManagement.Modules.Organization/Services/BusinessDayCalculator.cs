namespace RestaurantManagement.Modules.Organization.Services
{
    public class BusinessDayCalculator : IBusinessDayCalculator
    {
        public DateOnly GetBusinessDate(DateTime utcMoment, string timeZoneId, TimeOnly cutoff)
            => throw new NotImplementedException();
    }
}
