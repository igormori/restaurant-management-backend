using FluentAssertions;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Menu.Tests
{
    public class MenuCategoryServiceTests
    {
        private static CreateMenuCategoryRequest CreateRequest(Guid menuId, Guid organizationId) => new()
        {
            MenuId = menuId,
            OrganizationId = organizationId,
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
            IOrganizationLookup? organizationLookup = null)
        {
            return new MenuCategoryService(
                context,
                organizationLookup ?? Substitute.For<IOrganizationLookup>(),
                userRoleLookup,
                TestLocalizer.Create());
        }

        [Fact]
        public async Task CreateAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var act = () => sut.CreateAsync(userId, CreateRequest(menuId, organizationId));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task CreateAsync_WhenUserIsOwnerOrAdmin_CreatesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var response = await sut.CreateAsync(userId, CreateRequest(menuId, organizationId));

            // Assert
            response.Should().NotBeNull();
            response.OrganizationId.Should().Be(organizationId);
            response.Name.Should().Be("Starters");
        }

        [Fact]
        public async Task GetByIdAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var act = () => sut.GetByIdAsync(userId, categoryId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task GetByIdAsync_WhenUserIsOwnerOfOrganization_ReturnsCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var response = await sut.GetByIdAsync(userId, categoryId);

            // Assert
            response.Should().NotBeNull();
            response!.Id.Should().Be(categoryId);
        }

        [Fact]
        public async Task GetByOrganizationIdAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var act = () => sut.GetByOrganizationIdAsync(userId, organizationId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var act = () => sut.UpdateAsync(userId, categoryId, UpdateRequest);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task UpdateAsync_WhenUserIsAdminOfOrganization_UpdatesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var response = await sut.UpdateAsync(userId, categoryId, UpdateRequest);

            // Assert
            response.Should().NotBeNull();
            response!.Name.Should().Be(UpdateRequest.Name);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var act = () => sut.DeleteAsync(userId, categoryId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserIsOwnerOfOrganization_DeletesCategory()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var categoryId = await SeedCategoryAsync(database, menuId, organizationId);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var result = await sut.DeleteAsync(userId, categoryId);

            // Assert
            result.Should().BeTrue();
        }
    }
}
