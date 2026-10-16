using FluentAssertions;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Menu.Tests
{
    public class MenuServiceTests
    {
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
            ITenantContext tenantContext,
            IOrganizationLookup? organizationLookup = null)
        {
            return new MenuService(
                context,
                organizationLookup ?? Substitute.For<IOrganizationLookup>(),
                userRoleLookup,
                tenantContext,
                TestLocalizer.Create());
        }

        [Fact]
        public async Task GetMenusAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (_, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, userRoleLookup, tenant);

            // Act
            var act = () => sut.GetMenusAsync(userId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }
    }
}
