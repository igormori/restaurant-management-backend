using RestaurantManagement.Shared.Auth;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class LocationListTests
    {
        private static async Task<Guid> SeedOrganizationWithLocationAsync(OrganizationTestDatabase database)
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
                Id = Guid.NewGuid(),
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

        private static LocationService CreateSut(OrganizationTestDatabase database, Guid organizationId, Guid userId, string? role)
        {
            var roleLookup = Substitute.For<IUserRoleLookup>();
            roleLookup.GetRoleAssignmentsAsync(userId, organizationId).Returns(role is null ? new List<RoleAssignmentDto>() : new List<RoleAssignmentDto> { new() { Role = role } });
            var tenant = TestTenant.Of(organizationId);
            return new LocationService(database.CreateContext(tenant), roleLookup, tenant, TestLocalizer.Create(),
                Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());
        }

        [Theory]
        [InlineData(Roles.Owner)]
        [InlineData(Roles.Admin)]
        [InlineData(Roles.Manager)]
        [InlineData(Roles.Cashier)]
        [InlineData(Roles.Waiter)]
        [InlineData(Roles.Kitchen)]
        [Trait("Rule", "OR11")]
        public async Task GetLocationsAsync_UserHasAnyRoleInOrganization_ReturnsItsLocations(string role)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationWithLocationAsync(database);
            var userId = Guid.NewGuid();
            var sut = CreateSut(database, organizationId, userId, role);

            // Act
            var locations = await sut.GetLocationsAsync(userId);

            // Assert
            locations.Count.ShouldBe(1, "every role of the organization may list its locations");
            locations[0].Name.ShouldBe("Unidade Batel", "the list holds the token organization's location");
        }

        [Fact]
        [Trait("Rule", "OR11")]
        public async Task GetLocationsAsync_UserHasNoRoleInOrganization_ThrowsForbiddenNotAMember()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            var organizationId = await SeedOrganizationWithLocationAsync(database);
            var userId = Guid.NewGuid();
            var sut = CreateSut(database, organizationId, userId, role: null);

            // Act
            var act = () => sut.GetLocationsAsync(userId);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(403, "a user with no role in the organization may not list its locations");
            exception.Message.ShouldBe("UserNotMemberOfOrganization", customMessage: "the refusal says the user is not a member");
        }
    }
}
