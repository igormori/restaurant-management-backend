using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationServiceEditTests
    {
        private static EditOrganizationRequest ValidRequest => new()
        {
            Name = "Updated Restaurant Name",
            Description = "Updated description",
            ServiceChargeRate = 10m,
            DiscountApprovalLimit = 5m,
            BusinessDayCutoff = "04:00"
        };

        private static async Task<Guid> SeedOrganizationAsync(OrganizationTestDatabase database)
        {
            var organizationId = Guid.NewGuid();
            using var context = database.CreateContext();

            context.Organizations.Add(new Entities.Organization
            {
                Id = organizationId,
                Name = "Original Restaurant",
                Currency = Currency.BRL,
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
        [Trait("Rule", "OR8")]
        public async Task EditOrganizationAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role: null);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), tenant, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var act = () => sut.EditOrganizationAsync(userId, ValidRequest);

            // Assert
            await act.Should().ThrowAsync<BusinessException>("a user with no role in the organization may not edit it").Where(e => e.StatusCode == 403);
        }

        [Fact]
        [Trait("Rule", "OR7")]
        public async Task EditOrganizationAsync_WhenUserIsOwnerOfOrganization_EditsSuccessfully()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Owner);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), tenant, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var response = await sut.EditOrganizationAsync(userId, ValidRequest);

            // Assert
            response.Should().NotBeNull("an Owner may edit the organization");
            response.Name.Should().Be(ValidRequest.Name, "the edit changes the organization name");
            response.Description.Should().Be(ValidRequest.Description, "the edit changes the organization description");
        }

        [Fact]
        [Trait("Rule", "OR7")]
        public async Task EditOrganizationAsync_WhenUserIsAdminOfOrganization_EditsSuccessfully()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, Roles.Admin);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), tenant, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var response = await sut.EditOrganizationAsync(userId, ValidRequest);

            // Assert
            response.Should().NotBeNull("an Admin may edit the organization");
            response.Name.Should().Be(ValidRequest.Name, "the edit changes the organization name");
        }

        // Scenario 28
        [Theory]
        [InlineData(Roles.Manager)]
        [InlineData(Roles.Cashier)]
        [InlineData(Roles.Waiter)]
        [InlineData(Roles.Kitchen)]
        [Trait("Rule", "OR8")]
        public async Task EditOrganizationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden(string role)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var userRoleLookup = CreateUserRoleLookup(userId, organizationId, role);
            var userRoleAssigner = Substitute.For<IUserRoleAssigner>();
            var logger = Substitute.For<ILogger<OrganizationService>>();

            var tenant = TestTenant.Of(organizationId);
            using var context = database.CreateContext(tenant);
            var sut = new OrganizationService(context, userRoleLookup, userRoleAssigner, new TenantScope(), tenant, TestLocalizer.Create(), logger, Substitute.For<IAuditLog>());

            // Act
            var act = () => sut.EditOrganizationAsync(userId, ValidRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "only an Owner or Admin may edit the organization");
        }
    }
}
