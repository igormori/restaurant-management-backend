using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Services;
using Shouldly;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Other modules ask for one location through the lookup. Cantina da Nonna has Unidade Batel;
    /// Trattoria do Sol has Unidade Centro. Reads see only the token organization's rows.
    /// </summary>
    public class OrganizationLookupLocationTests
    {
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
        private static readonly Guid CentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
        private static readonly DateTime CreationTime = new(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

        private static Location Location(Guid id, Guid organizationId, string name, string code) => new()
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Address = "Rua XV de Novembro, 300",
            City = "Curitiba",
            State = "PR",
            PostalCode = "80020-310",
            Country = "Brasil",
            PhoneNumber = "+55 41 3000-2000",
            Status = LocationStatus.Active,
            TimeZone = "America/Sao_Paulo",
            Code = code,
            CreatedAt = CreationTime,
            UpdatedAt = CreationTime
        };

        private static async Task<OrganizationTestDatabase> SeedAsync()
        {
            var database = new OrganizationTestDatabase();
            using var context = database.CreateContext();
            context.Organizations.AddRange(
                new Entities.Organization { Id = CantinaId, Name = "Cantina da Nonna", Currency = Currency.BRL, CreatedAt = CreationTime, UpdatedAt = CreationTime },
                new Entities.Organization { Id = TrattoriaId, Name = "Trattoria do Sol", Currency = Currency.BRL, CreatedAt = CreationTime, UpdatedAt = CreationTime });
            context.Locations.AddRange(Location(BatelId, CantinaId, "Unidade Batel", "BATEL"), Location(CentroId, TrattoriaId, "Unidade Centro", "CENTRO"));
            await context.SaveChangesAsync();
            return database;
        }

        [Fact]
        [Trait("Rule", "OR44")]
        public async Task GetLocationAsync_LocationOfTheTokenOrganization_ReturnsItsName()
        {
            // Arrange
            using var database = await SeedAsync();
            using var context = database.CreateContext(TestTenant.Of(CantinaId));
            var sut = new OrganizationLookup(context);

            // Act
            var result = await sut.GetLocationAsync(BatelId);

            // Assert
            result.ShouldNotBeNull();
            result.Name.ShouldBe("Unidade Batel", "the invitation email names the location, so the lookup returns its name");
            result.OrganizationId.ShouldBe(CantinaId, "the location still reports its own organization");
        }
    }
}
