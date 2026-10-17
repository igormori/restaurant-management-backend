using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Data;

/// <summary>
/// The automatic organization and location filter on every read of tenant data (Rules 1 and 2).
/// </summary>
public class TenantQueryFilterTests
{
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid BatelLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid MercadoLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
    private static readonly Guid TrattoriaCentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");

    private static readonly Guid FeijoadaId = Guid.Parse("d1d1d1d1-0000-4000-8000-000000000001");
    private static readonly Guid MoquecaId = Guid.Parse("d1d1d1d1-0000-4000-8000-000000000002");
    private static readonly Guid LasanhaId = Guid.Parse("d2d2d2d2-0000-4000-8000-000000000001");

    private static async Task<TenantFilterTestDatabase> CreateDatabaseWithDishesAsync()
    {
        var database = new TenantFilterTestDatabase();
        await database.SeedAsync(
            new TestDish { Id = FeijoadaId, OrganizationId = CantinaId, Name = "Feijoada", PriceInBrl = 89.90m },
            new TestDish { Id = MoquecaId, OrganizationId = CantinaId, Name = "Moqueca", PriceInBrl = 112.00m },
            new TestDish { Id = LasanhaId, OrganizationId = TrattoriaId, Name = "Lasanha", PriceInBrl = 74.50m });
        return database;
    }

    private static async Task<TenantFilterTestDatabase> CreateDatabaseWithStockCountsAsync()
    {
        var database = new TenantFilterTestDatabase();
        await database.SeedAsync(
            new TestStockCount { Id = Guid.NewGuid(), OrganizationId = CantinaId, LocationId = BatelLocationId, Ingredient = "Feijão preto", QuantityInGrams = 5000 },
            new TestStockCount { Id = Guid.NewGuid(), OrganizationId = CantinaId, LocationId = MercadoLocationId, Ingredient = "Arroz", QuantityInGrams = 12000 },
            new TestStockCount { Id = Guid.NewGuid(), OrganizationId = TrattoriaId, LocationId = TrattoriaCentroLocationId, Ingredient = "Farinha", QuantityInGrams = 8000 });
        return database;
    }

    // Scenario 1
    [Fact]
    [Trait("Rule", "SH4")]
    public async Task Query_NoCurrentOrganization_ReturnsNoRows()
    {
        // Arrange
        using var database = await CreateDatabaseWithDishesAsync();
        using var context = database.CreateContext(tokenOrganizationId: null);

        // Act
        var dishes = await context.Dishes.ToListAsync();

        // Assert
        dishes.ShouldBeEmpty(customMessage: "with no organization in the token no tenant data is visible");
    }

    // Scenario 2
    [Fact]
    [Trait("Rule", "SH4")]
    public async Task Query_RowsInTwoOrganizations_ReturnsOnlyCurrentOrganizationRows()
    {
        // Arrange
        using var database = await CreateDatabaseWithDishesAsync();
        using var context = database.CreateContext(CantinaId);

        // Act
        var dishes = await context.Dishes.OrderBy(d => d.Name).ToListAsync();

        // Assert
        dishes.Select(d => d.Id).ShouldBe(new[] { FeijoadaId, MoquecaId }, ignoreOrder: false, customMessage: "only the current organization dishes are visible");
    }

    // Scenario 3
    [Fact]
    [Trait("Rule", "SH4")]
    public async Task FindById_RowOfAnotherOrganization_ReturnsNull()
    {
        // Arrange
        using var database = await CreateDatabaseWithDishesAsync();
        using var context = database.CreateContext(CantinaId);

        // Act
        var dish = await context.Dishes.FirstOrDefaultAsync(d => d.Id == LasanhaId);

        // Assert
        dish.ShouldBeNull("a row of another organization cannot be found even by its id");
    }

    // Scenario 4
    [Fact]
    [Trait("Rule", "SH4")]
    public async Task Query_TwoContextsOnTheSameModel_EachSeesOnlyItsOwnOrganization()
    {
        // Arrange
        using var database = await CreateDatabaseWithDishesAsync();
        using var cantinaContext = database.CreateContext(CantinaId);
        using var trattoriaContext = database.CreateContext(TrattoriaId);
        var cantinaDishes = await cantinaContext.Dishes.Select(d => d.Id).ToListAsync();

        // Act
        var trattoriaDishes = await trattoriaContext.Dishes.Select(d => d.Id).ToListAsync();

        // Assert
        cantinaDishes.ShouldBe(new[] { FeijoadaId, MoquecaId }, ignoreOrder: true, customMessage: "the first organization sees only its own dishes");
        trattoriaDishes.ShouldBe(new[] { LasanhaId }, ignoreOrder: false, customMessage: "the second organization sees only its own dishes");
    }

    // Scenario 5
    [Fact]
    [Trait("Rule", "SH5")]
    public async Task Query_LocationSelected_ReturnsOnlyThatLocationsRows()
    {
        // Arrange
        using var database = await CreateDatabaseWithStockCountsAsync();
        using var context = database.CreateContext(CantinaId, selectedLocationId: BatelLocationId);

        // Act
        var counts = await context.StockCounts.ToListAsync();

        // Assert
        counts.Select(c => c.LocationId).ShouldBe(new[] { BatelLocationId }, ignoreOrder: false, customMessage: "with a location selected only that location rows are visible");
    }

    // Scenario 6
    [Fact]
    [Trait("Rule", "SH5")]
    public async Task Query_NoLocationSelected_ReturnsEveryLocationOfTheOrganizationOnly()
    {
        // Arrange
        using var database = await CreateDatabaseWithStockCountsAsync();
        using var context = database.CreateContext(CantinaId, selectedLocationId: null);

        // Act
        var counts = await context.StockCounts.ToListAsync();

        // Assert
        counts.Select(c => c.LocationId).ShouldBe(new[] { BatelLocationId, MercadoLocationId }, ignoreOrder: true, customMessage: "with no location selected all locations of the organization are visible and no other organization");
    }
}
