using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

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
            PhoneNumber = "555-0100"
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
            Status = LocationStatus.Active
        };

        private static async Task<Guid> SeedOrganizationAsync(OrganizationTestDatabase database)
        {
            var organizationId = Guid.NewGuid();
            using var context = database.CreateContext();

            context.Organizations.Add(new Entities.Organization
            {
                Id = organizationId,
                Name = "Original Restaurant",
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
        public async Task CreateLocationAsync_WhenUserIsAdminOfOrganization_CreatesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            using var context = database.CreateContext();
            var sut = new LocationService(context, userRoleLookup, TestLocalizer.Create());

            // Act
            var response = await sut.CreateLocationAsync(userId, organizationId, ValidCreateRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidCreateRequest.Name);
        }

        [Fact]
        public async Task CreateLocationAsync_WhenUserIsOwnerOfOrganization_CreatesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext();
            var sut = new LocationService(context, userRoleLookup, TestLocalizer.Create());

            // Act
            var response = await sut.CreateLocationAsync(userId, organizationId, ValidCreateRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidCreateRequest.Name);
        }

        [Fact]
        public async Task CreateLocationAsync_WhenUserHasStaffRoleOrNoRole_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);

            var staffUserId = Guid.NewGuid();
            var staffUserRoleLookup = CreateUserRoleLookup(staffUserId, organizationId, Roles.Staff);
            using var staffContext = database.CreateContext();
            var staffSut = new LocationService(staffContext, staffUserRoleLookup, TestLocalizer.Create());

            var noRoleUserId = Guid.NewGuid();
            var noRoleUserRoleLookup = CreateUserRoleLookup(noRoleUserId, organizationId, role: null);
            using var noRoleContext = database.CreateContext();
            var noRoleSut = new LocationService(noRoleContext, noRoleUserRoleLookup, TestLocalizer.Create());

            // Act
            var staffAct = () => staffSut.CreateLocationAsync(staffUserId, organizationId, ValidCreateRequest);
            var noRoleAct = () => noRoleSut.CreateLocationAsync(noRoleUserId, organizationId, ValidCreateRequest);

            // Assert
            await staffAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
            await noRoleAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task EditLocationAsync_WhenUserIsAdminOfOrganization_EditsLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            using var context = database.CreateContext();
            var sut = new LocationService(context, userRoleLookup, TestLocalizer.Create());

            // Act
            var response = await sut.EditLocationAsync(userId, locationId, ValidEditRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidEditRequest.Name);
        }

        [Fact]
        public async Task EditLocationAsync_WhenUserIsOwnerOfOrganization_EditsLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext();
            var sut = new LocationService(context, userRoleLookup, TestLocalizer.Create());

            // Act
            var response = await sut.EditLocationAsync(userId, locationId, ValidEditRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidEditRequest.Name);
        }

        [Fact]
        public async Task EditLocationAsync_WhenUserHasStaffRoleOrNoRole_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);

            var staffUserId = Guid.NewGuid();
            var staffUserRoleLookup = CreateUserRoleLookup(staffUserId, organizationId, Roles.Staff);
            using var staffContext = database.CreateContext();
            var staffSut = new LocationService(staffContext, staffUserRoleLookup, TestLocalizer.Create());

            var noRoleUserId = Guid.NewGuid();
            var noRoleUserRoleLookup = CreateUserRoleLookup(noRoleUserId, organizationId, role: null);
            using var noRoleContext = database.CreateContext();
            var noRoleSut = new LocationService(noRoleContext, noRoleUserRoleLookup, TestLocalizer.Create());

            // Act
            var staffAct = () => staffSut.EditLocationAsync(staffUserId, locationId, ValidEditRequest);
            var noRoleAct = () => noRoleSut.EditLocationAsync(noRoleUserId, locationId, ValidEditRequest);

            // Assert
            await staffAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
            await noRoleAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task DeleteLocationAsync_WhenUserIsAdminOfOrganization_ClosesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            using var context = database.CreateContext();
            var sut = new LocationService(context, userRoleLookup, TestLocalizer.Create());

            // Act
            await sut.DeleteLocationAsync(userId, locationId);

            // Assert
            using var verifyContext = database.CreateContext();
            var location = await verifyContext.Locations.FirstAsync(l => l.Id == locationId);
            location.Status.Should().Be(LocationStatus.Closed);
        }

        [Fact]
        public async Task DeleteLocationAsync_WhenUserIsOwnerOfOrganization_ClosesLocation()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext();
            var sut = new LocationService(context, userRoleLookup, TestLocalizer.Create());

            // Act
            await sut.DeleteLocationAsync(userId, locationId);

            // Assert
            using var verifyContext = database.CreateContext();
            var location = await verifyContext.Locations.FirstAsync(l => l.Id == locationId);
            location.Status.Should().Be(LocationStatus.Closed);
        }

        [Fact]
        public async Task DeleteLocationAsync_WhenUserHasStaffRoleOrNoRole_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var locationId = await SeedLocationAsync(database, organizationId);

            var staffUserId = Guid.NewGuid();
            var staffUserRoleLookup = CreateUserRoleLookup(staffUserId, organizationId, Roles.Staff);
            using var staffContext = database.CreateContext();
            var staffSut = new LocationService(staffContext, staffUserRoleLookup, TestLocalizer.Create());

            var noRoleUserId = Guid.NewGuid();
            var noRoleUserRoleLookup = CreateUserRoleLookup(noRoleUserId, organizationId, role: null);
            using var noRoleContext = database.CreateContext();
            var noRoleSut = new LocationService(noRoleContext, noRoleUserRoleLookup, TestLocalizer.Create());

            // Act
            var staffAct = () => staffSut.DeleteLocationAsync(staffUserId, locationId);
            var noRoleAct = () => noRoleSut.DeleteLocationAsync(noRoleUserId, locationId);

            // Assert
            await staffAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
            await noRoleAct.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }
    }
}
