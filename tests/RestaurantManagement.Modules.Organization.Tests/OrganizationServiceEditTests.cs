using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared.Auth;
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

        private static OrganizationService CreateSut(OrganizationTestDatabase database, Guid organizationId, IAccessGuard guard)
        {
            var tenant = TestTenant.Of(organizationId);
            return new OrganizationService(database.CreateContext(tenant), Substitute.For<IMemberAccessLookup>(), Substitute.For<IOrganizationAccessSetup>(),
                guard, new TenantScope(), tenant, TestLocalizer.Create(), Substitute.For<ILogger<OrganizationService>>(), Substitute.For<IAuditLog>());
        }

        [Fact]
        [Trait("Rule", "OR7")]
        public async Task EditOrganizationAsync_OwnerWithNoStoredLevels_EditsSuccessfully()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var userId = Guid.NewGuid();
            var sut = CreateSut(database, organizationId, TestAccess.OwnerGuard(userId, organizationId));

            // Act
            var response = await sut.EditOrganizationAsync(userId, ValidRequest);

            // Assert
            response.Should().NotBeNull("an Owner may edit the organization");
            response.Name.Should().Be(ValidRequest.Name, "the edit changes the organization name");
            response.Description.Should().Be(ValidRequest.Description, "the edit changes the organization description");
        }

        [Theory]
        [InlineData("AdminWithLocationsManageAtEveryLocation")]
        [InlineData("ManagerWithReportsManage")]
        [InlineData("NewAdminWithNoAccess")]
        [Trait("Rule", "OR36")]
        public async Task EditOrganizationAsync_NotTheOwner_ThrowsForbiddenAndKeepsTheSettings(string member)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationAsync(database);
            var batelId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var grid = member switch
            {
                "AdminWithLocationsManageAtEveryLocation" => TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Manage), (batelId, AccessModule.Menus, AccessLevel.Manage), (batelId, AccessModule.Members, AccessLevel.Manage)),
                "ManagerWithReportsManage" => TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Read), (batelId, AccessModule.Reports, AccessLevel.Manage)),
                _ => TestAccess.NoAccessGrid
            };
            var sut = CreateSut(database, organizationId, TestAccess.Guard(userId, organizationId, grid, batelId));

            // Act
            var act = () => sut.EditOrganizationAsync(userId, ValidRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "only the Owner edits the organization and its settings");
            using var verifyContext = database.CreateContext(TestTenant.Of(organizationId));
            (await verifyContext.Organizations.SingleAsync()).Name.ShouldBe("Original Restaurant", "a refused edit changes nothing");
        }
    }
}
