using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Identity;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationCreationScopeTests
    {
        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid ExistingOrgA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");

        private static readonly DateTimeOffset CreationTime = new(2026, 3, 14, 18, 30, 0, TimeSpan.Zero);

        private static CreateOrganizationRequest CantinaDaNonna => new() { Name = "Cantina da Nonna", Currency = "BRL" };

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTimeOffset now) => _now = now;

            public override DateTimeOffset GetUtcNow() => _now;
        }

        private sealed class Harness
        {
            public TenantScope TenantScope { get; } = new();
            public IUserRoleAssigner RoleAssigner { get; } = Substitute.For<IUserRoleAssigner>();
            public IUserRoleLookup RoleLookup { get; } = Substitute.For<IUserRoleLookup>();
            public AuditSaveChangesInterceptor Interceptor { get; }
            public ITenantContext Tenant { get; }

            public Harness(Guid? currentUserOrganizationId)
            {
                var currentUser = Substitute.For<ICurrentUser>();
                currentUser.UserId.Returns(MariaId);
                currentUser.OrganizationId.Returns(currentUserOrganizationId);

                RoleLookup.UserExistsAsync(MariaId).Returns(true);
                RoleLookup.GetOrganizationIdsForUserAsync(MariaId).Returns(new List<Guid>());

                Interceptor = new AuditSaveChangesInterceptor(
                    currentUser, TenantScope, new FixedTimeProvider(CreationTime), TestLocalizer.Create());
                Tenant = new TenantContext(TenantScope, currentUser, new SelectedLocation());
            }

            public OrganizationService CreateService(OrganizationDbContext context) => new(
                context, RoleLookup, RoleAssigner, TenantScope, Tenant, TestLocalizer.Create(),
                Substitute.For<ILogger<OrganizationService>>(), Substitute.For<IAuditLog>());
        }

        // Scenario 1
        [Fact]
        public async Task CreateOrganizationAsync_UserHasNoOrganization_SettingsGetNewOrganizationId()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var harness = new Harness(currentUserOrganizationId: null);
            using var context = database.CreateContext(harness.Tenant, harness.Interceptor);
            var sut = harness.CreateService(context);

            // Act
            var response = await sut.CreateOrganizationAsync(MariaId, CantinaDaNonna);

            // Assert
            using var readContext = database.CreateContext();
            var settings = await readContext.OrganizationSettings.IgnoreQueryFilters().SingleAsync();
            settings.OrganizationId.ShouldBe(response.Id);
        }

        // Scenario 2
        [Fact]
        public async Task CreateOrganizationAsync_UserBelongsToAnotherOrganization_SettingsGetNewOrganizationNotUsers()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var harness = new Harness(currentUserOrganizationId: ExistingOrgA);
            using var context = database.CreateContext(harness.Tenant, harness.Interceptor);
            var sut = harness.CreateService(context);

            // Act
            var response = await sut.CreateOrganizationAsync(MariaId, CantinaDaNonna);

            // Assert
            using var readContext = database.CreateContext();
            var settings = await readContext.OrganizationSettings.IgnoreQueryFilters().SingleAsync();
            settings.OrganizationId.ShouldBe(response.Id);
            settings.OrganizationId.ShouldNotBe(ExistingOrgA);
        }

        // Scenario 3
        [Fact]
        public async Task CreateOrganizationAsync_SettingsSaved_CarryAuditFieldsOfCreator()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var harness = new Harness(currentUserOrganizationId: null);
            using var context = database.CreateContext(harness.Tenant, harness.Interceptor);
            var sut = harness.CreateService(context);

            // Act
            var response = await sut.CreateOrganizationAsync(MariaId, CantinaDaNonna);

            // Assert
            using var readContext = database.CreateContext();
            var organization = await readContext.Organizations.SingleAsync(o => o.Id == response.Id);
            var settings = await readContext.OrganizationSettings.IgnoreQueryFilters().SingleAsync();
            organization.CreatedByUserId.ShouldBe(MariaId);
            organization.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
            settings.CreatedByUserId.ShouldBe(MariaId);
            settings.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
        }

        // Scenario 4
        [Fact]
        public async Task CreateOrganizationAsync_OwnerRoleAssigned_AssignedWithNewOrganizationIdInsideScope()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var harness = new Harness(currentUserOrganizationId: null);
            Guid? scopeDuringAssignment = null;
            harness.RoleAssigner
                .AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(_ =>
                {
                    scopeDuringAssignment = harness.TenantScope.OrganizationId;
                    return Task.CompletedTask;
                });
            using var context = database.CreateContext(harness.Tenant, harness.Interceptor);
            var sut = harness.CreateService(context);

            // Act
            var response = await sut.CreateOrganizationAsync(MariaId, CantinaDaNonna);

            // Assert
            await harness.RoleAssigner.Received(1).AssignRoleAsync(MariaId, response.Id, Roles.Owner);
            scopeDuringAssignment.ShouldBe(response.Id);
        }

        // Scenario 5
        [Fact]
        public async Task CreateOrganizationAsync_Completed_ScopeIsClosed()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var harness = new Harness(currentUserOrganizationId: null);
            using var context = database.CreateContext(harness.Tenant, harness.Interceptor);
            var sut = harness.CreateService(context);

            // Act
            await sut.CreateOrganizationAsync(MariaId, CantinaDaNonna);

            // Assert
            harness.TenantScope.OrganizationId.ShouldBeNull();
        }

        // Scenario 6
        [Fact]
        public async Task CreateOrganizationAsync_RoleAssignmentFails_ScopeIsClosedAndNothingPersisted()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var harness = new Harness(currentUserOrganizationId: null);
            harness.RoleAssigner
                .AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("Role assignment unavailable")));
            using var context = database.CreateContext(harness.Tenant, harness.Interceptor);
            var sut = harness.CreateService(context);

            // Act
            await Should.ThrowAsync<InvalidOperationException>(() => sut.CreateOrganizationAsync(MariaId, CantinaDaNonna));

            // Assert
            harness.TenantScope.OrganizationId.ShouldBeNull();
            using var readContext = database.CreateContext();
            (await readContext.Organizations.CountAsync()).ShouldBe(0);
            (await readContext.OrganizationSettings.IgnoreQueryFilters().CountAsync()).ShouldBe(0);
        }
    }
}
