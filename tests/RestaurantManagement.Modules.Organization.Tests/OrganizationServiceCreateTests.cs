using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationServiceCreateTests
    {
        private static CreateOrganizationRequest ValidRequest => new()
        {
            Name = "Test Restaurant"
        };

        private static IUserRoleLookup CreateUserRoleLookup(Guid userId, List<Guid>? organizationIds = null)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.UserExistsAsync(userId).Returns(true);
            userRoleLookup.GetOrganizationIdsForUserAsync(userId).Returns(organizationIds ?? new List<Guid>());
            return userRoleLookup;
        }

        [Fact]
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
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<Exception>();

            using var verifyContext = database.CreateContext();
            (await verifyContext.Organizations.CountAsync()).Should().Be(0);
            (await verifyContext.OrganizationSettings.CountAsync()).Should().Be(0);
        }

        [Fact]
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
                var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);
                var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);
                await act.Should().ThrowAsync<Exception>();
            }

            // Act: since role assignment never succeeded, Identity holds no organization id for this user.
            using var listContext = database.CreateContext();
            var listSut = new OrganizationService(listContext, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);
            var organizations = await listSut.GetOrganizationsAsync(userId);

            // Assert
            organizations.Should().BeEmpty();
        }

        [Fact]
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
                var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);
                var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);
                await act.Should().ThrowAsync<Exception>();
            }

            // Act: the failed attempt must not have consumed the one-trial-organization limit.
            userRoleAssigner.AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
                .Returns(Task.CompletedTask);

            using var secondContext = database.CreateContext();
            var secondSut = new OrganizationService(secondContext, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);
            var response = await secondSut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            response.Should().NotBeNull();

            using var verifyContext = database.CreateContext();
            (await verifyContext.Organizations.CountAsync()).Should().Be(1);
        }

        [Fact]
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
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<Exception>();

            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(state => state.ToString()!.Contains("Failed to roll back organization creation")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
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
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
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
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert: the role assignment was made, but the caller still gets a failure,
            // not a successful response, and the compensating revoke was issued for the
            // same user/organization/role.
            await act.Should().ThrowAsync<Exception>();

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
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var act = () => sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert: the exception surfaced to the caller is the original commit failure,
            // not the revoke failure, and the revoke failure is logged separately.
            var thrown = await act.Should().ThrowAsync<Exception>();
            thrown.Which.Message.Should().NotContain("Identity database unavailable");

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
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var response = await sut.CreateOrganizationAsync(userId, ValidRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidRequest.Name);
            response.PlanType.Should().Be("TRIAL");

            using var verifyContext = database.CreateContext();
            (await verifyContext.Organizations.CountAsync()).Should().Be(1);
            (await verifyContext.OrganizationSettings.CountAsync()).Should().Be(1);

            await userRoleAssigner.Received(1).AssignRoleAsync(userId, response.Id, Roles.Owner);
        }
    }
}
