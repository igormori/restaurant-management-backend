using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Members;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// What Identity answers to "what may this user do": the Owner flag and the stored cells of one
    /// organization, read from storage on every call.
    /// </summary>
    public class MemberAccessLookupTests
    {
        private static MemberLocationLevel Cell(Guid userId, Guid organizationId, Guid locationId, AccessModule module, AccessLevel level) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            LocationId = locationId,
            Module = module,
            Level = level,
            CreatedAt = new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc)
        };

        [Fact]
        [Trait("Rule", "T35")]
        public async Task GetAccessGridAsync_MemberOfTwoOrganizations_ReturnsOnlyTheAskedOrganizationsCells()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            using (var context = database.CreateContext())
            {
                context.Set<OrganizationMember>().Add(TestMembers.Member(MariaId, TrattoriaId));
                context.Set<MemberLocationLevel>().AddRange(
                    Cell(MariaId, CantinaId, BatelId, AccessModule.Menus, AccessLevel.Manage),
                    Cell(MariaId, CantinaId, MercadoId, AccessModule.Menus, AccessLevel.Read),
                    Cell(MariaId, TrattoriaId, TrattoriaCentroId, AccessModule.Reports, AccessLevel.Manage));
                await context.SaveChangesAsync();
            }
            using var readContext = database.CreateContext();
            var sut = new MemberAccessLookup(readContext);

            // Act
            var grid = await sut.GetAccessGridAsync(MariaId, CantinaId);

            // Assert
            grid.IsOwner.ShouldBeFalse("a member without the Owner flag is not the Owner");
            grid.Cells.Select(c => (c.LocationId, c.Module, c.Level)).ShouldBe(
                new[] { (BatelId, AccessModule.Menus, AccessLevel.Manage), (MercadoId, AccessModule.Menus, AccessLevel.Read) }, ignoreOrder: true,
                customMessage: "the grid holds only the asked organization's cells, never the other organization's");
        }

        [Theory]
        [InlineData("Ana", true)]
        [InlineData("Maria", false)]
        [InlineData("Rita", false)]
        [Trait("Rule", "T35")]
        public async Task GetAccessGridAsync_OwnerNewMemberAndNonMember_OnlyTheOwnerHasTheFlagAndNobodyHasCells(string person, bool expectedOwner)
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var userId = person switch { "Ana" => AnaId, "Maria" => MariaId, _ => RitaId };
            using var context = database.CreateContext();
            var sut = new MemberAccessLookup(context);

            // Act
            var grid = await sut.GetAccessGridAsync(userId, CantinaId);

            // Assert
            grid.IsOwner.ShouldBe(expectedOwner, "only the Owner membership carries the Owner flag; a new member and a non-member do not");
            grid.Cells.ShouldBeEmpty("a new member has no access until it is granted, and a non-member never has any");
        }

        [Fact]
        [Trait("Rule", "T36")]
        public async Task GetOrganizationIdsForUserAsync_UserInTwoOrganizations_ReturnsThoseTwoOnly()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            using (var context = database.CreateContext())
            {
                context.Set<OrganizationMember>().Add(TestMembers.Member(MariaId, TrattoriaId));
                await context.SaveChangesAsync();
            }
            using var readContext = database.CreateContext();
            var sut = new MemberAccessLookup(readContext);

            // Act
            var organizationIds = await sut.GetOrganizationIdsForUserAsync(MariaId);

            // Assert
            organizationIds.ShouldBe(new[] { CantinaId, TrattoriaId }, ignoreOrder: true, customMessage: "the user's organizations are the ones they have a membership in, and nobody else's");
        }
    }
}
