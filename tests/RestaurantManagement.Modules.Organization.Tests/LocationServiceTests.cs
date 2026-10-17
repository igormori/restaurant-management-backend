using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;
using RestaurantManagement.Shared.Services.AuditLog;
using Microsoft.Extensions.Logging;

namespace RestaurantManagement.Modules.Organization.Tests
{
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

        private static async Task<Guid> SeedLocationAsync(OrganizationTestDatabase database, Guid organizationId)
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
                Code = "ORIG",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            return locationId;
        }

        private static IUserRoleLookup CreateUserRoleLookup(Guid userId, Guid organizationId, string? role)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.GetRoleAsync(userId, organizationId).Returns(role);
            return userRoleLookup;
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task CreateLocationAsync_WhenUserIsAdminOfOrganization_CreatesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = new LocationService(context, userRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var response = await sut.CreateLocationAsync(userId, ValidCreateRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidCreateRequest.Name);
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task CreateLocationAsync_WhenUserIsOwnerOfOrganization_CreatesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = new LocationService(context, userRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var response = await sut.CreateLocationAsync(userId, ValidCreateRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidCreateRequest.Name);
        }

        [Fact]
        [Trait("Rule", "OR10")]
        public async Task CreateLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);

            var waiterUserId = Guid.NewGuid();
            var waiterUserRoleLookup = CreateUserRoleLookup(waiterUserId, organizationId, Roles.Waiter);
            using var waiterContext = database.CreateContext(TestTenant.Of(organizationId));
            var waiterSut = new LocationService(waiterContext, waiterUserRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            var noRoleUserId = Guid.NewGuid();
            var noRoleUserRoleLookup = CreateUserRoleLookup(noRoleUserId, organizationId, role: null);
            using var noRoleContext = database.CreateContext(TestTenant.Of(organizationId));
            var noRoleSut = new LocationService(noRoleContext, noRoleUserRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var waiterAct = () => waiterSut.CreateLocationAsync(waiterUserId, ValidCreateRequest);
            var noRoleAct = () => noRoleSut.CreateLocationAsync(noRoleUserId, ValidCreateRequest);

            // Assert
            await waiterAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
            await noRoleAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task EditLocationAsync_WhenUserIsAdminOfOrganization_EditsLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = new LocationService(context, userRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var response = await sut.EditLocationAsync(userId, locationId, ValidEditRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidEditRequest.Name);
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task EditLocationAsync_WhenUserIsOwnerOfOrganization_EditsLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = new LocationService(context, userRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var response = await sut.EditLocationAsync(userId, locationId, ValidEditRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidEditRequest.Name);
        }

        [Fact]
        [Trait("Rule", "OR10")]
        public async Task EditLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);

            var waiterUserId = Guid.NewGuid();
            var waiterUserRoleLookup = CreateUserRoleLookup(waiterUserId, organizationId, Roles.Waiter);
            using var waiterContext = database.CreateContext(TestTenant.Of(organizationId));
            var waiterSut = new LocationService(waiterContext, waiterUserRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            var noRoleUserId = Guid.NewGuid();
            var noRoleUserRoleLookup = CreateUserRoleLookup(noRoleUserId, organizationId, role: null);
            using var noRoleContext = database.CreateContext(TestTenant.Of(organizationId));
            var noRoleSut = new LocationService(noRoleContext, noRoleUserRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var waiterAct = () => waiterSut.EditLocationAsync(waiterUserId, locationId, ValidEditRequest);
            var noRoleAct = () => noRoleSut.EditLocationAsync(noRoleUserId, locationId, ValidEditRequest);

            // Assert
            await waiterAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
            await noRoleAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task DeleteLocationAsync_WhenUserIsAdminOfOrganization_ClosesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = new LocationService(context, userRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            await sut.DeleteLocationAsync(userId, locationId);

            // Assert
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            var location = await verifyContext.Locations.FirstAsync(l => l.Id == locationId);
            location.Status.Should().Be(LocationStatus.Closed);
        }

        [Fact]
        [Trait("Rule", "OR9")]
        public async Task DeleteLocationAsync_WhenUserIsOwnerOfOrganization_ClosesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext(TestTenant.Of(organizationId));
            var sut = new LocationService(context, userRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            await sut.DeleteLocationAsync(userId, locationId);

            // Assert
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            var location = await verifyContext.Locations.FirstAsync(l => l.Id == locationId);
            location.Status.Should().Be(LocationStatus.Closed);
        }

        [Fact]
        [Trait("Rule", "OR10")]
        public async Task DeleteLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);

            var waiterUserId = Guid.NewGuid();
            var waiterUserRoleLookup = CreateUserRoleLookup(waiterUserId, organizationId, Roles.Waiter);
            using var waiterContext = database.CreateContext(TestTenant.Of(organizationId));
            var waiterSut = new LocationService(waiterContext, waiterUserRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            var noRoleUserId = Guid.NewGuid();
            var noRoleUserRoleLookup = CreateUserRoleLookup(noRoleUserId, organizationId, role: null);
            using var noRoleContext = database.CreateContext(TestTenant.Of(organizationId));
            var noRoleSut = new LocationService(noRoleContext, noRoleUserRoleLookup, TestTenant.Of(organizationId), TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var waiterAct = () => waiterSut.DeleteLocationAsync(waiterUserId, locationId);
            var noRoleAct = () => noRoleSut.DeleteLocationAsync(noRoleUserId, locationId);

            // Assert
            await waiterAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
            await noRoleAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        // Scenario 27
        [Theory]
        [InlineData(Roles.Manager)]
        [InlineData(Roles.Cashier)]
        [InlineData(Roles.Waiter)]
        [InlineData(Roles.Kitchen)]
        [Trait("Rule", "OR10")]
        public async Task CreateLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndCreatesNothing(string role)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = new LocationService(context, CreateUserRoleLookup(userId, organizationId, role), tenant, TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());

            // Act
            var act = () => sut.CreateLocationAsync(userId, ValidCreateRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403);
            using var verifyContext = database.CreateContext(tenant);
            (await verifyContext.Locations.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
        }
    }
}
