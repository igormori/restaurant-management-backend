using RestaurantManagement.Modules.Organization.Services;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// A location's business day (spec Rule 6): the local date in the location's time zone, minus one
    /// day when the local time is before the cutoff. Most cases use the default cutoff of 04:00.
    /// </summary>
    public class BusinessDayCalculatorTests
    {
        private static readonly TimeOnly FourAm = new(4, 0);

        private static DateTime Utc(string moment) => DateTime.SpecifyKind(DateTime.Parse(moment, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

        // Scenario 20
        [Theory]
        [InlineData("America/Curitiba_Centro")]
        [InlineData("E. South America Standard Time")]
        [InlineData("")]
        [InlineData(null)]
        [Trait("Rule", "OR30")]
        public void GetBusinessDate_UnknownTimeZone_ThrowsArgumentException(string? timeZoneId)
        {
            // Arrange
            var sut = new BusinessDayCalculator();

            // Act
            Action act = () => sut.GetBusinessDate(Utc("2026-10-15T05:30:00"), timeZoneId!, FourAm);

            // Assert
            act.ShouldThrow<ArgumentException>("a location with an unknown time zone cannot have a business day");
        }

        // Scenario 21
        [Theory]
        [InlineData("2026-10-15T05:30:00", "America/Sao_Paulo")] // 02:30 local, 15 Oct
        [InlineData("2026-10-15T06:30:00", "America/Toronto")]   // 02:30 local, 15 Oct
        [Trait("Rule", "OR30")]
        public void GetBusinessDate_SaleAt0230LocalBeforeCutoff_ReturnsPreviousDay(string utcMoment, string timeZoneId)
        {
            // Arrange
            var sut = new BusinessDayCalculator();

            // Act
            var businessDate = sut.GetBusinessDate(Utc(utcMoment), timeZoneId, FourAm);

            // Assert
            businessDate.ShouldBe(new DateOnly(2026, 10, 14), "a sale made before the cutoff belongs to the previous business day");
        }

        // Scenario 22
        [Theory]
        [InlineData("2026-10-15T07:00:00", "America/Sao_Paulo", "04:00", "2026-10-15")]  // exactly 04:00 local: same day
        [InlineData("2026-10-15T08:00:00", "America/Toronto", "04:00", "2026-10-15")]    // exactly 04:00 local: same day
        [InlineData("2026-10-15T16:00:00", "America/Toronto", "04:00", "2026-10-15")]    // 12:00 local, after the cutoff
        [InlineData("2026-10-16T03:30:00", "America/Toronto", "04:00", "2026-10-15")]    // 23:30 local on 15 Oct, UTC is already 16 Oct
        [InlineData("2026-10-16T05:00:00", "America/New_York", "04:00", "2026-10-15")]   // 01:00 local on 16 Oct, before the cutoff
        [InlineData("2026-11-01T07:30:00", "America/Toronto", "04:00", "2026-10-31")]    // 02:30 local just after daylight saving ends
        [InlineData("2026-10-15T05:30:00", "America/Sao_Paulo", "00:00", "2026-10-15")]  // 02:30 local, cutoff at midnight: same day
        [InlineData("2026-10-15T08:30:00", "America/Sao_Paulo", "06:00", "2026-10-14")]  // 05:30 local, before a 06:00 cutoff
        [InlineData("2026-10-15T09:00:00", "America/Toronto", "05:00", "2026-10-15")]    // 05:00 local, exactly at a 05:00 cutoff
        [Trait("Rule", "OR30")]
        public void GetBusinessDate_AtOrAfterCutoffOrAcrossUtcMidnight_ReturnsLocalBusinessDay(string utcMoment, string timeZoneId, string cutoff, string expectedDate)
        {
            // Arrange
            var sut = new BusinessDayCalculator();

            // Act
            var businessDate = sut.GetBusinessDate(Utc(utcMoment), timeZoneId, TimeOnly.Parse(cutoff, System.Globalization.CultureInfo.InvariantCulture));

            // Assert
            businessDate.ShouldBe(DateOnly.Parse(expectedDate, System.Globalization.CultureInfo.InvariantCulture), "a sale at or after the cutoff belongs to the local business day");
        }
    }
}
