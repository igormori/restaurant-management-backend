using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Editing an organization records one "settings changed" audit entry when the service charge rate,
    /// discount approval limit or business day cutoff changes, and nothing otherwise (plan T3).
    /// Cantina da Nonna starts at a 10% service charge, a 5% discount approval limit and a 04:00 cutoff.
    /// </summary>
    public class OrganizationServiceAuditTests
    {
        private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid CantinaSettingsId = Guid.Parse("88888888-0000-4000-8000-000000000001");

        private static EditOrganizationRequest EditRequest(
            decimal serviceChargeRate, decimal discountApprovalLimit = 5m, string businessDayCutoff = "04:00",
            string name = "Cantina da Nonna") => new()
            {
                Name = name,
                Description = "Cozinha italiana de família",
                ServiceChargeRate = serviceChargeRate,
                DiscountApprovalLimit = discountApprovalLimit,
                BusinessDayCutoff = businessDayCutoff
            };

        private static async Task SeedCantinaAsync(OrganizationTestDatabase database)
        {
            using var context = database.CreateContext();
            context.Organizations.Add(new Entities.Organization
            {
                Id = CantinaId,
                Name = "Cantina da Nonna",
                Description = "Cozinha italiana de família",
                Currency = Currency.BRL,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.OrganizationSettings.Add(new OrganizationSettings
            {
                Id = CantinaSettingsId,
                OrganizationId = CantinaId,
                ServiceChargeRate = 10m,
                DiscountApprovalLimit = 5m,
                BusinessDayCutoff = new TimeOnly(4, 0),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        private static OrganizationService CreateSut(
            OrganizationTestDatabase database, ITenantContext tenant, string role, IAuditLog auditLog,
            ILogger<OrganizationService>? logger = null)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.GetRoleAsync(AnaId, CantinaId).Returns(role);
            return new OrganizationService(database.CreateContext(tenant), userRoleLookup, Substitute.For<IUserRoleAssigner>(),
                new TenantScope(), tenant, TestLocalizer.Create(), logger ?? Substitute.For<ILogger<OrganizationService>>(), auditLog);
        }

        private static decimal ReadDecimal(string? json, string property)
            => JsonDocument.Parse(json!).RootElement.GetProperty(property).GetDecimal();

        private static string? ReadText(string? json, string property)
            => JsonDocument.Parse(json!).RootElement.GetProperty(property).GetString();

        // Scenario 18
        [Theory]
        [InlineData(Roles.Waiter, true)]
        [InlineData(Roles.Owner, false)]
        public async Task EditOrganizationAsync_EditRejected_RecordsNothing(string role, bool tokenHasOrganization)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaAsync(database);
            var auditLog = Substitute.For<IAuditLog>();
            var tenant = TestTenant.Of(tokenHasOrganization ? CantinaId : null);
            var sut = CreateSut(database, tenant, role, auditLog);

            // Act
            var edit = () => sut.EditOrganizationAsync(AnaId, EditRequest(serviceChargeRate: 12m));

            // Assert
            (await Should.ThrowAsync<BusinessException>(edit)).StatusCode.ShouldBe(403);
            auditLog.ReceivedCalls().ShouldBeEmpty();
        }

        // Scenario 19
        [Fact]
        public async Task EditOrganizationAsync_RecordingFails_KeepsSettingsSavedAndLogsError()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaAsync(database);
            var auditLog = Substitute.For<IAuditLog>();
            auditLog.RecordAsync(Arg.Any<AuditEntryDto>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Audit log unavailable"));
            var logger = Substitute.For<ILogger<OrganizationService>>();
            var tenant = TestTenant.Of(CantinaId);
            var sut = CreateSut(database, tenant, Roles.Owner, auditLog, logger);

            // Act
            var response = await sut.EditOrganizationAsync(AnaId, EditRequest(serviceChargeRate: 12m));

            // Assert
            response.ServiceChargeRate.ShouldBe(12m);
            using var readContext = database.CreateContext(tenant);
            (await readContext.OrganizationSettings.SingleAsync()).ServiceChargeRate.ShouldBe(12m);
            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        // Scenario 16
        [Theory]
        [InlineData(12.0, 5.0, "04:00")]
        [InlineData(10.0, 8.0, "04:00")]
        [InlineData(10.0, 5.0, "05:30")]
        public async Task EditOrganizationAsync_OneSettingChanged_RecordsOneSettingsChangedEntry(
            double serviceChargeRate, double discountApprovalLimit, string businessDayCutoff)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaAsync(database);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateSut(database, TestTenant.Of(CantinaId), Roles.Owner, auditLog);

            // Act
            await sut.EditOrganizationAsync(AnaId,
                EditRequest((decimal)serviceChargeRate, (decimal)discountApprovalLimit, businessDayCutoff));

            // Assert
            var call = auditLog.ReceivedCalls().ShouldHaveSingleItem();
            var entry = call.GetArguments()[0].ShouldBeOfType<AuditEntryDto>();
            entry.Action.ShouldBe(AuditActions.SettingsChanged);
            entry.EntityType.ShouldBe("OrganizationSettings");
            entry.EntityId.ShouldBe(CantinaSettingsId);
            entry.LocationId.ShouldBeNull();
            ReadDecimal(entry.Before, "serviceChargeRate").ShouldBe(10m);
            ReadDecimal(entry.Before, "discountApprovalLimit").ShouldBe(5m);
            ReadText(entry.Before, "businessDayCutoff").ShouldBe("04:00");
            ReadDecimal(entry.After, "serviceChargeRate").ShouldBe((decimal)serviceChargeRate);
            ReadDecimal(entry.After, "discountApprovalLimit").ShouldBe((decimal)discountApprovalLimit);
            ReadText(entry.After, "businessDayCutoff").ShouldBe(businessDayCutoff);
        }

        // Scenario 17
        [Theory]
        [InlineData("Cantina da Nonna Batel")]
        [InlineData("Cantina da Nonna")]
        public async Task EditOrganizationAsync_SettingsUnchanged_RecordsNothing(string name)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaAsync(database);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateSut(database, TestTenant.Of(CantinaId), Roles.Owner, auditLog);

            // Act
            await sut.EditOrganizationAsync(AnaId, EditRequest(serviceChargeRate: 10m, name: name));

            // Assert
            auditLog.ReceivedCalls().ShouldBeEmpty();
        }
    }
}
