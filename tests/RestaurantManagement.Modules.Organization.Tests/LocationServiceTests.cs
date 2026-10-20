using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Who may create, edit and close a location, decided by the real access guard over a stated grid:
    /// only the Owner creates and closes; Locations Manage at that location edits it.
    /// </summary>
    public class LocationServiceTests
    {
        private static CreateLocationRequest ValidCreateRequest => new()
        {
            Name = "Downtown Branch",
            Address = "123 Main St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "USA",
            PhoneNumber = "555-0100",
            TimeZone = "America/Chicago",
            Code = "DTWN"
        };

        private static EditLocationRequest ValidEditRequest => new()
        {
            Name = "Updated Branch",
            Address = "456 Elm St",
            City = "Springfield",
            State = "IL",
            PostalCode = "62701",
            Country = "USA",
            PhoneNumber = "555-0200",
            Status = LocationStatus.Active,
            TimeZone = "America/Chicago",
            Code = "ELM"
        };

        private static async Task<Guid> SeedOrganizationAsync(OrganizationTestDatabase database)
        {
            var organizationId = Guid.NewGuid();
            using var context = database.CreateContext();

            context.Organizations.Add(new Entities.Organization
            {
                Id = organizationId,
                Name = "Original Restaurant",
                Currency = Currency.USD,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            return organizationId;
        }

        private static async Task<Guid> SeedLocationAsync(OrganizationTestDatabase database, Guid organizationId, string code = "ORIG")
        {
            var locationId = Guid.NewGuid();
            using var context = database.CreateContext();

            context.Locations.Add(new Location
            {
                Id = locationId,
                OrganizationId = organizationId,
                Name = "Original Branch",
                Address = "1 Original St",
                City = "Springfield",
                State = "IL",
                PostalCode = "62701",
                Country = "USA",
                PhoneNumber = "555-0000",
                Status = LocationStatus.Active,
                TimeZone = "America/Chicago",
                Code = code,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            return locationId;
        }

        private static LocationService CreateSut(OrganizationTestDatabase database, Guid organizationId, IAccessGuard guard)
        {
            var tenant = TestTenant.Of(organizationId);
            return new LocationService(database.CreateContext(tenant), guard, tenant, TestLocalizer.Create(),
                Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task CreateLocationAsync_OwnerWithNoStoredLevels_CreatesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var sut = CreateSut(database, organizationId, TestAccess.OwnerGuard(userId, organizationId));

            // Act
            var response = await sut.CreateLocationAsync(userId, ValidCreateRequest);

            // Assert
            response.Should().NotBeNull("the Owner can create a location");
            response.Name.Should().Be(ValidCreateRequest.Name, "the location keeps the name the user chose");
        }

        [Theory]
        [InlineData("AdminWithLocationsManageAtEveryLocation", "create")]
        [InlineData("CashierWithLocationsRead", "create")]
        [InlineData("NewAdminWithNoAccess", "create")]
        [InlineData("AdminWithLocationsManageAtEveryLocation", "close")]
        [InlineData("CashierWithLocationsRead", "close")]
        [InlineData("NewAdminWithNoAccess", "close")]
        [Trait("Rule", "OR36")]
        public async Task CreateOrCloseLocation_NotTheOwner_ThrowsForbiddenAndChangesNothing(string member, string operation)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var batelId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var grid = member switch
            {
                "AdminWithLocationsManageAtEveryLocation" => TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Manage), (batelId, AccessModule.Menus, AccessLevel.Manage)),
                "CashierWithLocationsRead" => TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Read), (batelId, AccessModule.Cashier, AccessLevel.Manage)),
                _ => TestAccess.NoAccessGrid
            };
            var sut = CreateSut(database, organizationId, TestAccess.Guard(userId, organizationId, grid, batelId));

            // Act
            var act = () => operation == "create" ? sut.CreateLocationAsync(userId, ValidCreateRequest) : sut.DeleteLocationAsync(userId, batelId);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "only the Owner creates or closes a location, whatever the Admin may manage");
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            var stored = await verifyContext.Locations.ToListAsync();
            stored.Count.ShouldBe(1, "a refused creation stores no location");
            stored.Single().Status.ShouldBe(LocationStatus.Active, "a refused close leaves the location open");
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task EditLocationAsync_OwnerWithNoStoredLevels_EditsLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var sut = CreateSut(database, organizationId, TestAccess.OwnerGuard(userId, organizationId));

            // Act
            var response = await sut.EditLocationAsync(userId, locationId, ValidEditRequest);

            // Assert
            response.Should().NotBeNull("the Owner can edit a location");
            response.Name.Should().Be(ValidEditRequest.Name, "the edit changes the location name");
        }

        [Fact]
        [Trait("Rule", "OR37")]
        public async Task EditLocationAsync_LocationsManageAtThatLocation_EditsLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var batelId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var guard = TestAccess.Guard(userId, organizationId, TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Manage)), batelId);
            var sut = CreateSut(database, organizationId, guard);

            // Act
            var response = await sut.EditLocationAsync(userId, batelId, ValidEditRequest);

            // Assert
            response.Name.Should().Be(ValidEditRequest.Name, "a member who manages Locations at a location may edit it");
        }

        [Theory]
        [InlineData("ManageAtAnotherLocationOnly")]
        [InlineData("LocationsReadAtThisLocation")]
        [InlineData("NoAccess")]
        [Trait("Rule", "OR37")]
        public async Task EditLocationAsync_NoLocationsManageAtThatLocation_ThrowsForbiddenAndKeepsTheLocation(string member)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var batelId = await SeedLocationAsync(database, organizationId);
            var centroId = await SeedLocationAsync(database, organizationId, code: "CENTRO");
            var userId = Guid.NewGuid();
            var grid = member switch
            {
                "ManageAtAnotherLocationOnly" => TestAccess.Grid((centroId, AccessModule.Locations, AccessLevel.Manage)),
                "LocationsReadAtThisLocation" => TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Read), (batelId, AccessModule.Menus, AccessLevel.Manage)),
                _ => TestAccess.NoAccessGrid
            };
            var sut = CreateSut(database, organizationId, TestAccess.Guard(userId, organizationId, grid, batelId, centroId));

            // Act
            var act = () => sut.EditLocationAsync(userId, batelId, ValidEditRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "editing a location needs Locations Manage at that location");
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            (await verifyContext.Locations.SingleAsync(l => l.Id == batelId)).Name.ShouldBe("Original Branch", "a refused edit changes nothing");
        }

        [Fact]
        [Trait("Rule", "OR36")]
        public async Task EditLocationAsync_AdminWithLocationsManageSetsStatusClosed_ThrowsForbiddenAndKeepsStatus()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var batelId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var guard = TestAccess.Guard(userId, organizationId, TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Manage)), batelId);
            var sut = CreateSut(database, organizationId, guard);
            var request = ValidEditRequest;
            request.Status = LocationStatus.Closed;

            // Act
            var act = () => sut.EditLocationAsync(userId, batelId, request);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "closing a location, even through an edit, is for the Owner only");
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            (await verifyContext.Locations.SingleAsync(l => l.Id == batelId)).Status.ShouldBe(LocationStatus.Active, "a refused close leaves the location open");
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task DeleteLocationAsync_OwnerWithNoStoredLevels_ClosesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var sut = CreateSut(database, organizationId, TestAccess.OwnerGuard(userId, organizationId));

            // Act
            await sut.DeleteLocationAsync(userId, locationId);

            // Assert
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            var location = await verifyContext.Locations.FirstAsync(l => l.Id == locationId);
            location.Status.Should().Be(LocationStatus.Closed, "deleting a location closes it, it is never removed");
        }
    }
}
