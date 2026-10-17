using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Menu.Tests
{
    public class MenuAuditTests
    {
        private const int ForbiddenStatusCode = 403;
        private const string TenantMismatchKey = "TenantMismatch";
        private const string OrganizationRequiredKey = "OrganizationRequired";

        private static readonly Guid OrgA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid OrgB = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid CarlosId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private static readonly Guid CentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");

        private static readonly DateTimeOffset CreationTime = new(2026, 3, 14, 18, 30, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset UpdateTime = new(2026, 3, 15, 11, 45, 0, TimeSpan.Zero);

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTimeOffset now) => _now = now;

            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static AuditSaveChangesInterceptor CreateInterceptor(DateTimeOffset now, Guid userId, Guid? organizationId)
        {
            var currentUser = Substitute.For<ICurrentUser>();
            currentUser.UserId.Returns(userId);
            currentUser.OrganizationId.Returns(organizationId);

            return new AuditSaveChangesInterceptor(currentUser, new TenantScope(), new FixedTimeProvider(now), TestLocalizer.Create());
        }

        private static async Task<Guid> SeedMenuAsync(MenuTestDatabase database, Guid organizationId)
        {
            using var seedContext = database.CreateContext();
            var menu = new Entities.Menu
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Cardápio de Jantar",
                CreatedAt = CreationTime.UtcDateTime,
                UpdatedAt = CreationTime.UtcDateTime,
                CreatedByUserId = MariaId,
                UpdatedByUserId = MariaId
            };
            seedContext.Menus.Add(menu);
            await seedContext.SaveChangesAsync();
            return menu.Id;
        }

        // Scenario 1
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_NewMenuByUserInOrgA_StampsOrganizationAndCreationAudit()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(CreationTime, MariaId, OrgA));
            var menu = new Entities.Menu { Name = "Cardápio de Almoço" };
            context.Menus.Add(menu);

            // Act
            await context.SaveChangesAsync();

            // Assert
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            var stored = await readContext.Menus.SingleAsync();
            stored.OrganizationId.ShouldBe(OrgA);
            stored.CreatedByUserId.ShouldBe(MariaId);
            stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
            stored.UpdatedAt.ShouldBe(CreationTime.UtcDateTime);
            stored.UpdatedByUserId.ShouldBe(MariaId);
        }

        // Scenario 2
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_UpdatedMenu_SetsUpdateAuditAndKeepsCreatedFields()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var menuId = await SeedMenuAsync(database, OrgA);
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(UpdateTime, CarlosId, OrgA));
            var menu = await context.Menus.SingleAsync(m => m.Id == menuId);
            menu.Name = "Cardápio de Jantar Atualizado";

            // Act
            await context.SaveChangesAsync();

            // Assert
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            var stored = await readContext.Menus.SingleAsync();
            stored.UpdatedAt.ShouldBe(UpdateTime.UtcDateTime);
            stored.UpdatedByUserId.ShouldBe(CarlosId);
            stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
            stored.CreatedByUserId.ShouldBe(MariaId);
        }

        // Scenario 3
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_NewMenuCarriesAnotherOrganizationId_Throws403AndWritesNothing()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(CreationTime, MariaId, OrgA));
            context.Menus.Add(new Entities.Menu { Name = "Cardápio de Almoço", OrganizationId = OrgB });

            // Act
            var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

            // Assert
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(TenantMismatchKey);
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            (await readContext.Menus.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
        }

        // Scenario 4
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_UserInOrgBUpdatesOrgAMenu_Throws403AndMenuUnchanged()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var menuId = await SeedMenuAsync(database, OrgA);
            using var context = database.CreateContext(TestTenant.Of(OrgB), CreateInterceptor(UpdateTime, CarlosId, OrgB));
            // The query filter hides the Org A menu from an Org B user, so the change arrives as a detached entity.
            context.Menus.Update(new Entities.Menu { Id = menuId, OrganizationId = OrgA, Name = "Cardápio Invadido" });

            // Act
            var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

            // Assert
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(TenantMismatchKey);
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            var stored = await readContext.Menus.SingleAsync();
            stored.Name.ShouldBe("Cardápio de Jantar");
            stored.UpdatedByUserId.ShouldBe(MariaId);
        }

        // Scenario 5
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_OrganizationIdChangedOnStoredMenu_Throws403AndMenuStaysInOrgA()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var menuId = await SeedMenuAsync(database, OrgA);
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(UpdateTime, MariaId, OrgA));
            var menu = await context.Menus.SingleAsync(m => m.Id == menuId);
            menu.OrganizationId = OrgB;

            // Act
            var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

            // Assert
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(TenantMismatchKey);
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            (await readContext.Menus.SingleAsync()).OrganizationId.ShouldBe(OrgA);
        }

        // Scenario 6
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_NewMenuWithoutCurrentOrganization_Throws403OrganizationRequired()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            using var context = database.CreateContext(TestTenant.None, CreateInterceptor(CreationTime, MariaId, organizationId: null));
            context.Menus.Add(new Entities.Menu { Name = "Cardápio de Almoço" });

            // Act
            var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

            // Assert
            thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
            thrown.Message.ShouldBe(OrganizationRequiredKey);
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            (await readContext.Menus.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
        }

        // Scenario 7
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_NewMenuCategory_StampsOrganizationAndUpdatedAt()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var menuId = await SeedMenuAsync(database, OrgA);
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(CreationTime, MariaId, OrgA));
            context.MenuCategories.Add(new MenuCategory { MenuId = menuId, Name = "Massas", SortOrder = 1 });

            // Act
            await context.SaveChangesAsync();

            // Assert
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            var stored = await readContext.MenuCategories.SingleAsync();
            stored.OrganizationId.ShouldBe(OrgA);
            stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
            stored.UpdatedAt.ShouldBe(CreationTime.UtcDateTime);
            stored.CreatedByUserId.ShouldBe(MariaId);
        }

        // Scenario 8
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_NewMenuCategoryWithoutLocation_KeepsLocationNull()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var menuId = await SeedMenuAsync(database, OrgA);
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(CreationTime, MariaId, OrgA));
            context.MenuCategories.Add(new MenuCategory { MenuId = menuId, Name = "Sobremesas", LocationId = null });

            // Act
            await context.SaveChangesAsync();

            // Assert
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            (await readContext.MenuCategories.SingleAsync()).LocationId.ShouldBeNull();
        }

        // Scenario 9
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_NewLocationMenu_GetsOrganizationFromUserAndUpdatedAt()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var menuId = await SeedMenuAsync(database, OrgA);
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(CreationTime, MariaId, OrgA));
            context.LocationMenus.Add(new LocationMenu { MenuId = menuId, LocationId = CentroLocationId });

            // Act
            await context.SaveChangesAsync();

            // Assert
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            var stored = await readContext.LocationMenus.SingleAsync();
            stored.OrganizationId.ShouldBe(OrgA);
            stored.UpdatedAt.ShouldBe(CreationTime.UtcDateTime);
            stored.CreatedByUserId.ShouldBe(MariaId);
        }

        // Scenario 10
        [Fact]
        [Trait("Rule", "M15")]
        public async Task SaveChangesAsync_UpdatedMenuCategory_SetsUpdatedAtAndKeepsCreatedAt()
        {
            // Arrange
            using var database = new MenuTestDatabase();
            var menuId = await SeedMenuAsync(database, OrgA);
            using (var seedContext = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(CreationTime, MariaId, OrgA)))
            {
                seedContext.MenuCategories.Add(new MenuCategory { MenuId = menuId, Name = "Massas", SortOrder = 1 });
                await seedContext.SaveChangesAsync();
            }
            using var context = database.CreateContext(TestTenant.Of(OrgA), CreateInterceptor(UpdateTime, CarlosId, OrgA));
            var category = await context.MenuCategories.SingleAsync();
            category.SortOrder = 2;

            // Act
            await context.SaveChangesAsync();

            // Assert
            using var readContext = database.CreateContext(TestTenant.Of(OrgA));
            var stored = await readContext.MenuCategories.SingleAsync();
            stored.UpdatedAt.ShouldBe(UpdateTime.UtcDateTime);
            stored.UpdatedByUserId.ShouldBe(CarlosId);
            stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
        }
    }
}
