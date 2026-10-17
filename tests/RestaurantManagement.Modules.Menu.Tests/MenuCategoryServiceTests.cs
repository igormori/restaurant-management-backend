using FluentAssertions;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Menu.Tests
{
    public class MenuCategoryServiceTests
    {
        private static CreateMenuCategoryRequest CreateRequest(Guid menuId) => new()
        {
            MenuId = menuId,
            Name = "Starters",
            Description = "Starter dishes",
            SortOrder = 1
        };

        private static UpdateMenuCategoryRequest UpdateRequest => new()
        {
            Name = "Updated Category",
            Description = "Updated description",
            SortOrder = 2
        };

        private static async Task<(Guid MenuId, Guid OrganizationId)> SeedMenuAsync(MenuTestDatabase database)
        {
            var organizationId = Guid.NewGuid();
            var menuId = Guid.NewGuid();
            using var context = database.CreateContext();

            context.Menus.Add(new Entities.Menu
            {
                Id = menuId,
                OrganizationId = organizationId,
                Name = "Main Menu",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            return (menuId, organizationId);
        }

        private static async Task<Guid> SeedCategoryAsync(MenuTestDatabase database, Guid menuId, Guid organizationId)
        {
            var categoryId = Guid.NewGuid();
            using var context = database.CreateContext();

            context.MenuCategories.Add(new MenuCategory
            {
                Id = categoryId,
                MenuId = menuId,
                OrganizationId = organizationId,
                Name = "Starters",
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            return categoryId;
        }

        private static IUserRoleLookup CreateUserRoleLookup(Guid userId, Guid organizationId, string? role)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.GetRoleAsync(userId, organizationId).Returns(role);
            return userRoleLookup;
        }

        private static MenuCategoryService CreateSut(
            Data.MenuDbContext context,
            IUserRoleLookup userRoleLookup,
            ITenantContext tenantContext,
            IOrganizationLookup? organizationLookup = null)
        {
            return new MenuCategoryService(
                context,
                organizationLookup ?? Substitute.For<IOrganizationLookup>(),
                userRoleLookup,
                tenantContext,
                TestLocalizer.Create());
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task CreateAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.CreateAsync(userId, CreateRequest(menuId));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "M11")]
        public async Task CreateAsync_WhenMenuBelongsToAnotherOrganization_ThrowsNotFoundAndStoresNothing()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (foreignMenuId, _) = await SeedMenuAsync(database);
            var ownOrganizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, ownOrganizationId, Roles.Owner);

            var tenant = TestTenant.Of(ownOrganizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.CreateAsync(userId, CreateRequest(foreignMenuId));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404);
            using var readContext = database.CreateContext();
            readContext.MenuCategories.Count().Should().Be(0);
        }

        [Fact]
        [Trait("Rule", "M11")]
        public async Task CreateAsync_WhenMenuDoesNotExist_ThrowsNotFound()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.CreateAsync(userId, CreateRequest(Guid.NewGuid()));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404);
        }

        [Fact]
        [Trait("Rule", "M11")]
        public async Task CreateAsync_WhenLocationBelongsToAnotherOrganization_ThrowsNotFoundAndStoresNothing()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);
            var foreignLocationId = Guid.NewGuid();
            var organizationLookup = Substitute.For<IOrganizationLookup>();
            organizationLookup.GetLocationAsync(foreignLocationId).Returns(
                new LocationSummaryDto { Id = foreignLocationId, OrganizationId = Guid.NewGuid() });

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant, organizationLookup);
            var request = CreateRequest(menuId);
            request.LocationId = foreignLocationId;

            // Act
            var act = () => sut.CreateAsync(userId, request);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404);
            using var readContext = database.CreateContext();
            readContext.MenuCategories.Count().Should().Be(0);
        }

        [Fact]
        [Trait("Rule", "M11")]
        public async Task CreateAsync_WhenLocationDoesNotExist_ThrowsNotFoundAndStoresNothing()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);
            var request = CreateRequest(menuId);
            request.LocationId = Guid.NewGuid();

            // Act
            var act = () => sut.CreateAsync(userId, request);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404);
            using var readContext = database.CreateContext();
            readContext.MenuCategories.Count().Should().Be(0);
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task CreateAsync_WhenLocationBelongsToOrganization_StoresLocationId()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);
            var locationId = Guid.NewGuid();
            var organizationLookup = Substitute.For<IOrganizationLookup>();
            organizationLookup.GetLocationAsync(locationId).Returns(
                new LocationSummaryDto { Id = locationId, OrganizationId = organizationId });

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant, organizationLookup);
            var request = CreateRequest(menuId);
            request.LocationId = locationId;

            // Act
            var response = await sut.CreateAsync(userId, request);

            // Assert
            response.LocationId.Should().Be(locationId);
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task CreateAsync_WhenUserIsOwnerOrAdmin_CreatesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var response = await sut.CreateAsync(userId, CreateRequest(menuId));

            // Assert
            response.Should().NotBeNull();
            response.OrganizationId.Should().Be(organizationId);
            response.Name.Should().Be("Starters");
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task GetByIdAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.GetByIdAsync(userId, categoryId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task GetByIdAsync_WhenUserIsOwnerOfOrganization_ReturnsCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var response = await sut.GetByIdAsync(userId, categoryId);

            // Assert
            response.Should().NotBeNull();
            response!.Id.Should().Be(categoryId);
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task GetAllAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.GetAllAsync(userId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task UpdateAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.UpdateAsync(userId, categoryId, UpdateRequest);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task UpdateAsync_WhenUserIsAdminOfOrganization_UpdatesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var response = await sut.UpdateAsync(userId, categoryId, UpdateRequest);

            // Assert
            response.Should().NotBeNull();
            response!.Name.Should().Be(UpdateRequest.Name);
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task DeleteAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.DeleteAsync(userId, categoryId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task DeleteAsync_WhenUserIsOwnerOfOrganization_DeletesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var result = await sut.DeleteAsync(userId, categoryId);

            // Assert
            result.Should().BeTrue();
        }
    }
}
