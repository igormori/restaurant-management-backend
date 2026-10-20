using RestaurantManagement.Shared.Auth;
using FluentAssertions;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared.Data;
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

        private static MenuService CreateSut(
            Data.MenuDbContext context,
            IAccessGuard accessGuard,
            ITenantContext tenantContext,
            IOrganizationLookup? organizationLookup = null)
        {
            return new MenuService(
                context,
                organizationLookup ?? Substitute.For<IOrganizationLookup>(),
                accessGuard,
                tenantContext,
                TestLocalizer.Create());
        }

        [Fact]
        [Trait("Rule", "M4")]
        public async Task GetMenusAsync_MemberWithNoAccessYet_ThrowsForbidden()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var (_, organizationId) = await SeedMenuAsync(database);
            var userId = Guid.NewGuid();
            var accessGuard = TestAccess.Guard(userId, organizationId, TestAccess.NoAccessGrid, Guid.NewGuid());

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = CreateSut(context, accessGuard, tenant);

            // Act
            var act = () => sut.GetMenusAsync(userId);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403, "a member who has been given no access yet may not read the organization's menus");
        }
    }
}
