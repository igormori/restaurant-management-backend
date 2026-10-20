using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Services;
using Shouldly;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// The IDs of an organization's locations that are not Closed, which the access guard needs for
    /// actions that are not tied to one location. Cantina da Nonna has four locations, one in each
    /// status; Trattoria do Sol has one open location.
    /// </summary>
    public class OrganizationLookupNonClosedTests
    {
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid UnknownId = Guid.Parse("d4d4d4d4-0000-4000-8000-000000000004");
        private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
        private static readonly Guid MercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
        private static readonly Guid BariguiId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000004");
        private static readonly Guid CentroClosedId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000005");
        private static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
        private static readonly DateTime CreationTime = new(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

        private static Location Location(Guid id, Guid organizationId, string name, string code, LocationStatus status) => new()
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Address = "Av. do Batel, 1868",
            City = "Curitiba",
            State = "PR",
            PostalCode = "80420-090",
            Country = "Brasil",
            PhoneNumber = "+55 41 3000-2000",
            Status = status,
            TimeZone = "America/Sao_Paulo",
            Code = code,
            CreatedAt = CreationTime,
            UpdatedAt = CreationTime
        };

        private static async Task SeedAsync(OrganizationTestDatabase database)
        {
            using var context = database.CreateContext();
            context.Organizations.AddRange(
                new Entities.Organization { Id = CantinaId, Name = "Cantina da Nonna", Currency = Currency.BRL, CreatedAt = CreationTime, UpdatedAt = CreationTime },
                new Entities.Organization { Id = TrattoriaId, Name = "Trattoria do Sol", Currency = Currency.BRL, CreatedAt = CreationTime, UpdatedAt = CreationTime });
            context.Locations.AddRange(
                Location(BatelId, CantinaId, "Unidade Batel", "BATEL", LocationStatus.Active),
                Location(MercadoId, CantinaId, "Unidade Mercado Municipal", "MERCADO", LocationStatus.TemporarilyClosed),
                Location(BariguiId, CantinaId, "Unidade Barigui", "BARIGUI", LocationStatus.UnderConstruction),
                Location(CentroClosedId, CantinaId, "Unidade Centro", "CENTRO", LocationStatus.Closed),
                Location(TrattoriaCentroId, TrattoriaId, "Unidade Centro", "CENTRO", LocationStatus.Active));
            await context.SaveChangesAsync();
        }

        [Fact]
        [Trait("Rule", "OR38")]
        public async Task GetNonClosedLocationIdsAsync_LocationsInEveryStatus_ReturnsAllButTheClosedOne()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database);
            using var context = database.CreateContext(TestTenant.Of(CantinaId));
            var sut = new OrganizationLookup(context);

            // Act
            var ids = await sut.GetNonClosedLocationIdsAsync(CantinaId);

            // Assert
            ids.ShouldBe(new[] { BatelId, MercadoId, BariguiId }, ignoreOrder: true,
                customMessage: "Active, TemporarilyClosed and UnderConstruction locations count; a Closed location is left out");
        }

        [Fact]
        [Trait("Rule", "OR38")]
        public async Task GetNonClosedLocationIdsAsync_AnotherOrUnknownOrganization_ReturnsNothingOfTheirs()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database);
            using var context = database.CreateContext(TestTenant.Of(CantinaId));
            var sut = new OrganizationLookup(context);

            // Act
            var ownLocations = await sut.GetNonClosedLocationIdsAsync(CantinaId);
            var otherOrganizationsLocations = await sut.GetNonClosedLocationIdsAsync(TrattoriaId);
            var unknownOrganizationsLocations = await sut.GetNonClosedLocationIdsAsync(UnknownId);

            // Assert
            ownLocations.ShouldNotContain(TrattoriaCentroId, "another organization's location is never part of the list");
            otherOrganizationsLocations.ShouldBeEmpty("asking for another organization's locations returns nothing, as if it had none");
            unknownOrganizationsLocations.ShouldBeEmpty("an organization that does not exist has no locations");
        }
    }
}
