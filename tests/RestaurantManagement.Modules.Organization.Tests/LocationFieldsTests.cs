using RestaurantManagement.Shared.Auth;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;
using RestaurantManagement.Shared.Services.AuditLog;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Location time zone, type and code (spec Rules 2–4 and 7) on the create and edit requests and in
    /// the location service. Pizzaria Paulista runs stores in São Paulo and Toronto.
    /// </summary>
    public class LocationFieldsTests
    {
        private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

        private static CreateLocationRequest CreateRequest(string? timeZone, string? type, string? code) => new()
        {
            Name = "Unidade Paulista",
            Address = "Av. Paulista, 1578",
            City = "São Paulo",
            State = "SP",
            PostalCode = "01310-200",
            Country = "Brasil",
            PhoneNumber = "+55 11 3000-1000",
            TimeZone = timeZone,
            Type = type,
            Code = code
        };

        private static EditLocationRequest EditRequest(string? timeZone, string? type, string? code) => new()
        {
            Name = "Queen Street West",
            Address = "350 Queen St W",
            City = "Toronto",
            State = "ON",
            PostalCode = "M5V 2A2",
            Country = "Canada",
            PhoneNumber = "+1 416 555 0142",
            Status = LocationStatus.Active,
            TimeZone = timeZone,
            Type = type,
            Code = code
        };

        public static TheoryData<object, string> InvalidRequests => new()
        {
            { CreateRequest(null, null, "PAU"), nameof(CreateLocationRequest.TimeZone) },
            { CreateRequest("America/Curitiba_Centro", null, "PAU"), nameof(CreateLocationRequest.TimeZone) },
            { CreateRequest("E. South America Standard Time", null, "PAU"), nameof(CreateLocationRequest.TimeZone) },
            { CreateRequest("America/Sao_Paulo", "Warehouse", "PAU"), nameof(CreateLocationRequest.Type) },
            { CreateRequest("America/Sao_Paulo", "1", "PAU"), nameof(CreateLocationRequest.Type) },
            { CreateRequest("America/Sao_Paulo", null, null), nameof(CreateLocationRequest.Code) },
            { CreateRequest("America/Sao_Paulo", null, ""), nameof(CreateLocationRequest.Code) },
            { CreateRequest("America/Sao_Paulo", null, "PAU-01"), nameof(CreateLocationRequest.Code) },
            { CreateRequest("America/Sao_Paulo", null, "PAULISTA001"), nameof(CreateLocationRequest.Code) },
            { EditRequest(null, null, "QSW"), nameof(EditLocationRequest.TimeZone) },
            { EditRequest("Eastern Standard Time", null, "QSW"), nameof(EditLocationRequest.TimeZone) },
            { EditRequest("America/Toronto", "Warehouse", "QSW"), nameof(EditLocationRequest.Type) },
            { EditRequest("America/Toronto", null, "QUEENSTREET"), nameof(EditLocationRequest.Code) },
        };

        public static TheoryData<object> ValidRequests => new()
        {
            CreateRequest("America/Sao_Paulo", null, "PAU"),
            CreateRequest("America/Toronto", "CentralKitchen", "COZINHA123"),
            CreateRequest("America/New_York", "Store", "nyc1"),
            EditRequest("America/Toronto", "Store", "QSW"),
        };

        private static List<ValidationResult> Validate(object request)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
            return results;
        }

        private static async Task<Guid> SeedOrganizationAsync(OrganizationTestDatabase database)
        {
            var organizationId = Guid.NewGuid();
            using var context = database.CreateContext();
            context.Organizations.Add(new Entities.Organization
            {
                Id = organizationId,
                Name = "Pizzaria Paulista",
                Currency = Currency.BRL,
                CreatedAt = CreationTime,
                UpdatedAt = CreationTime
            });
            await context.SaveChangesAsync();
            return organizationId;
        }

        private static async Task<Guid> SeedLocationAsync(OrganizationTestDatabase database, Guid organizationId, LocationType type = LocationType.Store)
        {
            var locationId = Guid.NewGuid();
            using var context = database.CreateContext();
            context.Locations.Add(new Location
            {
                Id = locationId,
                OrganizationId = organizationId,
                Name = "Unidade Paulista",
                Address = "Av. Paulista, 1578",
                City = "São Paulo",
                State = "SP",
                PostalCode = "01310-200",
                Country = "Brasil",
                PhoneNumber = "+55 11 3000-1000",
                Status = LocationStatus.Active,
                TimeZone = "America/Sao_Paulo",
                Type = type,
                Code = "PAU",
                CreatedAt = CreationTime,
                UpdatedAt = CreationTime
            });
            await context.SaveChangesAsync();
            return locationId;
        }

        private static LocationService CreateService(Data.OrganizationDbContext context, Guid userId, Guid organizationId, string role)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.GetRoleAssignmentsAsync(userId, organizationId).Returns(role is null ? new List<RoleAssignmentDto>() : new List<RoleAssignmentDto> { new() { Role = role } });
            return new LocationService(context, userRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());
        }

        // Scenario 11
        [Theory]
        [MemberData(nameof(InvalidRequests))]
        [Trait("Rule", "OR26")]
        public void Validate_LocationFieldMissingOrInvalid_ReturnsErrorForThatField(object request, string invalidField)
        {
            // Arrange: the request comes from InvalidRequests

            // Act
            var results = Validate(request);

            // Assert
            results.SelectMany(r => r.MemberNames).Distinct().ShouldBe(new[] { invalidField }, ignoreOrder: false, customMessage: "a location with a missing or invalid field is refused, and the error names only that field");
        }

        // Scenario 12
        [Theory]
        [MemberData(nameof(ValidRequests))]
        [Trait("Rule", "OR26")]
        public void Validate_ValidTimeZoneTypeAndCode_IsValid(object request)
        {
            // Arrange: the request comes from ValidRequests

            // Act
            var results = Validate(request);

            // Assert
            results.ShouldBeEmpty(customMessage: "a location with a supported time zone, type and code is accepted");
        }

        // Scenario 13
        [Fact]
        [Trait("Rule", "OR27")]
        public async Task CreateLocationAsync_TypeOmittedAndLowercaseCode_ReturnsStoreTimeZoneAndUppercaseCode()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = CreateService(context, userId, organizationId, Roles.Owner);

            // Act
            await sut.CreateLocationAsync(userId, CreateRequest("America/Sao_Paulo", null, " pau1 "));

            // Assert: read the stored location back through the location list
            using var listContext = database.CreateContext(TestTenant.Of(organizationId));
            var stored = (await CreateService(listContext, userId, organizationId, Roles.Owner).GetLocationsAsync(userId)).ShouldHaveSingleItem(customMessage: "the new location is stored once");
            stored.TimeZone.ShouldBe("America/Sao_Paulo", "the location keeps the time zone chosen for it");
            stored.Type.ShouldBe(LocationType.Store, "a location created without a type is a Store");
            stored.Code.ShouldBe("PAU1", "a location code is always stored in uppercase");
        }

        // Scenario 14
        [Fact]
        [Trait("Rule", "OR27")]
        public async Task EditLocationAsync_NewTimeZoneTypeAndCode_ReturnsThem()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = CreateService(context, userId, organizationId, Roles.Admin);

            // Act
            var response = await sut.EditLocationAsync(userId, locationId, EditRequest("America/Toronto", "CentralKitchen", "yyz1"));

            // Assert
            response.TimeZone.ShouldBe("America/Toronto", "editing a location changes its time zone");
            response.Type.ShouldBe(LocationType.CentralKitchen, "editing a location changes its type");
            response.Code.ShouldBe("YYZ1", "editing a location changes its code");
        }

        // Scenario 24
        [Fact]
        [Trait("Rule", "OR27")]
        public async Task EditLocationAsync_TypeOmitted_KeepsStoredType()
        {
            // Arrange: the location is a central kitchen
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId, LocationType.CentralKitchen);
            var userId = Guid.NewGuid();
            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = CreateService(context, userId, organizationId, Roles.Owner);

            // Act
            await sut.EditLocationAsync(userId, locationId, EditRequest("America/Sao_Paulo", null, "PAU"));

            // Assert: read the stored location back through the location list
            using var listContext = database.CreateContext(TestTenant.Of(organizationId));
            var stored = (await CreateService(listContext, userId, organizationId, Roles.Owner).GetLocationsAsync(userId)).ShouldHaveSingleItem(customMessage: "the edited location is still stored once");
            stored.Type.ShouldBe(LocationType.CentralKitchen, "an edit that leaves the type out keeps the type the location already had");
        }

        // Scenario 19
        [Theory]
        [InlineData(Roles.Manager)]
        [InlineData(Roles.Cashier)]
        [InlineData(Roles.Waiter)]
        [InlineData(Roles.Kitchen)]
        [Trait("Rule", "OR10")]
        public async Task EditLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndChangesNothing(string role)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = CreateService(context, userId, organizationId, role);

            // Act
            var act = () => sut.EditLocationAsync(userId, locationId, EditRequest("America/Toronto", "CentralKitchen", "YYZ1"));

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "only an Owner or Admin may edit a location");
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            (await verifyContext.Locations.SingleAsync(l => l.Id == locationId)).Name.ShouldBe("Unidade Paulista", "a refused edit leaves the location unchanged");
        }
    }
}
