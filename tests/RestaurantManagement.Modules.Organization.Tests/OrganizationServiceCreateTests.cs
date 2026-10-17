using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using Xunit;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationServiceCreateTests
    {
        private static CreateOrganizationRequest ValidRequest => new()
        {
            Name = "Test Restaurant",
            Currency = "BRL"
        };

        private static IUserRoleLookup CreateUserRoleLookup(Guid userId, List<Guid>? organizationIds = null)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.UserExistsAsync(userId).Returns(true);
            userRoleLookup.GetOrganizationIdsForUserAsync(userId).Returns(organizationIds ?? new List<Guid>());
            return userRoleLookup;
        }

        [Fact]
        [Trait("Rule", "OR5")]
        public async Task CreateOrganizationAsync_WhenRoleAssignmentFails_PersistsNoOrganizationOrSettings()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("Role assignment unavailable")));
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<Exception>("the creation fails when the Owner role cannot be assigned");

            using var verifyContext = database.CreateContext();
            (await verifyContext.Organizations.CountAsync()).Should().Be(0, "no organization is kept when the Owner role could not be assigned");
            (await verifyContext.OrganizationSettings.IgnoreQueryFilters().CountAsync()).Should().Be(0, "no settings are kept when the Owner role could not be assigned");
        }

        [Fact]
        [Trait("Rule", "OR5")]
        public async Task GetOrganizationsAsync_AfterFailedRoleAssignment_ReturnsEmptyList()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("Role assignment unavailable")));
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using (var context = database.CreateContext())
            {
                var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());
                var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);
                await act.Should().ThrowAsync<Exception>("the creation fails when the Owner role cannot be assigned");
            }

            // Act: since role assignment never succeeded, Identity holds no organization id for this user.
            using var listContext = database.CreateContext();
            var listSut = new OrganizationService(listContext, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());
            var organizations = await listSut.GetOrganizationsAsync(userId);

            // Assert
            organizations.Should().BeEmpty("a failed creation leaves the user with no organization");
        }

        [Fact]
        [Trait("Rule", "OR5")]
        public async Task CreateOrganizationAsync_AfterFailedRoleAssignment_SecondAttemptSucceeds()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("Role assignment unavailable")));

            using (var context = database.CreateContext())
            {
                var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());
                var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);
                await act.Should().ThrowAsync<Exception>("the first attempt fails when the Owner role cannot be assigned");
            }

            // Act: the failed attempt must not have consumed the one-trial-organization limit.
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);

            using var secondContext = database.CreateContext();
            var secondSut = new OrganizationService(secondContext, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());
            var response = await secondSut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            response.Should().NotBeNull("a failed attempt does not use up the one-trial-organization limit, so a retry works");

            using var verifyContext = database.CreateContext();
            (await verifyContext.Organizations.CountAsync()).Should().Be(1, "after the retry the user has exactly one organization");
        }

        [Fact]
        [Trait("Rule", "OR5")]
        public async Task CreateOrganizationAsync_WhenRollbackFails_LogsError()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    // Simulate the database becoming unavailable right as we try to roll back.
                    database.CloseConnection();
                    return Task.FromException(new InvalidOperationException("Role assignment unavailable"));
                });
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<Exception>("the creation fails when the Owner role cannot be assigned");

            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => state.ToString()!.Contains("Failed to roll back organization creation")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        [Trait("Rule", "OR5")]
        public async Task CreateOrganizationAsync_WhenRoleAssignmentFails_ThrowsAndReturnsNoResponse()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("Role assignment unavailable")));
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>("the caller gets the failure, never a successful response, when the Owner role cannot be assigned");
        }

        [Fact]
        [Trait("Rule", "OR5")]
        public async Task CreateOrganizationAsync_WhenCommitFailsAfterRoleAssignmentSucceeds_RevokesRoleAndThrows()
        {
            // Arrange: role assignment itself succeeds (Identity has already recorded the
            // Owner role), but the organization database becomes unavailable right before
            // the transaction commits. The compensating action must revoke the Owner role
            // from Identity so no dangling UserRole is left pointing at an organization
            // that was never committed, and the caller must still receive the original
            // failure.
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    database.CloseConnection();
                    return Task.CompletedTask;
                });
            userRoleAssigner.RevokeRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert: the role assignment was made, but the caller still gets a failure,
            // not a successful response, and the compensating revoke was issued for the
            // same user/organization/role.
            await act.Should().ThrowAsync<Exception>("the caller gets a failure when the organization cannot be saved");

            await userRoleAssigner.Received(1).AssignRoleAsync(userId, Arg.Any<Guid>(), Roles.Owner);
            await userRoleAssigner.Received(1).RevokeRoleAsync(userId, Arg.Any<Guid>(), Roles.Owner);

            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => state.ToString()!.Contains("dangling UserRole")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        [Trait("Rule", "OR5")]
        public async Task CreateOrganizationAsync_WhenCommitFailsAndRevokeAlsoFails_LogsSecondErrorAndThrowsOriginalException()
        {
            // Arrange: same commit-failure scenario as above, but the compensating
            // RevokeRoleAsync call itself also fails (e.g. Identity's database is
            // unreachable too). The caller must still see the original commit failure,
            // not the revoke failure, with the revoke failure logged separately.
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    database.CloseConnection();
                    return Task.CompletedTask;
                });
            userRoleAssigner.RevokeRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("Identity database unavailable")));
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert: the exception surfaced to the caller is the original commit failure,
            // not the revoke failure, and the revoke failure is logged separately.
            var thrown = await act.Should().ThrowAsync<Exception>();
            thrown.Which.Message.Should().NotContain("Identity database unavailable", "the caller sees the original save failure, not the failure of the clean-up");

            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => state.ToString()!.Contains("dangling UserRole")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());

            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => state.ToString()!.Contains("Failed to revoke")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        [Trait("Rule", "OR1")]
        public async Task CreateOrganizationAsync_WhenRoleAssignmentSucceeds_PersistsAllAndReturnsResponse()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), TestTenant.None, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var response = await sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            response.Should().NotBeNull("a valid request creates the organization");
            response.Name.Should().Be(ValidRequest.Name, "the organization keeps the name the user chose");
            response.PlanType.Should().Be("TRIAL", "a new organization always starts on the TRIAL plan");

            using var verifyContext = database.CreateContext();
            (await verifyContext.Organizations.CountAsync()).Should().Be(1, "exactly one organization is stored");
            (await verifyContext.OrganizationSettings.IgnoreQueryFilters().CountAsync()).Should().Be(1, "the organization gets its own settings");

            await userRoleAssigner.Received(1).AssignRoleAsync(userId, response.Id, Roles.Owner);
        }
    }
}
