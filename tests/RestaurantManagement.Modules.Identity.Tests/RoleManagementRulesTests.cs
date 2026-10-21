using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Services.Roles;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Who may create, edit or rename a role at Cantina da Nonna (Batel and Mercado Municipal). Ana is the Owner;
    /// Maria is the caller and holds the stored levels each test gives her. A role has no location, so the caller's
    /// level in a module is her lowest level over the non-closed locations.
    /// </summary>
    public class RoleManagementRulesTests
    {
        private static readonly string[] AllRoles = { "Admin", "Manager", "Cashier", "Waiter", "Kitchen" };
        private static readonly IReadOnlyDictionary<AccessModule, AccessLevel> NoLevels = new Dictionary<AccessModule, AccessLevel>();

        private static IReadOnlyDictionary<AccessModule, AccessLevel> Levels(params (AccessModule Module, AccessLevel Level)[] levels)
            => levels.ToDictionary(l => l.Module, l => l.Level);

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

        private static RoleManagementRules CreateRules(IdentityTestDatabase database, Guid callerId, params Guid[] nonClosedLocations)
        {
            var currentUser = Substitute.For<ICurrentUser>();
            currentUser.UserId.Returns(callerId);
            currentUser.OrganizationId.Returns(CantinaId);
            var lookup = Substitute.For<IOrganizationLookup>();
            lookup.GetNonClosedLocationIdsAsync(CantinaId).Returns(nonClosedLocations.ToList());
            return new RoleManagementRules(database.CreateContext(), Tenant(CantinaId), currentUser, lookup, TestLocalizer.Create());
        }

        private static (AccessModule, AccessLevel)[] MembersAndMenusManage => new[]
        {
            (AccessModule.Members, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Manage)
        };

        [Fact]
        [Trait("Rule", "T91")]
        public async Task RequireAsync_OwnerProposesManageInEveryModule_Passes()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            var sut = CreateRules(database, AnaId, BatelId, MercadoId);
            var everythingManage = Enum.GetValues<AccessModule>().ToDictionary(m => m, _ => AccessLevel.Manage);

            // Act
            var act = () => sut.RequireAsync(NoLevels, everythingManage);

            // Assert
            await Should.NotThrowAsync(act, "the Owner has no stored cells and no cap, so she may set any level");
        }

        [Fact]
        [Trait("Rule", "T92")]
        public async Task RequireAsync_AdminWithMembersManageAtBothLocationsProposesLevelsAtOrBelowHers_Passes()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, MembersAndMenusManage);
            await HoldAsync(database, MariaId, MercadoId, MembersAndMenusManage);
            var sut = CreateRules(database, MariaId, BatelId, MercadoId);

            // Act
            var act = () => sut.RequireAsync(NoLevels, Levels((AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read)));

            // Assert
            await Should.NotThrowAsync(act, "an Admin may create a role at or below her own level in every module");
        }

        [Fact]
        [Trait("Rule", "T93")]
        public async Task RequireAsync_AdminWithoutCashierProposesCashierManage_Refuses403AboveOwnLevel()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, MembersAndMenusManage);
            await HoldAsync(database, MariaId, MercadoId, MembersAndMenusManage);
            var sut = CreateRules(database, MariaId, BatelId, MercadoId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(NoLevels, Levels((AccessModule.Menus, AccessLevel.Read), (AccessModule.Cashier, AccessLevel.Manage))));

            // Assert
            exception.StatusCode.ShouldBe(403, "an Admin cannot set a default level above her own: Cashier Manage is more than the nothing she holds");
            exception.Message.ShouldBe("CannotGrantAboveOwnLevel", "the refusal names the cap, not a generic denial");
        }

        [Fact]
        [Trait("Rule", "T94")]
        public async Task RequireAsync_AdminWithoutCashierRenamesARoleHoldingCashierManage_Refuses403AboveOwnLevel()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, MembersAndMenusManage);
            await HoldAsync(database, MariaId, MercadoId, MembersAndMenusManage);
            var sut = CreateRules(database, MariaId, BatelId, MercadoId);
            var cashierRoleLevels = Levels((AccessModule.Cashier, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read));

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.RequireAsync(cashierRoleLevels, NoLevels));

            // Assert
            exception.StatusCode.ShouldBe(403, "a role already above the Admin's cap cannot be edited or renamed by her");
            exception.Message.ShouldBe("CannotGrantAboveOwnLevel", "the current levels of the role count, even when the request proposes none");
        }

        [Fact]
        [Trait("Rule", "T95")]
        public async Task RequireAsync_MembersManageAtBatelOnlyWhileMercadoIsOpen_Refuses403AccessDenied()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, MembersAndMenusManage);
            var sut = CreateRules(database, MariaId, BatelId, MercadoId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(NoLevels, Levels((AccessModule.Menus, AccessLevel.Read))));

            // Assert
            exception.StatusCode.ShouldBe(403, "roles belong to the whole organization, so Members = Manage is needed at every non-closed location");
            exception.Message.ShouldBe("AccessDenied", "a missing Members level is a plain denial");
        }

        [Fact]
        [Trait("Rule", "T96")]
        public async Task RequireAsync_MercadoIsClosedAndAdminHoldsNothingThere_TheMissingCellDoesNotLowerHerCap()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, MembersAndMenusManage);
            var sut = CreateRules(database, MariaId, BatelId);

            // Act
            var act = () => sut.RequireAsync(NoLevels, Levels((AccessModule.Menus, AccessLevel.Manage)));

            // Assert
            await Should.NotThrowAsync(act, "a closed location is left out, so her levels at Batel alone decide");
        }

        [Theory]
        [InlineData("Manager")]
        [InlineData("Cashier")]
        [InlineData("Waiter")]
        [InlineData("Kitchen")]
        [Trait("Rule", "T97")]
        public async Task RequireAsync_CallerWithADefaultRoleThatLacksMembersManage_Refuses403AccessDenied(string role)
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, DefaultLevels[role]);
            await HoldAsync(database, MariaId, MercadoId, DefaultLevels[role]);
            var sut = CreateRules(database, MariaId, BatelId, MercadoId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(NoLevels, Levels((AccessModule.Locations, AccessLevel.Read))));

            // Assert
            exception.StatusCode.ShouldBe(403, $"a {role} has no Members = Manage, so she cannot manage roles");
            exception.Message.ShouldBe("AccessDenied");
        }

        [Fact]
        [Trait("Rule", "T98")]
        public async Task RequireAsync_NoNonClosedLocationInTheOrganization_RefusesANonOwner()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, MembersAndMenusManage);
            var sut = CreateRules(database, MariaId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(NoLevels, Levels((AccessModule.Menus, AccessLevel.Read))));

            // Assert
            exception.StatusCode.ShouldBe(403, "with every location closed there is no level to compare, so only the Owner may manage roles");
        }

        [Fact]
        [Trait("Rule", "T99")]
        public async Task RequireAsync_CashierManageAtBatelButReadAtMercado_RefusesCashierManage()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(AllRoles);
            await HoldAsync(database, MariaId, BatelId, (AccessModule.Members, AccessLevel.Manage), (AccessModule.Cashier, AccessLevel.Manage));
            await HoldAsync(database, MariaId, MercadoId, (AccessModule.Members, AccessLevel.Manage), (AccessModule.Cashier, AccessLevel.Read));
            var sut = CreateRules(database, MariaId, BatelId, MercadoId);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(
                () => sut.RequireAsync(NoLevels, Levels((AccessModule.Cashier, AccessLevel.Manage))));

            // Assert
            exception.StatusCode.ShouldBe(403, "her level in a module is the lowest over the non-closed locations: Cashier is only Read at Mercado");
            exception.Message.ShouldBe("CannotGrantAboveOwnLevel");
        }
    }
}
