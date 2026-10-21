using FluentAssertions;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.Tenancy;
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

        // Access is not what most tests here check: the guard allows everything unless the test says otherwise.
        private static IAccessGuard CreateGuard(Guid userId, Guid organizationId, string? role)
            => role is null
                ? TestAccess.Guard(userId, organizationId, TestAccess.NoAccessGrid, Guid.NewGuid())
                : TestAccess.Permissive();

        private static MenuCategoryService CreateSut(
            Data.MenuDbContext context,
            IAccessGuard accessGuard,
            ITenantContext tenantContext,
            IOrganizationLookup? organizationLookup = null)
        {
            return new MenuCategoryService(
                context,
                organizationLookup ?? Substitute.For<IOrganizationLookup>(),
                accessGuard,
                tenantContext,
                TestLocalizer.Create());
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task CreateAsync_MemberWithNoAccessYet_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.CreateAsync(userId, CreateRequest(menuId));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403, "a member who has been given no access yet may not create menu categories");
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
            var accessGuard = CreateGuard(userId, ownOrganizationId, "Owner");

            var tenant = TestTenant.Of(ownOrganizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.CreateAsync(userId, CreateRequest(foreignMenuId));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404, "a category cannot be added to another organization's menu; it answers as if the menu did not exist");
            using var readContext = database.CreateContext();
            readContext.MenuCategories.Count().Should().Be(0, "a refused category is not stored");
        }

        [Fact]
        [Trait("Rule", "M11")]
        public async Task CreateAsync_WhenMenuDoesNotExist_ThrowsNotFound()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Owner");

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.CreateAsync(userId, CreateRequest(Guid.NewGuid()));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404, "a category cannot be added to a menu that does not exist");
        }

        [Fact]
        [Trait("Rule", "M11")]
        public async Task CreateAsync_WhenLocationBelongsToAnotherOrganization_ThrowsNotFoundAndStoresNothing()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Owner");
            var foreignLocationId = Guid.NewGuid();
            var organizationLookup = Substitute.For<IOrganizationLookup>();
            organizationLookup.GetLocationAsync(foreignLocationId).Returns(
                new LocationSummaryDto { Id = foreignLocationId, OrganizationId = Guid.NewGuid() });

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant, organizationLookup);
            var request = CreateRequest(menuId);
            request.LocationId = foreignLocationId;

            // Act
            var act = () => sut.CreateAsync(userId, request);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404, "a category cannot use another organization's location; it answers as if the location did not exist");
            using var readContext = database.CreateContext();
            readContext.MenuCategories.Count().Should().Be(0, "a refused category is not stored");
        }

        [Fact]
        [Trait("Rule", "M11")]
        public async Task CreateAsync_WhenLocationDoesNotExist_ThrowsNotFoundAndStoresNothing()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Owner");

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);
            var request = CreateRequest(menuId);
            request.LocationId = Guid.NewGuid();

            // Act
            var act = () => sut.CreateAsync(userId, request);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 404, "a category cannot use a location that does not exist");
            using var readContext = database.CreateContext();
            readContext.MenuCategories.Count().Should().Be(0, "a refused category is not stored");
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task CreateAsync_WhenLocationBelongsToOrganization_StoresLocationId()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Owner");
            var locationId = Guid.NewGuid();
            var organizationLookup = Substitute.For<IOrganizationLookup>();
            organizationLookup.GetLocationAsync(locationId).Returns(
                new LocationSummaryDto { Id = locationId, OrganizationId = organizationId });

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant, organizationLookup);
            var request = CreateRequest(menuId);
            request.LocationId = locationId;

            // Act
            var response = await sut.CreateAsync(userId, request);

            // Assert
            response.LocationId.Should().Be(locationId, "a location of the same organization is kept on the category");
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task CreateAsync_WhenAccessIsAllowed_CreatesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Owner");

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var response = await sut.CreateAsync(userId, CreateRequest(menuId));

            // Assert
            response.Should().NotBeNull("a member who is allowed may create a category");
            response.OrganizationId.Should().Be(organizationId, "the new category belongs to the user's organization");
            response.Name.Should().Be("Starters");
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task GetByIdAsync_MemberWithNoAccessYet_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.GetByIdAsync(userId, categoryId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403, "a member who has been given no access yet may not read menu categories");
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task GetByIdAsync_WhenAccessIsAllowed_ReturnsCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Owner");

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var response = await sut.GetByIdAsync(userId, categoryId);

            // Assert
            response.Should().NotBeNull("a member who is allowed may read a category of the organization");
            response!.Id.Should().Be(categoryId);
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task GetAllAsync_MemberWithNoAccessYet_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.GetAllAsync(userId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403, "a member who has been given no access yet may not list menu categories");
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task UpdateAsync_MemberWithNoAccessYet_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.UpdateAsync(userId, categoryId, UpdateRequest);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403, "a member who has been given no access yet may not change menu categories");
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task UpdateAsync_WhenAccessIsAllowed_UpdatesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Admin");

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var response = await sut.UpdateAsync(userId, categoryId, UpdateRequest);

            // Assert
            response.Should().NotBeNull("a member who is allowed may change a category of the organization");
            response!.Name.Should().Be(UpdateRequest.Name, "the category shows the new name after the change");
        }

        [Fact]
        [Trait("Rule", "M5")]
        public async Task DeleteAsync_MemberWithNoAccessYet_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.DeleteAsync(userId, categoryId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403, "a member who has been given no access yet may not delete menu categories");
        }

        [Fact]
        [Trait("Rule", "M19")]
        public async Task DeleteAsync_WhenAccessIsAllowed_DeletesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var accessGuard = CreateGuard(userId, organizationId, "Owner");

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var result = await sut.DeleteAsync(userId, categoryId);

            // Assert
            result.Should().BeTrue("a member who is allowed may delete a category of the organization");
        }
    }
}
