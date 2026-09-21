using FluentAssertions;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Menu.Tests
{
    public class MenuServiceTests
    {
        private static CreateMenuRequest CreateRequest(Guid organizationId) => new()
        {
            OrganizationId = organizationId,
            Name = "Main Menu",
            Description = "Everyday menu"
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

        private static IUserRoleLookup CreateUserRoleLookup(Guid userId, Guid organizationId, string? role)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.GetRoleAsync(userId, organizationId).Returns(role);
            return userRoleLookup;
        }

        private static MenuService CreateSut(
            Data.MenuDbContext context,
            IUserRoleLookup userRoleLookup,
            IOrganizationLookup? organizationLookup = null)
        {
            return new MenuService(
                context,
                organizationLookup ?? Substitute.For<IOrganizationLookup>(),
                userRoleLookup,
                TestLocalizer.Create());
        }

        [Fact]
        public async Task GetMenusByOrganizationAsync_WhenUserHasStaffRole_ReturnsMenus()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Staff);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var response = await sut.GetMenusByOrganizationAsync(userId, organizationId);

            // Assert
            response.Should().ContainSingle(m => m.Id == menuId);
        }

        [Fact]
        public async Task GetMenusByLocationAsync_WhenUserHasStaffRole_ReturnsMenus()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (menuId, organizationId) = await SeedMenuAsync(database);
            var locationId = Guid.NewGuid();

            using (var context = database.CreateContext())
            {
                context.LocationMenus.Add(new Entities.LocationMenu
                {
                    Id = Guid.NewGuid(),
                    MenuId = menuId,
                    LocationId = locationId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Staff);
            var organizationLookup = Substitute.For<IOrganizationLookup>();
            organizationLookup.GetLocationAsync(locationId).Returns(new LocationSummaryDto
            {
                Id = locationId,
                OrganizationId = organizationId
            });

            using var sutContext = database.CreateContext();
            var sut = CreateSut(sutContext, userRoleLookup, organizationLookup);

            // Act
            var response = await sut.GetMenusByLocationAsync(userId, locationId);

            // Assert
            response.Should().ContainSingle(m => m.Id == menuId);
        }

        [Fact]
        public async Task GetMenusByOrganizationAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (_, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var act = () => sut.GetMenusByOrganizationAsync(userId, organizationId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task CreateMenuAsync_WhenUserHasStaffRole_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var organizationId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Staff);

            using var context = database.CreateContext();
            var sut = CreateSut(context, userRoleLookup);

            // Act
            var act = () => sut.CreateMenuAsync(userId, CreateRequest(organizationId));

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }
    }
}
