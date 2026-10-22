using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class LocationListTests
    {
        private static async Task<Guid> SeedOrganizationWithLocationAsync(OrganizationTestDatabase database, Guid? locationId = null)
        {
            var organizationId = Guid.NewGuid();
            using var context = database.CreateContext();
            context.Organizations.Add(new Entities.Organization
            {
                Id = organizationId,
                Name = "Cantina da Nonna",
                Currency = Currency.BRL,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Locations.Add(new Location
            {
                Id = locationId ?? Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Unidade Batel",
                Address = "Rua Bento Viana, 100",
                City = "Curitiba",
                State = "PR",
                PostalCode = "80420-100",
                Country = "Brasil",
                PhoneNumber = "+55 41 3000-3000",
                Status = LocationStatus.Active,
                TimeZone = "America/Sao_Paulo",
                Code = "BATEL",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            return organizationId;
        }

        private static LocationService CreateSut(OrganizationTestDatabase database, Guid organizationId, IAccessGuard guard)
        {
            var tenant = TestTenant.Of(organizationId);
            return new LocationService(database.CreateContext(tenant), guard, tenant, TestLocalizer.Create(),
                Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());
        }

        [Theory]
        [InlineData("Owner")]
        [InlineData("WaiterWithLocationsRead")]
        [InlineData("WaiterWithOnlyMenusRead")]
        [Trait("Rule", "OR11")]
        public async Task GetLocationsAsync_OwnerOrMemberWithAnyAccess_ReturnsItsLocations(string member)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var batelId = Guid.NewGuid();
            var organizationId = await SeedOrganizationWithLocationAsync(database, batelId);
            var userId = Guid.NewGuid();
            var grid = member switch
            {
                "Owner" => TestAccess.OwnerGrid,
                "WaiterWithLocationsRead" => TestAccess.Grid((batelId, AccessModule.Locations, AccessLevel.Read), (batelId, AccessModule.Orders, AccessLevel.Manage)),
                _ => TestAccess.Grid((batelId, AccessModule.Menus, AccessLevel.Read))
            };
            var sut = CreateSut(database, organizationId, TestAccess.Guard(userId, organizationId, grid, batelId));

            // Act
            var locations = await sut.GetLocationsAsync(userId);

            // Assert
            locations.Count.ShouldBe(1, "the Owner and any member with access to something may list the locations");
            locations[0].Name.ShouldBe("Unidade Batel", "the list holds the token organization's location");
        }

        [Fact]
        [Trait("Rule", "OR11")]
        public async Task GetLocationsAsync_NewAdminWithNoAccess_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationWithLocationAsync(database);
            var userId = Guid.NewGuid();
            var sut = CreateSut(database, organizationId, TestAccess.Guard(userId, organizationId, TestAccess.NoAccessGrid));

            // Act
            var act = () => sut.GetLocationsAsync(userId);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "a member who has been given no access yet may not list the locations");
        }
    }
}
