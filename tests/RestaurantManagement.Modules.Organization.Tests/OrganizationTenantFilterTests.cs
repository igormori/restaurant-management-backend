using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Organization reads under the automatic tenant filter: other organizations' locations are hidden,
    /// while the two listed plan-details reads still see every organization the user belongs to.
    /// </summary>
    public class OrganizationTenantFilterTests
    {
        private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid TrattoriaCentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
        private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

        private static async Task SeedOrganizationAsync(OrganizationTestDatabase database, Guid organizationId, string name, string planType, int maxLocations)
        {
            using var context = database.CreateContext();
            context.Organizations.Add(new Entities.Organization { Id = organizationId, Name = name, Currency = Currency.BRL, CreatedAt = CreationTime, UpdatedAt = CreationTime });
            context.OrganizationSettings.Add(new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                PlanType = planType,
                MaxLocations = maxLocations,
                TrialEndDate = CreationTime.AddDays(30),
                IsTrialActive = planType == "TRIAL",
                CreatedAt = CreationTime,
                UpdatedAt = CreationTime
            });
            await context.SaveChangesAsync();
        }

        private static async Task SeedTrattoriaCentroAsync(OrganizationTestDatabase database)
        {
            using var context = database.CreateContext();
            context.Locations.Add(new Location
            {
                Id = TrattoriaCentroLocationId,
                OrganizationId = TrattoriaId,
                Name = "Unidade Centro",
                Address = "Rua XV de Novembro, 300",
                City = "Curitiba",
                State = "PR",
                PostalCode = "80020-310",
                Country = "Brasil",
                PhoneNumber = "+55 41 3000-2000",
                Status = LocationStatus.Active,
                TimeZone = "America/Sao_Paulo",
                Code = "CENTRO",
                CreatedAt = CreationTime,
                UpdatedAt = CreationTime
            });
            await context.SaveChangesAsync();
        }

        private static OrganizationService CreateService(Data.OrganizationDbContext context, IUserRoleLookup userRoleLookup, ITenantContext tenant)
            => new(context, userRoleLookup, Substitute.For<IUserRoleAssigner>(), new TenantScope(), tenant,
                TestLocalizer.Create(), Substitute.For<ILogger<OrganizationService>>(), Substitute.For<IAuditLog>());

        private static IUserRoleLookup AnaBelongsTo(params Guid[] organizationIds)
        {
            var userRoleLookup = Substitute.For<IUserRoleLookup>();
            userRoleLookup.UserExistsAsync(AnaId).Returns(true);
            userRoleLookup.GetOrganizationIdsForUserAsync(AnaId).Returns(organizationIds.ToList());
            return userRoleLookup;
        }

        // Scenario 19
        [Fact]
        [Trait("Rule", "OR14")]
        public async Task GetLocationAsync_LocationOfAnotherOrganization_ReturnsNull()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedOrganizationAsync(database, TrattoriaId, "Trattoria do Sol", "PRO", 5);
            await SeedTrattoriaCentroAsync(database);
            using var context = database.CreateContext(TestTenant.Of(CantinaId));
            var sut = new OrganizationLookup(context);

            // Act
            var location = await sut.GetLocationAsync(TrattoriaCentroLocationId);

            // Assert
            location.ShouldBeNull("another organization's location is not visible, as if it did not exist");
        }

        // Scenario 20
        [Fact]
        [Trait("Rule", "OR3")]
        public async Task CreateOrganizationAsync_TrialOwnerWithNoOrganizationInToken_ThrowsBadRequest()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedOrganizationAsync(database, CantinaId, "Cantina da Nonna", "TRIAL", 1);
            var tenant = TestTenant.None;
            using var context = database.CreateContext(tenant);
            var sut = CreateService(context, AnaBelongsTo(CantinaId), tenant);

            // Act
            var act = () => sut.CreateOrganizationAsync(AnaId, new CreateOrganizationRequest { Name = "Trattoria do Sol", Currency = "BRL" });

            // Assert
            var exception = await act.ShouldThrowAsync<BusinessException>();
            exception.StatusCode.ShouldBe(400, "a user who already owns a TRIAL organization cannot create another");
        }

        // Scenario 21
        [Fact]
        [Trait("Rule", "OR17")]
        public async Task GetOrganizationsAsync_SignedInToOneOfTwoOrganizations_ShowsPlanDetailsOfBoth()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedOrganizationAsync(database, CantinaId, "Cantina da Nonna", "TRIAL", 1);
            await SeedOrganizationAsync(database, TrattoriaId, "Trattoria do Sol", "PRO", 5);
            var tenant = TestTenant.Of(CantinaId);
            using var context = database.CreateContext(tenant);
            var sut = CreateService(context, AnaBelongsTo(CantinaId, TrattoriaId), tenant);

            // Act
            var organizations = await sut.GetOrganizationsAsync(AnaId);

            // Assert
            organizations.Single(o => o.Id == CantinaId).PlanType.ShouldBe("TRIAL", "each organization shows its own plan");
            organizations.Single(o => o.Id == TrattoriaId).PlanType.ShouldBe("PRO", "each organization shows its own plan");
            organizations.Single(o => o.Id == TrattoriaId).MaxLocations.ShouldBe(5, "each organization shows its own location limit");
        }
    }
}
