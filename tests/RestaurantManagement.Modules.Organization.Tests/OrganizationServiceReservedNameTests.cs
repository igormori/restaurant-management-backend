using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using Shouldly;
using Xunit;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationServiceReservedNameTests
    {
        [Theory]
        [InlineData("TEST_SENTRY")]
        [InlineData("test_sentry")]
        [Trait("Rule", "OR1")]
        public async Task CreateOrganizationAsync_NameMatchesFormerSentryTestHook_CreatesOrganization(string name)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.UserExistsAsync(userId).Returns(true);
            userRoleLookup.GetOrganizationIdsForUserAsync(userId).Returns(new List<Guid>());
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);
            var logger = Substitute.For<ILogger<OrganizationService>>();
            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());
            var request = new CreateOrganizationRequest { Name = name, Currency = "BRL" };

            // Act
            var response = await sut.CreateOrganizationAsync(userId, request);

            // Assert
            response.Name.ShouldBe(name);
            using var verifyContext = database.CreateContext();
            (await verifyContext.Organizations.CountAsync(o => o.Name == name)).ShouldBe(1);
        }
    }
}
