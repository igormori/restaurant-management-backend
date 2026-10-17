using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using Shouldly;
using Xunit;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Organization settings (spec Rule 5): service charge rate and discount approval limit (0 to 100,
    /// default 0) and business-day cutoff (default 04:00, sent and returned as HH:mm).
    /// </summary>
    public class OrganizationSettingsTests
    {
        private static List<ValidationResult> Validate(object request)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
            return results;
        }

        private static EditOrganizationRequest EditRequest(double? serviceChargeRate, double? discountApprovalLimit, string? businessDayCutoff)
            => new()
            {
                Name = "Cantina da Nonna",
                ServiceChargeRate = (decimal?)serviceChargeRate,
                DiscountApprovalLimit = (decimal?)discountApprovalLimit,
                BusinessDayCutoff = businessDayCutoff
            };

        // Scenario 6
        [Theory]
        [InlineData(120.0, 0.0, "04:00", nameof(EditOrganizationRequest.ServiceChargeRate))]
        [InlineData(-1.0, 0.0, "04:00", nameof(EditOrganizationRequest.ServiceChargeRate))]
        [InlineData(10.555, 0.0, "04:00", nameof(EditOrganizationRequest.ServiceChargeRate))]
        [InlineData(null, 0.0, "04:00", nameof(EditOrganizationRequest.ServiceChargeRate))]
        [InlineData(10.0, 120.0, "04:00", nameof(EditOrganizationRequest.DiscountApprovalLimit))]
        [InlineData(10.0, -1.0, "04:00", nameof(EditOrganizationRequest.DiscountApprovalLimit))]
        [InlineData(10.0, null, "04:00", nameof(EditOrganizationRequest.DiscountApprovalLimit))]
        [InlineData(10.0, 0.0, "4:00", nameof(EditOrganizationRequest.BusinessDayCutoff))]
        [InlineData(10.0, 0.0, "25:00", nameof(EditOrganizationRequest.BusinessDayCutoff))]
        [InlineData(10.0, 0.0, "quatro", nameof(EditOrganizationRequest.BusinessDayCutoff))]
        [InlineData(10.0, 0.0, null, nameof(EditOrganizationRequest.BusinessDayCutoff))]
        [Trait("Rule", "OR23")]
        public void Validate_SettingMissingOutOfRangeOrMalformed_ReturnsErrorForThatSetting(
            double? serviceChargeRate, double? discountApprovalLimit, string? businessDayCutoff, string invalidSetting)
        {
            // Arrange
            var request = EditRequest(serviceChargeRate, discountApprovalLimit, businessDayCutoff);

            // Act
            var results = Validate(request);

            // Assert
            results.SelectMany(r => r.MemberNames).Distinct().ShouldBe(new[] { invalidSetting }, ignoreOrder: false, customMessage: "a setting that is missing, out of range or malformed is refused, and the error names only that setting");
        }

        // Scenario 7
        [Theory]
        [InlineData(0.0, 0.0, "00:00")]
        [InlineData(100.0, 100.0, "23:59")]
        [InlineData(12.5, 7.25, "04:00")]
        [Trait("Rule", "OR23")]
        public void Validate_SettingsWithinLimits_IsValid(double serviceChargeRate, double discountApprovalLimit, string businessDayCutoff)
        {
            // Arrange
            var request = EditRequest(serviceChargeRate, discountApprovalLimit, businessDayCutoff);

            // Act
            var results = Validate(request);

            // Assert
            results.ShouldBeEmpty(customMessage: "settings within the allowed limits are accepted");
        }

        // Scenario 8
        [Fact]
        [Trait("Rule", "OR1")]
        public async Task CreateOrganizationAsync_NewOrganization_ReturnsDefaultSettings()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.UserExistsAsync(userId).Returns(true);
            userRoleLookup.GetOrganizationIdsForUserAsync(userId).Returns(new List<Guid>());
            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, Substitute.For<IUserRoleAssigner>(), new TenantScope(),
                TestTenant.None, TestLocalizer.Create(), Substitute.For<ILogger<OrganizationService>>(), Substitute.For<IAuditLog>());

            // Act
            var created = await sut.CreateOrganizationAsync(userId, new CreateOrganizationRequest { Name = "Maple Leaf Bistro", Currency = "CAD" });

            // Assert: read the stored settings back through the organization list
            userRoleLookup.GetOrganizationIdsForUserAsync(userId).Returns(new List<Guid> { created.Id });
            using var listContext = database.CreateContext();
            var listSut = new OrganizationService(listContext, userRoleLookup, Substitute.For<IUserRoleAssigner>(), new TenantScope(),
                TestTenant.None, TestLocalizer.Create(), Substitute.For<ILogger<OrganizationService>>(), Substitute.For<IAuditLog>());
            var stored = (await listSut.GetOrganizationsAsync(userId)).ShouldHaveSingleItem(customMessage: "the user has the one organization just created");
            stored.ServiceChargeRate.ShouldBe(0m, "a new organization starts with no service charge");
            stored.DiscountApprovalLimit.ShouldBe(0m, "a new organization starts with no discount approval limit");
            stored.BusinessDayCutoff.ShouldBe("04:00", "a new organization starts its business day at 04:00");
        }
    }
}
