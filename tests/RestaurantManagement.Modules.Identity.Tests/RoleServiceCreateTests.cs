using NSubstitute;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;
using static RestaurantManagement.Modules.Identity.Tests.RoleServiceTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>Creating a role at Cantina da Nonna, whose five default roles already exist.</summary>
    public class RoleServiceCreateTests
    {
        public static IEnumerable<object[]> InvalidNames => new[]
        {
            new object[] { "" },
            new object[] { "   " },
            new object[] { "owner" },
            new object[] { "OWNER" },
            new object[] { " Owner " },
            new object[] { new string('G', 101) }
        };

        [Fact]
        [Trait("Rule", "T100")]
        public async Task CreateAsync_MarketingWithLevels_StoresTheTrimmedNameAndOnlyTheLevelsAboveNone()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(DefaultRoles);
            var sut = CreateRoleService(database);
            var request = new CreateRoleRequest
            {
                Name = "  Marketing ",
                Levels = new Dictionary<string, string> { ["Menus"] = "Manage", ["Reports"] = "Read", ["Orders"] = "None" }
            };

            // Act
            var response = await sut.CreateAsync(request);

            // Assert
            response.Name.ShouldBe("Marketing", "the name is trimmed");
            StoredRoleNames(database, CantinaId).ShouldContain("Marketing", "the role is stored in the token organization");
            StoredLevels(database, CantinaId, response.Id).ShouldBe("Menus:Manage,Reports:Read",
                "a module left out or set to None means None, and a None level is not stored");
        }

        [Theory]
        [MemberData(nameof(InvalidNames))]
        [Trait("Rule", "T101")]
        public async Task CreateAsync_EmptyBlankOwnerOrTooLongName_Refuses400AndStoresNothing(string name)
        {
            // Arrange
            using var database = await CreateDatabaseAsync(DefaultRoles);
            var sut = CreateRoleService(database);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.CreateAsync(new CreateRoleRequest { Name = name }));

            // Assert
            exception.StatusCode.ShouldBe(400, "an empty name, the name Owner or a name over 100 characters is a bad request");
            exception.Message.ShouldBe("RoleNameInvalid", "the refusal says the name is not allowed");
            StoredRoleNames(database, CantinaId).ShouldBe(DefaultRoles.OrderBy(n => n), ignoreOrder: true, customMessage: "nothing is stored for a refused name");
        }

        [Fact]
        [Trait("Rule", "T102")]
        public async Task CreateAsync_NameUsedInAnotherLetterCase_Refuses400AndStoresNothing()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(DefaultRoles);
            var sut = CreateRoleService(database);

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.CreateAsync(new CreateRoleRequest { Name = "waiter" }));

            // Assert
            exception.StatusCode.ShouldBe(400, "two roles of one organization cannot share a name, whatever the letter case");
            exception.Message.ShouldBe("RoleNameAlreadyUsed", "the refusal says the name is taken");
            StoredRoleNames(database, CantinaId).ShouldBe(DefaultRoles.OrderBy(n => n), ignoreOrder: true, customMessage: "nothing is stored for a duplicate name");
        }

        [Theory]
        [InlineData("Dessert", "Manage")]
        [InlineData("Menus", "Admin")]
        [Trait("Rule", "T103")]
        public async Task CreateAsync_UnknownModuleOrLevelName_Refuses400AndStoresNothing(string module, string level)
        {
            // Arrange
            using var database = await CreateDatabaseAsync(DefaultRoles);
            var sut = CreateRoleService(database);
            var request = new CreateRoleRequest { Name = "Marketing", Levels = new Dictionary<string, string> { [module] = level } };

            // Act
            var exception = await Should.ThrowAsync<BusinessException>(() => sut.CreateAsync(request));

            // Assert
            exception.StatusCode.ShouldBe(400, "a module or level that does not exist is a bad request");
            StoredRoleNames(database, CantinaId).ShouldBe(DefaultRoles.OrderBy(n => n), ignoreOrder: true, customMessage: "nothing is stored for a bad request");
        }
    }
}
