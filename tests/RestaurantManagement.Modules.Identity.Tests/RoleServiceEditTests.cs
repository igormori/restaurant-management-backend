using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;
using static RestaurantManagement.Modules.Identity.Tests.RoleServiceTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Editing the default levels of a role and renaming it at Cantina da Nonna. Waiter starts with Locations Read,
    /// Menus Read and Orders Manage; Maria holds the Waiter label at Batel with the Waiter levels.
    /// </summary>
    public class RoleServiceEditTests
    {
        private static IdentityTestDatabase Open(out Guid waiterId)
        {
            var database = CreateDatabaseAsync(DefaultRoles).GetAwaiter().GetResult();
            waiterId = RoleIdOf(database, CantinaId, "Waiter");
            return database;
        }

        private static UpdateRoleLevelsRequest Levels(params (string Module, string Level)[] levels)
            => new() { Levels = levels.ToDictionary(l => l.Module, l => l.Level) };

        [Fact]
        [Trait("Rule", "T104")]
        public async Task UpdateLevelsAsync_WaiterLevelsReplaced_StoresTheNewDefaultsOnly()
        {
            // Arrange
            using var database = Open(out var waiterId);
            var sut = CreateRoleService(database);

            // Act
            await sut.UpdateLevelsAsync(waiterId, Levels(("Orders", "Read"), ("Reports", "Read"), ("Menus", "None")));

            // Assert
            StoredLevels(database, CantinaId, waiterId).ShouldBe("Orders:Read,Reports:Read",
                "the new levels replace the old ones: Locations is left out and Menus is None, so neither is stored");
        }

        [Fact]
        [Trait("Rule", "T105")]
        public async Task RenameAsync_WaiterToServer_HoldersShowServerAtTheirLocationAndKeepTheirLevels()
        {
            // Arrange
            using var database = Open(out var waiterId);
            await LabelMariaAsWaiterAtBatelAsync(database, waiterId);
            var sut = CreateRoleService(database);
            var cellsBefore = StoredCells(database, MariaId, CantinaId);

            // Act
            await sut.RenameAsync(waiterId, new RenameRoleRequest { Name = "Server" });

            // Assert
            var shown = (await CreateService(database).GetAccessAsync(MariaId)).Single(a => a.LocationId == BatelId);
            shown.RoleNames.ShouldBe(new[] { "Server" }, ignoreOrder: true, customMessage: "every holder's label shows the new name at once");
            StoredCells(database, MariaId, CantinaId).ShouldBe(cellsBefore, ignoreOrder: true, customMessage: "a rename changes no member's access");
        }

        [Fact]
        [Trait("Rule", "T106")]
        public async Task RenameAsync_OwnNameInAnotherLetterCase_Renames()
        {
            // Arrange
            using var database = Open(out var waiterId);
            var sut = CreateRoleService(database);

            // Act
            var response = await sut.RenameAsync(waiterId, new RenameRoleRequest { Name = "waiter" });

            // Assert
            response.Name.ShouldBe("waiter", "a role may change only the letter case of its own name");
            StoredRoleNames(database, CantinaId).ShouldContain("waiter", "the new spelling is stored");
        }

        public static IEnumerable<object[]> RefusedRenames => new[]
        {
            new object[] { "manager", "RoleNameAlreadyUsed" },
            new object[] { "", "RoleNameInvalid" },
            new object[] { "   ", "RoleNameInvalid" },
            new object[] { "OWNER", "RoleNameInvalid" },
            new object[] { new string('G', 101), "RoleNameInvalid" }
        };

        [Theory]
        [MemberData(nameof(RefusedRenames))]
        [Trait("Rule", "T107")]
        public async Task RenameAsync_ExistingEmptyBlankOrOwnerName_Refuses400AndKeepsTheName(string name, string expectedKey)
        {
            // Arrange
            using var database = Open(out var waiterId);
            var sut = CreateRoleService(database);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.RenameAsync(waiterId, new RenameRoleRequest { Name = name }));

            // Assert
            exception.StatusCode.ShouldBe(400, "a name used by another role, an empty name or Owner is a bad request");
            exception.Message.ShouldBe(expectedKey);
            StoredRoleNames(database, CantinaId).ShouldBe(DefaultRoles.OrderBy(n => n), ignoreOrder: true, customMessage: "the refused rename changes nothing");
        }

        [Theory]
        [InlineData("Dessert", "Manage")]
        [InlineData("Menus", "Admin")]
        [Trait("Rule", "T131")]
        public async Task UpdateLevelsAsync_UnknownModuleOrLevelName_Refuses400AndKeepsTheLevels(string module, string level)
        {
            // Arrange
            using var database = Open(out var waiterId);
            var sut = CreateRoleService(database);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.UpdateLevelsAsync(waiterId, Levels((module, level))));

            // Assert
            exception.StatusCode.ShouldBe(400, "a module or level that does not exist is a bad request");
            StoredLevels(database, CantinaId, waiterId).ShouldBe("Locations:Read,Menus:Read,Orders:Manage", "the stored levels are unchanged");
        }

        [Fact]
        [Trait("Rule", "T108")]
        public async Task UpdateLevelsAsync_LevelsChanged_RecordsOneEntryWithTheLevelsBeforeAndAfter()
        {
            // Arrange
            using var database = Open(out var waiterId);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateRoleService(database, auditLog: auditLog);

            // Act
            await sut.UpdateLevelsAsync(waiterId, Levels(("Orders", "Read"), ("Reports", "Read")));

            // Assert
            var entry = Recorded(auditLog).ShouldNotBeNull("a level edit is audited");
            entry.Action.ShouldBe(AuditActions.RoleLevelsChanged, "the entry is filed under the level edit action");
            entry.EntityId.ShouldBe(waiterId, "the entry points at the edited role");
            entry.Before.ShouldNotBeNull().ShouldContain("Manage", customMessage: "the entry records the old Orders level");
            entry.After.ShouldNotBeNull().ShouldContain("Reports", customMessage: "the entry records the new levels");
        }

        [Fact]
        [Trait("Rule", "T109")]
        public async Task UpdateLevelsAsync_SameLevelsAsStored_RecordsNoEntry()
        {
            // Arrange
            using var database = Open(out var waiterId);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateRoleService(database, auditLog: auditLog);

            // Act
            await sut.UpdateLevelsAsync(waiterId, Levels(("Locations", "Read"), ("Menus", "Read"), ("Orders", "Manage")));

            // Assert
            Recorded(auditLog).ShouldBeNull("nothing changed, so nothing is audited");
        }

        [Fact]
        [Trait("Rule", "T110")]
        public async Task RenameAsync_WaiterToServer_RecordsOneEntryWithTheNamesBeforeAndAfter()
        {
            // Arrange
            using var database = Open(out var waiterId);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateRoleService(database, auditLog: auditLog);

            // Act
            await sut.RenameAsync(waiterId, new RenameRoleRequest { Name = "Server" });

            // Assert
            var entry = Recorded(auditLog).ShouldNotBeNull("a rename is audited");
            entry.Action.ShouldBe(AuditActions.RoleRenamed, "the entry is filed under the rename action");
            entry.Before.ShouldNotBeNull().ShouldContain("Waiter", customMessage: "the entry records the old name");
            entry.After.ShouldNotBeNull().ShouldContain("Server", customMessage: "the entry records the new name");
        }

        [Fact]
        [Trait("Rule", "T111")]
        public async Task RenameAsync_SameNameAsStored_RecordsNoEntry()
        {
            // Arrange
            using var database = Open(out var waiterId);
            var auditLog = Substitute.For<IAuditLog>();
            var sut = CreateRoleService(database, auditLog: auditLog);

            // Act
            await sut.RenameAsync(waiterId, new RenameRoleRequest { Name = " Waiter " });

            // Assert
            Recorded(auditLog).ShouldBeNull("the name did not change, so nothing is audited");
        }

        private static async Task LabelMariaAsWaiterAtBatelAsync(IdentityTestDatabase database, Guid waiterId)
        {
            using var context = database.CreateContext();
            var now = new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);
            context.Set<MemberLocationRole>().Add(new MemberLocationRole
            {
                Id = Guid.NewGuid(), OrganizationId = CantinaId, UserId = MariaId, LocationId = BatelId, RoleId = waiterId,
                CreatedAt = now, UpdatedAt = now
            });
            context.Set<MemberLocationLevel>().Add(new MemberLocationLevel
            {
                Id = Guid.NewGuid(), OrganizationId = CantinaId, UserId = MariaId, LocationId = BatelId,
                Module = AccessModule.Orders, Level = AccessLevel.Manage, CreatedAt = now, UpdatedAt = now
            });
            await context.SaveChangesAsync();
        }
    }
}
