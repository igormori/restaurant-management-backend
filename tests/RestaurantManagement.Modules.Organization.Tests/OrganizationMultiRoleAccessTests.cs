using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Location and organization access when a user holds several roles in the organization:
    /// permissions add up, a role for one location counts only at that location, a role for all
    /// locations counts everywhere (also at locations added later), and the order of the roles never matters.
    /// </summary>
    public class OrganizationMultiRoleAccessTests
    {
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
        private static readonly Guid CentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");

        private static RoleAssignmentDto AllLocations(string role) => new() { Role = role, LocationId = null };

        private static RoleAssignmentDto At(string role, Guid locationId) => new() { Role = role, LocationId = locationId };

        private static List<RoleAssignmentDto> InOrder(RoleAssignmentDto first, RoleAssignmentDto second, bool firstStoredFirst)
            => firstStoredFirst ? new() { first, second } : new() { second, first };

        private static IUserRoleLookup Holding(params RoleAssignmentDto[] assignments)
            => Holding(assignments.ToList());

        private static IUserRoleLookup Holding(List<RoleAssignmentDto> assignments)
        {
            var lookup = Substitute.For<IUserRoleLookup>();
            lookup.GetRoleAssignmentsAsync(JoaoId, CantinaId).Returns(assignments);
            return lookup;
        }

        private static Location NewLocation(Guid id, string name, string code) => new()
        {
            Id = id,
            OrganizationId = CantinaId,
            Name = name,
            Address = "Rua Bento Viana, 100",
            City = "Curitiba",
            State = "PR",
            PostalCode = "80420-100",
            Country = "Brasil",
            PhoneNumber = "+55 41 3000-3000",
            Status = LocationStatus.Active,
            TimeZone = "America/Sao_Paulo",
            Code = code,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static async Task SeedCantinaWithTwoLocationsAsync(OrganizationTestDatabase database)
        {
            using var context = database.CreateContext();
            context.Organizations.Add(new Entities.Organization
            {
                Id = CantinaId,
                Name = "Cantina da Nonna",
                Currency = Currency.BRL,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.OrganizationSettings.Add(new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = CantinaId,
                PlanType = "TRIAL",
                MaxLocations = 5,
                TrialEndDate = DateTime.UtcNow.AddDays(30),
                IsTrialActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.Locations.Add(NewLocation(BatelId, "Unidade Batel", "BATEL"));
            context.Locations.Add(NewLocation(CentroId, "Unidade Centro", "CENTRO"));
            await context.SaveChangesAsync();
        }

        private static LocationService CreateLocationService(OrganizationTestDatabase database, IUserRoleLookup roles)
        {
            var tenant = TestTenant.Of(CantinaId);
            var context = database.CreateContext(tenant);
            return new LocationService(context, roles, tenant, TestLocalizer.Create(), Substitute.For<IAuditLog>(), Substitute.For<ILogger<LocationService>>());
        }

        private static OrganizationService CreateOrganizationService(OrganizationTestDatabase database, IUserRoleLookup roles)
        {
            var tenant = TestTenant.Of(CantinaId);
            var context = database.CreateContext(tenant);
            return new OrganizationService(context, roles, Substitute.For<IUserRoleAssigner>(), new TenantScope(), tenant, TestLocalizer.Create(),
                Substitute.For<ILogger<OrganizationService>>(), Substitute.For<IAuditLog>());
        }

        private static CreateLocationRequest NewLocationRequest => new()
        {
            Name = "Unidade Mercado Municipal",
            Address = "Av. Sete de Setembro, 1865",
            City = "Curitiba",
            State = "PR",
            PostalCode = "80060-070",
            Country = "Brasil",
            PhoneNumber = "+55 41 3000-3000",
            TimeZone = "America/Sao_Paulo",
            Code = "MERCADO"
        };

        private static EditLocationRequest EditRequest => EditRequestWithCode("CENTRO");

        private static EditLocationRequest EditRequestWithCode(string code) => new()
        {
            Name = "Unidade Centro Histórico",
            Address = "Rua XV de Novembro, 300",
            City = "Curitiba",
            State = "PR",
            PostalCode = "80020-310",
            Country = "Brasil",
            PhoneNumber = "+55 41 3000-2000",
            Status = LocationStatus.Active,
            TimeZone = "America/Sao_Paulo",
            Code = code
        };

        private static EditOrganizationRequest EditOrganizationRequest => new()
        {
            Name = "Cantina da Nonna Batel",
            Description = "Cozinha italiana de família",
            ServiceChargeRate = 10m,
            DiscountApprovalLimit = 5m,
            BusinessDayCutoff = "04:00"
        };

        // Scenario 23
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "OR31")]
        public async Task EditLocationAsync_AdminForAllLocationsPlusWaiterAtBatelEditsCentro_EditsInEveryOrder(bool adminStoredFirst)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(InOrder(AllLocations(Roles.Admin), At(Roles.Waiter, BatelId), adminStoredFirst)));

            // Act
            var response = await sut.EditLocationAsync(JoaoId, CentroId, EditRequest);

            // Assert
            response.Name.ShouldBe("Unidade Centro Histórico", "an all-locations Admin may edit any location, also one the Waiter role does not cover");
        }

        // Scenario 24
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "OR32")]
        public async Task EditLocationAsync_WaiterForAllLocationsPlusAdminAtBatel_EditsBatelInEveryOrder(bool waiterStoredFirst)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(InOrder(AllLocations(Roles.Waiter), At(Roles.Admin, BatelId), waiterStoredFirst)));

            // Act
            var response = await sut.EditLocationAsync(JoaoId, BatelId, EditRequestWithCode("BATEL"));

            // Assert
            response.Id.ShouldBe(BatelId, "an Admin assigned to a location may edit that location whatever role is stored first");
        }

        // Scenario 25
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "OR32")]
        public async Task EditLocationAsync_WaiterForAllLocationsPlusAdminAtBatelEditsCentro_ThrowsForbiddenInEveryOrder(bool waiterStoredFirst)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(InOrder(AllLocations(Roles.Waiter), At(Roles.Admin, BatelId), waiterStoredFirst)));

            // Act
            var act = () => sut.EditLocationAsync(JoaoId, CentroId, EditRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin for one location may not edit another location");
            exception.StatusCode.ShouldBe(403, "a role for one location counts only at that location");
        }

        // Scenario 26
        [Fact]
        [Trait("Rule", "OR32")]
        public async Task DeleteLocationAsync_AdminAssignedOnlyToBatelClosesCentro_ThrowsForbiddenAndKeepsLocationActive()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(At(Roles.Admin, BatelId)));

            // Act
            var act = () => sut.DeleteLocationAsync(JoaoId, CentroId);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin for one location may not close another location");
            exception.StatusCode.ShouldBe(403, "a role for one location counts only at that location");
            using var checkContext = database.CreateContext(TestTenant.Of(CantinaId));
            checkContext.Locations.Single(l => l.Id == CentroId).Status.ShouldBe(LocationStatus.Active, "a refused close leaves the location active");
        }

        // Scenario 27
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "OR31")]
        public async Task CreateLocationAsync_AdminForAllLocationsPlusWaiterAtBatel_CreatesInEveryOrder(bool adminStoredFirst)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(InOrder(AllLocations(Roles.Admin), At(Roles.Waiter, BatelId), adminStoredFirst)));

            // Act
            var response = await sut.CreateLocationAsync(JoaoId, NewLocationRequest);

            // Assert
            response.Name.ShouldBe("Unidade Mercado Municipal", "an all-locations Admin may create locations whatever other role the user holds");
        }

        // Scenario 28
        [Fact]
        [Trait("Rule", "OR33")]
        public async Task CreateLocationAsync_AdminAssignedOnlyToBatel_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(At(Roles.Admin, BatelId)));

            // Act
            var act = () => sut.CreateLocationAsync(JoaoId, NewLocationRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin for one location may not create locations for the organization");
            exception.StatusCode.ShouldBe(403, "a role for one location never counts for an action on the whole organization");
        }

        // Scenario 29
        [Theory]
        [InlineData(Roles.Waiter, Roles.Cashier)]
        [InlineData(Roles.Cashier, Roles.Waiter)]
        [InlineData(Roles.Manager, Roles.Kitchen)]
        [InlineData(Roles.Kitchen, Roles.Manager)]
        [Trait("Rule", "OR34")]
        public async Task EditLocationAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder(string first, string second)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(AllLocations(first), AllLocations(second)));

            // Act
            var act = () => sut.EditLocationAsync(JoaoId, CentroId, EditRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "roles that do not include Owner or Admin never add up to managing locations");
            exception.StatusCode.ShouldBe(403, "a user without an Owner or Admin role is refused");
        }

        // Scenario 30
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "OR31")]
        public async Task EditOrganizationAsync_AdminForAllLocationsPlusWaiterAtBatel_EditsInEveryOrder(bool adminStoredFirst)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateOrganizationService(database, Holding(InOrder(AllLocations(Roles.Admin), At(Roles.Waiter, BatelId), adminStoredFirst)));

            // Act
            var response = await sut.EditOrganizationAsync(JoaoId, EditOrganizationRequest);

            // Assert
            response.Name.ShouldBe("Cantina da Nonna Batel", "an all-locations Admin may edit the organization whatever other role the user holds");
        }

        // Scenario 31
        [Fact]
        [Trait("Rule", "OR33")]
        public async Task EditOrganizationAsync_AdminAssignedOnlyToBatel_ThrowsForbidden()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateOrganizationService(database, Holding(At(Roles.Admin, BatelId)));

            // Act
            var act = () => sut.EditOrganizationAsync(JoaoId, EditOrganizationRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "an Admin for one location may not edit the organization");
            exception.StatusCode.ShouldBe(403, "a role for one location never counts for an action on the whole organization");
        }

        // Scenario 32
        [Theory]
        [InlineData(Roles.Waiter, Roles.Cashier)]
        [InlineData(Roles.Cashier, Roles.Waiter)]
        [Trait("Rule", "OR34")]
        public async Task EditOrganizationAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder(string first, string second)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateOrganizationService(database, Holding(AllLocations(first), AllLocations(second)));

            // Act
            var act = () => sut.EditOrganizationAsync(JoaoId, EditOrganizationRequest);

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>(customMessage: "roles that do not include Owner or Admin never add up to editing the organization");
            exception.StatusCode.ShouldBe(403, "a user without an Owner or Admin role is refused");
        }

        // Scenario 38
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Rule", "OR31")]
        public async Task DeleteLocationAsync_AdminForAllLocationsPlusWaiterAtBatelClosesCentro_ClosesInEveryOrder(bool adminStoredFirst)
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(InOrder(AllLocations(Roles.Admin), At(Roles.Waiter, BatelId), adminStoredFirst)));

            // Act
            await sut.DeleteLocationAsync(JoaoId, CentroId);

            // Assert
            using var checkContext = database.CreateContext(TestTenant.Of(CantinaId));
            checkContext.Locations.Single(l => l.Id == CentroId).Status.ShouldBe(LocationStatus.Closed, "an all-locations Admin may close any location, which is kept with status Closed");
        }

        // Scenario 37
        [Fact]
        [Trait("Rule", "OR35")]
        public async Task GetLocationsAsync_WaiterAssignedOnlyToOneLocation_ReturnsLocations()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedCantinaWithTwoLocationsAsync(database);
            var sut = CreateLocationService(database, Holding(At(Roles.Waiter, BatelId)));

            // Act
            var locations = await sut.GetLocationsAsync(JoaoId);

            // Assert
            locations.Count.ShouldBe(2, "any role of the organization, even for one location, may list its locations");
        }
    }
}
