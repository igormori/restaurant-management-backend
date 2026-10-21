using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Setting and reading a member's access at one location: roles are merged into levels (the higher
    /// level per cell wins), changed levels are applied on top, and the final levels and the role labels
    /// are stored. Maria is a member of Cantina da Nonna; Ana is its Owner; the roles are Admin, Cashier
    /// and Waiter with the spec's default levels.
    /// </summary>
    public class MemberAccessServiceTests
    {
        private static readonly string[] Roles = { "Admin", "Cashier", "Waiter" };
        private static readonly IReadOnlyDictionary<AccessModule, AccessLevel> NoChanges = new Dictionary<AccessModule, AccessLevel>();

        private static string Describe(IEnumerable<(Guid LocationId, AccessModule Module, AccessLevel Level)> cells, Guid locationId)
            => string.Join(",", cells.Where(c => c.LocationId == locationId).OrderBy(c => c.Module).Select(c => $"{c.Module}:{c.Level}"));

        [Fact]
        [Trait("Rule", "T37")]
        public async Task SetAccessAsync_WaiterAndCashierAtBatel_StoresTheHigherLevelOfEachModule()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var sut = CreateService(database);

            // Act
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter", "Cashier" }, NoChanges);

            // Assert
            Describe(StoredCells(database, MariaId, CantinaId), BatelId).ShouldBe(
                "Locations:Read,Menus:Read,Cashier:Manage,Orders:Manage",
                "Waiter and Cashier together give the higher level of each module: Orders Manage from the Waiter, Cashier Manage from the Cashier, Reports and the rest None");
        }

        [Fact]
        [Trait("Rule", "T37")]
        public async Task SetAccessAsync_WaiterAndCashierWithCashierLoweredToReadAndReportsNone_StoresTheSpecExampleExactly()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var sut = CreateService(database);
            var changes = new Dictionary<AccessModule, AccessLevel> { [AccessModule.Cashier] = AccessLevel.Read, [AccessModule.Reports] = AccessLevel.None };

            // Act
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter", "Cashier" }, changes);

            // Assert
            Describe(StoredCells(database, MariaId, CantinaId), BatelId).ShouldBe(
                "Locations:Read,Menus:Read,Cashier:Read,Orders:Manage",
                "the changed cells win over the roles: Cashier is Read, Reports is None and stores no cell");
            var shown = (await sut.GetAccessAsync(MariaId)).Single(a => a.LocationId == BatelId);
            shown.RoleNames.ShouldBe(new[] { "Waiter", "Cashier" }, ignoreOrder: true, customMessage: "the role names given are stored with the grid for display");
        }

        [Fact]
        [Trait("Rule", "T38")]
        public async Task SetAccessAsync_AdminManagesAtBatelAndReadsAtMercado_StoresDifferentLevelsAndTheAdminLabelAtBoth()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var sut = CreateService(database);
            var everythingRead = Enum.GetValues<AccessModule>().ToDictionary(m => m, _ => AccessLevel.Read);

            // Act
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Admin" }, NoChanges);
            await sut.SetAccessAsync(MariaId, MercadoId, new[] { "Admin" }, everythingRead);

            // Assert
            var cells = StoredCells(database, MariaId, CantinaId);
            Describe(cells, BatelId).ShouldBe("Locations:Manage,Menus:Manage,Cashier:Manage,Orders:Manage,Kitchen:Manage,Reports:Manage,Members:Manage",
                "Admin gives Manage on everything at Batel");
            Describe(cells, MercadoId).ShouldBe("Locations:Read,Menus:Read,Cashier:Read,Orders:Read,Kitchen:Read,Reports:Read,Members:Read",
                "the same Admin only reads everything at Mercado Municipal");
            var shown = await sut.GetAccessAsync(MariaId);
            shown.SelectMany(a => a.RoleNames).ShouldBe(new[] { "Admin", "Admin" }, ignoreOrder: false, customMessage: "the label is Admin at both locations whatever the levels are");
        }

        [Fact]
        [Trait("Rule", "T39")]
        public async Task SetAccessAsync_Owner_Throws403AndStoresNothing()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var sut = CreateService(database);

            // Act
            var act = () => sut.SetAccessAsync(AnaId, BatelId, new[] { "Waiter" }, NoChanges);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "the Owner is never restricted, so no grid change can lower the Owner");
            StoredCells(database, AnaId, CantinaId).ShouldBeEmpty("a refused change stores nothing");
        }

        [Theory]
        [InlineData("NotAMember")]
        [InlineData("RoleOfAnotherOrganization")]
        [InlineData("LocationOfAnotherOrganization")]
        [Trait("Rule", "T40")]
        public async Task SetAccessAsync_NonMemberOrRoleOrLocationOfAnotherOrganization_ThrowsNotFoundAndStoresNothing(string problem)
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            using (var context = database.CreateContext())
            {
                var onlyTrattoriaRole = Role(TrattoriaId, "Marketing");
                context.Set<OrganizationRole>().Add(onlyTrattoriaRole);
                await context.SaveChangesAsync();
            }
            var sut = CreateService(database);
            var userId = problem == "NotAMember" ? PaoloId : MariaId;
            var locationId = problem == "LocationOfAnotherOrganization" ? TrattoriaCentroId : BatelId;
            var roleName = problem == "RoleOfAnotherOrganization" ? "Marketing" : "Waiter";

            // Act
            var act = () => sut.SetAccessAsync(userId, locationId, new[] { roleName }, NoChanges);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(404, "a member, role or location of another organization answers as if it did not exist");
            using var readContext = database.CreateContext();
            readContext.Set<MemberLocationLevel>().Count().ShouldBe(0, "a refused change stores no level");
            readContext.Set<MemberLocationRole>().Count().ShouldBe(0, "a refused change stores no label");
        }

        [Fact]
        [Trait("Rule", "T41")]
        public async Task SetAccessAsync_AccessChanged_RecordsOneEntryWithLabelsAndLevelsBeforeAndAfter()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateService(database, auditLog);

            // Act
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter" }, NoChanges);
            var first = auditLog.ReceivedCalls().ShouldHaveSingleItem("one grid change records exactly one audit entry").GetArguments()[0].ShouldBeOfType<AuditEntryDto>();
            auditLog.ClearReceivedCalls();
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter" }, new Dictionary<AccessModule, AccessLevel> { [AccessModule.Orders] = AccessLevel.Read });
            var second = auditLog.ReceivedCalls().ShouldHaveSingleItem("the second change records its own entry").GetArguments()[0].ShouldBeOfType<AuditEntryDto>();

            // Assert
            first.Action.ShouldBe(AuditActions.MemberAccessChanged, "the entry says a member's access changed");
            first.EntityId.ShouldBe(MariaId, "the entry points at the member whose access changed");
            first.LocationId.ShouldBe(BatelId, "the entry names the location");
            (first.Before ?? string.Empty).ShouldNotContain("Waiter", customMessage: "before the first change the member had no Waiter label");
            var firstAfter = first.After.ShouldNotBeNull("the entry records the state after the change");
            firstAfter.ShouldContain("Waiter", customMessage: "after the change the label is recorded by role name");
            firstAfter.ShouldContain("Orders", customMessage: "the levels are recorded by module name");
            firstAfter.ShouldContain("Manage", customMessage: "the levels are recorded by level name");
            var secondBefore = second.Before.ShouldNotBeNull("the second entry records the state before the change");
            secondBefore.ShouldContain("Waiter", customMessage: "before the second change the label was Waiter");
            secondBefore.ShouldContain("Manage", customMessage: "before the second change Orders was Manage");
            second.After.ShouldNotBeNull().ShouldNotContain("Manage", customMessage: "after Orders is lowered to Read no Manage level is left at Batel");
        }

        [Fact]
        [Trait("Rule", "T41")]
        public async Task SetAccessAsync_NothingChanged_RecordsNoEntry()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateService(database, auditLog);
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter" }, NoChanges);
            auditLog.ClearReceivedCalls();

            // Act
            await CreateService(database, auditLog).SetAccessAsync(MariaId, BatelId, new[] { "Waiter" }, NoChanges);

            // Assert
            auditLog.ReceivedCalls().ShouldBeEmpty("setting the same roles and levels again changes nothing, so it is not recorded");
        }

        [Fact]
        [Trait("Rule", "T41")]
        public async Task SetAccessAsync_AuditWriteFails_KeepsTheChangeAndLogsTheError()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var auditLog = Substitute.For<IAuditLog>();
            auditLog.RecordAsync(Arg.Any<AuditEntryDto>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Audit log unavailable"));
            var logger = Substitute.For<ILogger<RestaurantManagement.Modules.Identity.Services.Members.MemberAccessService>>();
            var sut = CreateService(database, auditLog, logger);

            // Act
            var act = () => sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter" }, NoChanges);

            // Assert
            await Should.NotThrowAsync(act, "a failed audit write never fails the change");
            Describe(StoredCells(database, MariaId, CantinaId), BatelId).ShouldBe("Locations:Read,Menus:Read,Orders:Manage", "the change is kept even when the audit entry cannot be recorded");
            logger.Received().Log(LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        [Trait("Rule", "T42")]
        public async Task GetAccessAsync_RoleDefaultLevelsChangedLater_KeepsTheMembersStoredLevels()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var sut = CreateService(database);
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter" }, NoChanges);
            using (var context = database.CreateContext())
            {
                var waiterOrders = context.Set<OrganizationRoleLevel>().ToList()
                    .Single(l => l.OrganizationId == CantinaId && l.Module == AccessModule.Orders && context.Set<OrganizationRole>().Single(r => r.Id == l.RoleId).Name == "Waiter");
                waiterOrders.Level = AccessLevel.Read;
                await context.SaveChangesAsync();
            }

            // Act
            var shown = await CreateService(database).GetAccessAsync(MariaId);

            // Assert
            Describe(StoredCells(database, MariaId, CantinaId), BatelId).ShouldBe("Locations:Read,Menus:Read,Orders:Manage", "editing a role's default levels later does not change existing members' grids");
            shown.Single().Levels.Single(l => l.Module == AccessModule.Orders).Level.ShouldBe(AccessLevel.Manage, "the reader shows the stored level, not the role's new default");
        }

        [Fact]
        [Trait("Rule", "T42")]
        public async Task GetAccessAsync_RoleRenamed_ShowsTheNewNameAndKeepsTheLevels()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Roles);
            var sut = CreateService(database);
            await sut.SetAccessAsync(MariaId, BatelId, new[] { "Waiter" }, NoChanges);
            using (var context = database.CreateContext())
            {
                context.Set<OrganizationRole>().Single(r => r.OrganizationId == CantinaId && r.Name == "Waiter").Name = "Garçom";
                await context.SaveChangesAsync();
            }

            // Act
            var shown = (await CreateService(database).GetAccessAsync(MariaId)).Single();

            // Assert
            shown.RoleNames.ShouldBe(new[] { "Garçom" }, ignoreOrder: false, customMessage: "renaming a role updates every member's label");
            Describe(StoredCells(database, MariaId, CantinaId), BatelId).ShouldBe("Locations:Read,Menus:Read,Orders:Manage", "a rename changes labels only, never levels");
        }
    }
}
