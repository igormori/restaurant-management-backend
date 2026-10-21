using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Roles;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Members;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.AuditLog;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// The access side of a new organization: its Owner and its five default roles (Admin, Manager,
    /// Cashier, Waiter, Kitchen) with the levels of the spec table, and the undo when creation fails.
    /// </summary>
    public class OrganizationAccessSetupTests
    {
        private static readonly string[] DefaultRoleNames = { "Admin", "Manager", "Cashier", "Waiter", "Kitchen" };

        [Fact]
        [Trait("Rule", "T32")]
        public async Task AddOwnerWithDefaultRolesAsync_NewOrganization_StoresTheOwnerAndTheFiveRolesWithTheDefaultLevels()
        {
            // Arrange
            using var database = new IdentityTestDatabase();
            using (var seed = database.CreateContext())
            {
                seed.Users.Add(new User { Id = RitaId, Email = "rita@exemplo.com.br", PasswordHash = "hash", FirstName = "Rita", LastName = "Souza", IsVerified = true });
                await seed.SaveChangesAsync();
            }

            // Act
            using (var context = database.CreateContext())
                await new OrganizationAccessSetup(context).AddOwnerWithDefaultRolesAsync(RitaId, CantinaId);

            // Assert
            using var readContext = database.CreateContext();
            var owner = readContext.Set<OrganizationMember>().Single(m => m.OrganizationId == CantinaId);
            owner.UserId.ShouldBe(RitaId, "the creator becomes a member of the new organization");
            owner.IsOwner.ShouldBeTrue("the creator is the Owner, a flag on the membership and not a role");
            var roles = readContext.Set<OrganizationRole>().Where(r => r.OrganizationId == CantinaId).ToList();
            roles.Select(r => r.Name).ShouldBe(DefaultRoleNames, ignoreOrder: true, customMessage: "a new organization gets Admin, Manager, Cashier, Waiter and Kitchen, and no Owner role");
            var stored = readContext.Set<OrganizationRoleLevel>().Where(l => l.OrganizationId == CantinaId).AsEnumerable()
                .GroupBy(l => roles.Single(r => r.Id == l.RoleId).Name)
                .ToDictionary(g => g.Key, g => Describe(g.Select(l => (l.Module, l.Level))));
            var expected = DefaultLevels.ToDictionary(d => d.Key, d => Describe(d.Value));
            stored.ShouldBe(expected, ignoreOrder: true, customMessage: "every role gets exactly the default levels of the spec table, and a level of None is not stored");
        }

        private static string Describe(IEnumerable<(AccessModule Module, AccessLevel Level)> levels)
            => string.Join(",", levels.OrderBy(l => l.Module).Select(l => $"{l.Module}:{l.Level}"));

        [Fact]
        [Trait("Rule", "T33")]
        public async Task RemoveOwnerAndRolesAsync_TwoOrganizations_RemovesOnlyTheGivenOrganizationsMembershipAndRoles()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(DefaultRoleNames);
            using (var context = database.CreateContext())
            {
                context.Set<OrganizationMember>().Add(TestMembers.Owner(RitaId, TrattoriaId));
                await context.SaveChangesAsync();
            }

            // Act
            using (var context = database.CreateContext())
                await new OrganizationAccessSetup(context).RemoveOwnerAndRolesAsync(AnaId, CantinaId);

            // Assert
            using var readContext = database.CreateContext();
            readContext.Set<OrganizationMember>().Count(m => m.OrganizationId == CantinaId && m.IsOwner).ShouldBe(0, "the Owner membership of the organization is removed");
            readContext.Set<OrganizationRole>().Count(r => r.OrganizationId == CantinaId).ShouldBe(0, "the roles of the organization are removed");
            readContext.Set<OrganizationRoleLevel>().Count(l => l.OrganizationId == CantinaId).ShouldBe(0, "the default levels of the removed roles go with them");
            readContext.Set<OrganizationMember>().Count(m => m.OrganizationId == TrattoriaId).ShouldBe(2, "another organization's memberships are never touched");
            readContext.Set<OrganizationRole>().Count(r => r.OrganizationId == TrattoriaId).ShouldBe(5, "another organization's roles are never touched");
        }

        [Fact]
        [Trait("Rule", "T34")]
        public async Task SaveChanges_RoleOfAnotherOrganizationThanTheCurrentOne_IsRefusedByTheAuditInterceptor()
        {
            // Arrange
            using var database = await CreateDatabaseAsync();
            var currentUser = Substitute.For<ICurrentUser>();
            currentUser.UserId.Returns(AnaId);
            currentUser.OrganizationId.Returns(CantinaId);
            var interceptor = new AuditSaveChangesInterceptor(currentUser, new TenantScope(), TimeProvider.System, TestLocalizer.Create());
            using var context = database.CreateContext(interceptor);
            context.Set<OrganizationRole>().Add(Role(TrattoriaId, "Waiter"));

            // Act
            var act = () => context.SaveChangesAsync();

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "a role cannot be written into an organization other than the current one");
            using var readContext = database.CreateContext();
            readContext.Set<OrganizationRole>().Count().ShouldBe(0, "the refused role is not stored");
        }
    }
}
