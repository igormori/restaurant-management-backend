using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationServiceNotFoundTests
    {
        private static readonly EditOrganizationRequest EditRequest = new()
        {
            Name = "Cantina da Nonna",
            ServiceChargeRate = 10m,
            DiscountApprovalLimit = 5m,
            BusinessDayCutoff = "04:00"
        };

        private static OrganizationService CreateSut(OrganizationTestDatabase database, ITenantContext tenant, IUserRoleLookup lookup)
        {
            var context = database.CreateContext(tenant);
            return new OrganizationService(context, lookup, Substitute.For<IUserRoleAssigner>(), new TenantScope(), tenant,
                TestLocalizer.Create(), Substitute.For<ILogger<OrganizationService>>(), Substitute.For<IAuditLog>());
        }

        [Fact]
        public async Task CreateOrganizationAsync_UserDoesNotExist_ThrowsUnauthorizedBusinessException()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var lookup = Substitute.For<IUserRoleLookup>();
            lookup.UserExistsAsync(Arg.Any<Guid>()).Returns(false);
            var sut = CreateSut(database, TestTenant.Of(Guid.NewGuid()), lookup);

            // Act
            var act = () => sut.CreateOrganizationAsync(Guid.NewGuid(), new CreateOrganizationRequest { Name = "Cantina da Nonna", Currency = "BRL" });

            // Assert
            var ex = await Should.ThrowAsync<BusinessException>(act);
            ex.StatusCode.ShouldBe(401);
            ex.Message.ShouldBe("UserNotFound");
        }

        [Fact]
        public async Task GetOrganizationsAsync_UserDoesNotExist_ThrowsUnauthorizedBusinessException()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var lookup = Substitute.For<IUserRoleLookup>();
            lookup.UserExistsAsync(Arg.Any<Guid>()).Returns(false);
            var sut = CreateSut(database, TestTenant.Of(Guid.NewGuid()), lookup);

            // Act
            var act = () => sut.GetOrganizationsAsync(Guid.NewGuid());

            // Assert
            var ex = await Should.ThrowAsync<BusinessException>(act);
            ex.StatusCode.ShouldBe(401);
            ex.Message.ShouldBe("UserNotFound");
        }

        [Fact]
        public async Task EditOrganizationAsync_OrganizationDoesNotExist_ThrowsNotFoundBusinessException()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var sut = CreateSut(database, TestTenant.Of(Guid.NewGuid()), Substitute.For<IUserRoleLookup>());

            // Act
            var act = () => sut.EditOrganizationAsync(Guid.NewGuid(), EditRequest);

            // Assert
            var ex = await Should.ThrowAsync<BusinessException>(act);
            ex.StatusCode.ShouldBe(404);
            ex.Message.ShouldBe("OrganizationNotFound");
        }

        [Fact]
        public async Task EditOrganizationAsync_OrganizationHasNoSettings_ThrowsNotFoundBusinessException()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = Guid.NewGuid();
            using (var seed = database.CreateContext())
            {
                seed.Organizations.Add(new Entities.Organization
                {
                    Id = organizationId,
                    Name = "Trattoria Bella",
                    Currency = Currency.BRL,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
            }
            var sut = CreateSut(database, TestTenant.Of(organizationId), Substitute.For<IUserRoleLookup>());

            // Act
            var act = () => sut.EditOrganizationAsync(Guid.NewGuid(), EditRequest);

            // Assert
            var ex = await Should.ThrowAsync<BusinessException>(act);
            ex.StatusCode.ShouldBe(404);
            ex.Message.ShouldBe("OrganizationSettingsNotFound");
        }
    }
}
