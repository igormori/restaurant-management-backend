using RestaurantManagement.Shared.Auth;
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
    /// Editing or deleting (closing) a location records one "location status changed" audit entry when
    /// its status actually changes, and nothing otherwise (plan T4). Ana is signed in to Cantina da Nonna;
    /// Unidade Batel belongs to Cantina, Unidade Centro to Trattoria do Sol.
    /// </summary>
    public class LocationServiceAuditTests
    {
        private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid CantinaBatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
        private static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");

        private static readonly Dictionary<string, Guid> LocationsByOrganization = new()
        {
            ["Cantina"] = CantinaBatelId,
            ["Trattoria"] = TrattoriaCentroId
        };

        // The requests a test sends; the name is shown in the test results.
        private static readonly Dictionary<string, Func<LocationService, Guid, Task>> Operations = new()
        {
            ["edit to TemporarilyClosed"] = (sut, locationId) => sut.EditLocationAsync(AnaId, locationId, EditRequest(LocationStatus.TemporarilyClosed)),
            ["edit keeping Active"] = (sut, locationId) => sut.EditLocationAsync(AnaId, locationId, EditRequest(LocationStatus.Active)),
            ["edit keeping Closed"] = (sut, locationId) => sut.EditLocationAsync(AnaId, locationId, EditRequest(LocationStatus.Closed)),
            ["delete"] = (sut, locationId) => sut.DeleteLocationAsync(AnaId, locationId)
        };

        private static EditLocationRequest EditRequest(LocationStatus status) => new()
        {
            Name = "Unidade Batel",
            Address = "Av. do Batel, 1868",
            City = "Curitiba",
            State = "PR",
            PostalCode = "80420-090",
            Country = "Brasil",
            PhoneNumber = "+55 41 3000-2000",
            Status = status,
            TimeZone = "America/Sao_Paulo",
            Code = "BATEL"
        };

        private static async Task SeedAsync(OrganizationTestDatabase database, LocationStatus batelStatus)
        {
            using var context = database.CreateContext();
            context.Organizations.AddRange(
                new Entities.Organization { Id = CantinaId, Name = "Cantina da Nonna", Currency = Currency.BRL, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Entities.Organization { Id = TrattoriaId, Name = "Trattoria do Sol", Currency = Currency.BRL, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            context.Locations.AddRange(
                Location(CantinaBatelId, CantinaId, "Unidade Batel", "BATEL", batelStatus),
                Location(TrattoriaCentroId, TrattoriaId, "Unidade Centro", "CENTRO", LocationStatus.Active));
            await context.SaveChangesAsync();
        }

        private static Location Location(Guid id, Guid organizationId, string name, string code, LocationStatus status) => new()
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Address = "Av. do Batel, 1868",
            City = "Curitiba",
            State = "PR",
            PostalCode = "80420-090",
            Country = "Brasil",
            PhoneNumber = "+55 41 3000-2000",
            Status = status,
            TimeZone = "America/Sao_Paulo",
            Code = code,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static LocationService CreateSut(
            OrganizationTestDatabase database, string role, IAuditLog auditLog, ILogger<LocationService>? logger = null)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.GetRoleAssignmentsAsync(AnaId, CantinaId).Returns(role is null ? new List<RoleAssignmentDto>() : new List<RoleAssignmentDto> { new() { Role = role } });
            var tenant = TestTenant.Of(CantinaId);
            return new LocationService(database.CreateContext(tenant), userRoleLookup, tenant, TestLocalizer.Create(),
                auditLog, logger ?? Substitute.For<ILogger<LocationService>>());
        }

        // Scenario 23
        [Theory]
        [InlineData("edit to TemporarilyClosed", Roles.Waiter, "Cantina", 403)]
        [InlineData("delete", Roles.Waiter, "Cantina", 403)]
        [InlineData("edit to TemporarilyClosed", Roles.Owner, "Trattoria", 404)]
        [Trait("Rule", "OR29")]
        public async Task ChangeLocationStatus_RequestRejected_RecordsNothing(
            string operation, string role, string locationOrganization, int expectedStatusCode)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database, LocationStatus.Active);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateSut(database, role, auditLog);

            // Act
            var change = () => Operations[operation](sut, LocationsByOrganization[locationOrganization]);

            // Assert
            (await Should.ThrowAsync<BusinessException>(change)).StatusCode.ShouldBe(expectedStatusCode, "a refused status change is rejected with its error");
            auditLog.ReceivedCalls().ShouldBeEmpty(customMessage: "a refused status change is not recorded in the audit log");
        }

        // Scenario 24
        [Fact]
        [Trait("Rule", "OR29")]
        public async Task DeleteLocationAsync_RecordingFails_KeepsLocationClosedAndLogsError()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database, LocationStatus.Active);
            var auditLog = Substitute.For<IAuditLog>();
            auditLog.RecordAsync(Arg.Any<AuditEntryDto>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Audit log unavailable"));
            var logger = Substitute.For<ILogger<LocationService>>();
            var sut = CreateSut(database, Roles.Owner, auditLog, logger);

            // Act
            var delete = () => sut.DeleteLocationAsync(AnaId, CantinaBatelId);

            // Assert
            await Should.NotThrowAsync(delete);
            using var readContext = database.CreateContext(TestTenant.Of(CantinaId));
            (await readContext.Locations.SingleAsync(l => l.Id == CantinaBatelId)).Status.ShouldBe(LocationStatus.Closed, "the location is closed even when the audit entry cannot be recorded");
            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        // Scenario 21
        [Theory]
        [InlineData("edit to TemporarilyClosed", "TemporarilyClosed")]
        [InlineData("delete", "Closed")]
        [Trait("Rule", "OR29")]
        public async Task ChangeLocationStatus_StatusChangesFromActive_RecordsOneLocationStatusChangedEntry(string operation, string expectedAfter)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database, LocationStatus.Active);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateSut(database, Roles.Owner, auditLog);

            // Act
            await Operations[operation](sut, CantinaBatelId);

            // Assert
            var call = auditLog.ReceivedCalls().ShouldHaveSingleItem(customMessage: "a status change records exactly one audit entry");
            var entry = call.GetArguments()[0].ShouldBeOfType<AuditEntryDto>();
            entry.Action.ShouldBe(AuditActions.LocationStatusChanged, "the audit entry says a location status changed");
            entry.EntityType.ShouldBe("Location");
            entry.EntityId.ShouldBe(CantinaBatelId, "the audit entry points at the location that changed");
            entry.LocationId.ShouldBe(CantinaBatelId);
            entry.Before.ShouldBe("Active", "the audit entry records the status before the change");
            entry.After.ShouldBe(expectedAfter, "the audit entry records the status after the change");
        }

        // Scenario 22
        [Theory]
        [InlineData("edit keeping Active", LocationStatus.Active)]
        [InlineData("edit keeping Closed", LocationStatus.Closed)]
        [InlineData("delete", LocationStatus.Closed)]
        [Trait("Rule", "OR29")]
        public async Task ChangeLocationStatus_StatusUnchanged_RecordsNothing(string operation, LocationStatus currentStatus)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database, currentStatus);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateSut(database, Roles.Owner, auditLog);

            // Act
            await Operations[operation](sut, CantinaBatelId);

            // Assert
            auditLog.ReceivedCalls().ShouldBeEmpty(customMessage: "a status change that changes nothing is not recorded in the audit log");
        }
    }
}
