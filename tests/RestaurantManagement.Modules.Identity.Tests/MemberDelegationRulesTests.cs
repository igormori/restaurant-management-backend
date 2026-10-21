using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Members;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Who may grant what at Cantina da Nonna (Batel and Mercado Municipal). Ana is the Owner; Maria is
    /// the caller and holds the stored levels each test gives her; Bruno is another member and the
    /// usual target.
    /// </summary>
    public class MemberDelegationRulesTests
    {
        private static readonly Guid BrunoId = Guid.Parse("55555555-eeee-4eee-8eee-00000000000e");
        private static readonly string[] AllRoles = { "Admin", "Manager", "Cashier", "Waiter", "Kitchen" };

        private static async Task<IdentityTestDatabase> CreateAsync()
        {
            var database = await CreateDatabaseAsync(AllRoles);
            using var context = database.CreateContext();
            context.Users.Add(new User
            {
                Id = BrunoId,
                Email = "bruno@cantinadanonna.com.br",
                PasswordHash = "hash",
                FirstName = "Bruno",
                LastName = "Souza",
                IsVerified = true
            });
            context.Set<OrganizationMember>().Add(TestMembers.Member(BrunoId, CantinaId));
            await context.SaveChangesAsync();
            return database;
        }

        private static async Task HoldAsync(IdentityTestDatabase database, Guid userId, Guid locationId, params (AccessModule Module, AccessLevel Level)[] levels)
        {
            using var context = database.CreateContext();
            var now = new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);
            foreach (var (module, level) in levels)
                context.Set<MemberLocationLevel>().Add(new MemberLocationLevel
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = CantinaId,
                    UserId = userId,
                    LocationId = locationId,
                    Module = module,
                    Level = level,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            await context.SaveChangesAsync();
        }

        private static MemberDelegationRules CreateRules(IdentityTestDatabase database, Guid callerId)
        {
            var currentUser = Substitute.For<ICurrentUser>();
            currentUser.UserId.Returns(callerId);
            currentUser.OrganizationId.Returns(CantinaId);
            return new MemberDelegationRules(database.CreateContext(), Tenant(CantinaId), currentUser, TestLocalizer.Create());
        }

        private static LocationGrant Grant(Guid locationId, params (AccessModule Module, AccessLevel Level)[] levels)
            => new(locationId, levels.ToDictionary(l => l.Module, l => l.Level));

        private static (AccessModule, AccessLevel)[] AdminLevelsExceptCashier => new[]
        {
            (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Manage),
            (AccessModule.Kitchen, AccessLevel.Manage), (AccessModule.Reports, AccessLevel.Manage), (AccessModule.Members, AccessLevel.Manage)
        };

        private static (AccessModule, AccessLevel)[] CashierPresetLevels => new[]
        {
            (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read),
            (AccessModule.Cashier, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read)
        };

        [Fact]
        [Trait("Rule", "T43")]
        public async Task RequireAsync_AdminWithoutCashierAtBatelGivesTheCashierPresetThere_Refuses403()
        {
            // Arrange
            using var database = await CreateAsync();
            await HoldAsync(database, MariaId, BatelId, AdminLevelsExceptCashier);
            var sut = CreateRules(database, MariaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.RequireAsync(BrunoId, new[] { Grant(BatelId, CashierPresetLevels) }));

            // Assert
            exception.StatusCode.ShouldBe(403, "nobody can grant a level above their own: Cashier Manage at Batel is more than Maria holds there");
            exception.Message.ShouldBe("CannotGrantAboveOwnLevel", "the refusal names the cap, not a generic denial");
        }

        [Fact]
        [Trait("Rule", "T44")]
        public async Task RequireAsync_AdminWithReportsReadRaisesReportsToManage_Refuses403()
        {
            // Arrange
            using var database = await CreateAsync();
            await HoldAsync(database, MariaId, BatelId, (AccessModule.Members, AccessLevel.Manage), (AccessModule.Reports, AccessLevel.Read));
            var sut = CreateRules(database, MariaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(BrunoId, new[] { Grant(BatelId, (AccessModule.Reports, AccessLevel.Manage)) }));

            // Assert
            exception.StatusCode.ShouldBe(403, "Read in Reports can give Read, never Manage");
        }

        [Fact]
        [Trait("Rule", "T45")]
        public async Task RequireAsync_MembersManageAtBatelOnlyChangesMercadoToo_Refuses403()
        {
            // Arrange
            using var database = await CreateAsync();
            await HoldAsync(database, MariaId, BatelId, (AccessModule.Members, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage));
            await HoldAsync(database, MariaId, MercadoId, (AccessModule.Menus, AccessLevel.Manage));
            var sut = CreateRules(database, MariaId);
            var grants = new[] { Grant(BatelId, (AccessModule.Menus, AccessLevel.Read)), Grant(MercadoId, (AccessModule.Menus, AccessLevel.Read)) };

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.RequireAsync(BrunoId, grants));

            // Assert
            exception.StatusCode.ShouldBe(403, "Members = Manage is needed at every location involved, and Maria has none at Mercado");
        }

        [Fact]
        [Trait("Rule", "T46")]
        public async Task RequireAsync_CallerChangesHerOwnAccessWithManageEverywhere_Refuses403()
        {
            // Arrange
            using var database = await CreateAsync();
            await HoldAsync(database, MariaId, BatelId, AdminLevelsExceptCashier);
            var sut = CreateRules(database, MariaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(MariaId, new[] { Grant(BatelId, (AccessModule.Menus, AccessLevel.Read)) }));

            // Assert
            exception.StatusCode.ShouldBe(403, "nobody can change their own grid, even with Manage everywhere");
            exception.Message.ShouldBe("CannotChangeOwnAccess");
        }

        [Fact]
        [Trait("Rule", "T47")]
        public async Task RequireAsync_TargetIsTheOwner_Refuses403()
        {
            // Arrange
            using var database = await CreateAsync();
            await HoldAsync(database, MariaId, BatelId, AdminLevelsExceptCashier);
            var sut = CreateRules(database, MariaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(AnaId, new[] { Grant(BatelId, (AccessModule.Menus, AccessLevel.Read)) }));

            // Assert
            exception.StatusCode.ShouldBe(403, "the Owner's access can never be edited");
        }

        [Fact]
        [Trait("Rule", "T48")]
        public async Task RequireAsync_OwnerGivesFullAdminLevelsAtBatel_Passes()
        {
            // Arrange
            using var database = await CreateAsync();
            var sut = CreateRules(database, AnaId);
            var admin = DefaultLevels["Admin"].Select(l => (l.Module, l.Level)).ToArray();

            // Act
            var act = () => sut.RequireAsync(BrunoId, new[] { Grant(BatelId, admin) });

            // Assert
            await Should.NotThrowAsync(act, "the Owner holds no stored cell and has no cap, so every level can be granted");
        }

        [Fact]
        [Trait("Rule", "T50")]
        public async Task RequireAsync_AdminGivesLevelsWithinHerOwnAtBatel_Passes()
        {
            // Arrange
            using var database = await CreateAsync();
            await HoldAsync(database, MariaId, BatelId, DefaultLevels["Admin"].Select(l => (l.Module, l.Level)).ToArray());
            var sut = CreateRules(database, MariaId);

            // Act
            var act = () => sut.RequireAsync(BrunoId, new[] { Grant(BatelId, CashierPresetLevels) });

            // Assert
            await Should.NotThrowAsync(act, "an Admin with Members = Manage may grant any level up to her own at that location");
        }

        [Theory]
        [InlineData("Manager")]
        [InlineData("Cashier")]
        [InlineData("Waiter")]
        [InlineData("Kitchen")]
        [Trait("Rule", "T49")]
        public async Task RequireAsync_CallerWithADefaultRoleThatLacksMembersManage_Refuses403(string role)
        {
            // Arrange
            using var database = await CreateAsync();
            await HoldAsync(database, MariaId, BatelId, DefaultLevels[role].Select(l => (l.Module, l.Level)).ToArray());
            var sut = CreateRules(database, MariaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(BrunoId, new[] { Grant(BatelId, (AccessModule.Menus, AccessLevel.Read)) }));

            // Assert
            exception.StatusCode.ShouldBe(403, $"a {role} has no Members = Manage, so she cannot edit anyone's access");
        }
    }
}
