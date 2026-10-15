using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationServiceEditTests
    {
        private static EditOrganizationRequest ValidRequest => new()
        {
            Name = "Updated Restaurant Name",
            Description = "Updated description"
        };

        private static async Task<Guid> SeedOrganizationAsync(OrganizationTestDatabase database)
        {
            var organizationId = Guid.NewGuid();
            using var context = database.CreateContext();

            context.Organizations.Add(new Entities.Organization
            {
                Id = organizationId,
                Name = "Original Restaurant",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            context.OrganizationSettings.Add(new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                PlanType = "TRIAL",
                MaxLocations = 1,
                TrialEndDate = DateTime.UtcNow.AddDays(30),
                IsTrialActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            return organizationId;
        }

        private static IUserRoleLookup CreateUserRoleLookup(Guid userId, Guid organizationId, string? role)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.GetRoleAsync(userId, organizationId).Returns(role);
            return userRoleLookup;
        }

        [Fact]
        public async Task EditOrganizationAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var act = () => sut.EditOrganizationAsync(userId, organizationId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }

        [Fact]
        public async Task EditOrganizationAsync_WhenUserIsOwnerOfOrganization_EditsSuccessfully()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var response = await sut.EditOrganizationAsync(userId, organizationId, ValidRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidRequest.Name);
            response.Description.Should().Be(ValidRequest.Description);
        }

        [Fact]
        public async Task EditOrganizationAsync_WhenUserIsAdminOfOrganization_EditsSuccessfully()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var response = await sut.EditOrganizationAsync(userId, organizationId, ValidRequest);

            // Assert
            response.Should().NotBeNull();
            response.Name.Should().Be(ValidRequest.Name);
        }

        [Fact]
        public async Task EditOrganizationAsync_WhenUserHasStaffRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Staff);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            using var context = database.CreateContext();
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, TestLocalizer.Create(), logger);

            // Act
            var act = () => sut.EditOrganizationAsync(userId, organizationId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<BusinessException>().Where(e => e.StatusCode == 403);
        }
    }
}
