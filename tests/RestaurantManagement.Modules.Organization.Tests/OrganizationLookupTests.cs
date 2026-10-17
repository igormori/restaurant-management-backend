using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Services;
using Shouldly;

namespace RestaurantManagement.Modules.Organization.Tests
{
    public class OrganizationLookupTests
    {
        private const string CantinaName = "Cantina da Nonna";
        private const string TrattoriaName = "Trattoria do Sol";
        private const string PizzariaName = "Pizzaria Vila Madalena";

        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid PizzariaId = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");
        private static readonly Guid UnknownId = Guid.Parse("d4d4d4d4-0000-4000-8000-000000000004");

        private static async Task SeedAsync(OrganizationTestDatabase database)
        {
            using var context = database.CreateContext();
            context.Organizations.AddRange(
                new Entities.Organization { Id = CantinaId, Name = CantinaName, Currency = Entities.Currency.BRL },
                new Entities.Organization { Id = TrattoriaId, Name = TrattoriaName, Currency = Entities.Currency.BRL, IsActive = false },
                new Entities.Organization { Id = PizzariaId, Name = PizzariaName, Currency = Entities.Currency.BRL });
            await context.SaveChangesAsync();
        }

        private static OrganizationLookup CreateSut(OrganizationDbContext context) => new(context);

        // Scenario 26
        [Fact]
        [Trait("Rule", "OR20")]
        public async Task GetOrganizationsAsync_RequestedIdsExist_ReturnsNamesAndActiveFlags()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database);
            using var context = database.CreateContext();

            // Act
            var result = await CreateSut(context).GetOrganizationsAsync(new[] { CantinaId, TrattoriaId });

            // Assert
            result.OrderBy(o => o.Name).Select(o => (o.Id, o.Name, o.IsActive)).ToList()
                .ShouldBe(new[] { (CantinaId, CantinaName, true), (TrattoriaId, TrattoriaName, false) });
        }

        // Scenario 27
        [Fact]
        [Trait("Rule", "OR20")]
        public async Task GetOrganizationsAsync_OrganizationNotRequested_IsNotReturned()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database);
            using var context = database.CreateContext();

            // Act
            var result = await CreateSut(context).GetOrganizationsAsync(new[] { CantinaId });

            // Assert
            result.Select(o => o.Id).ShouldBe(new[] { CantinaId });
        }

        // Scenario 28
        [Fact]
        [Trait("Rule", "OR20")]
        public async Task GetOrganizationsAsync_RequestedIdDoesNotExist_IsOmitted()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database);
            using var context = database.CreateContext();

            // Act
            var result = await CreateSut(context).GetOrganizationsAsync(new[] { PizzariaId, UnknownId });

            // Assert
            result.Select(o => o.Id).ShouldBe(new[] { PizzariaId });
        }

        // Scenario 29
        [Fact]
        [Trait("Rule", "OR20")]
        public async Task GetOrganizationsAsync_EmptyIdList_ReturnsEmptyList()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database);
            using var context = database.CreateContext();

            // Act
            var result = await CreateSut(context).GetOrganizationsAsync(Array.Empty<Guid>());

            // Assert
            result.ShouldBeEmpty();
        }

        // Scenario 30
        [Fact]
        [Trait("Rule", "OR20")]
        public async Task GetOrganizationsAsync_OrganizationCreatedWithoutSettingIsActive_ReportsActive()
        {
            // Arrange
            using var database = new OrganizationTestDatabase();
            await SeedAsync(database);
            using var context = database.CreateContext();

            // Act
            var result = await CreateSut(context).GetOrganizationsAsync(new[] { PizzariaId });

            // Assert
            result.Single().IsActive.ShouldBeTrue();
        }
    }
}
