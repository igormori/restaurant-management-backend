using Shouldly;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Shared.Tests;

public class AuditSaveChangesInterceptorExistingRowTests
{
    private const int ForbiddenStatusCode = 403;
    private const string TenantMismatchKey = "TenantMismatch";
    private const string OrganizationRequiredKey = "OrganizationRequired";
    private const string RisottoName = "Risoto de Cogumelos";
    private const decimal RisottoPriceInBrl = 68.00m;
    private const decimal NewRisottoPriceInBrl = 72.50m;

    private static readonly Guid OrgA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid OrgB = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CarlosId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");

    private static readonly DateTimeOffset CreationTime = new(2026, 3, 14, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdateTime = new(2026, 3, 15, 11, 45, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedRisottoAsync(AuditTestDatabase database, Guid organizationId)
    {
        using var seedContext = database.CreatePlainContext();
        var dish = new TestDish
        {
            Id = Guid.NewGuid(),
            Name = RisottoName,
            PriceInBrl = RisottoPriceInBrl,
            OrganizationId = organizationId,
            CreatedAt = CreationTime.UtcDateTime,
            UpdatedAt = CreationTime.UtcDateTime,
            CreatedByUserId = MariaId,
            UpdatedByUserId = MariaId
        };
        seedContext.Dishes.Add(dish);
        await seedContext.SaveChangesAsync();
        return dish.Id;
    }

    private static TestDish DetachedRisotto(Guid dishId, Guid organizationId) => new()
    {
        Id = dishId,
        Name = RisottoName,
        PriceInBrl = NewRisottoPriceInBrl,
        OrganizationId = organizationId
    };

    [Fact]
    [Trait("Rule", "SH8")]
    public async Task SaveChangesAsync_DetachedDishUpdatedWithAnotherOrganizationId_Throws403AndKeepsStoredOrganization()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedRisottoAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Update(DetachedRisotto(dishId, OrgB));

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(TenantMismatchKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Single(d => d.Id == dishId).OrganizationId.ShouldBe(OrgA);
    }

    [Fact]
    [Trait("Rule", "SH8")]
    public async Task SaveChangesAsync_DishOfAnotherOrganizationModified_Throws403AndKeepsStoredPrice()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedRisottoAsync(database, OrgB);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Single(d => d.Id == dishId).PriceInBrl = NewRisottoPriceInBrl;

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(TenantMismatchKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Single(d => d.Id == dishId).PriceInBrl.ShouldBe(RisottoPriceInBrl);
    }

    [Fact]
    [Trait("Rule", "SH8")]
    public async Task SaveChangesAsync_DishOfAnotherOrganizationRemoved_Throws403AndKeepsRow()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedRisottoAsync(database, OrgB);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Remove(context.Dishes.Single(d => d.Id == dishId));

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(TenantMismatchKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Count(d => d.Id == dishId).ShouldBe(1);
    }

    [Fact]
    [Trait("Rule", "SH8")]
    public async Task SaveChangesAsync_ExistingDishModifiedWithNoOrganizationAndNoScope_Throws403AndKeepsStoredPrice()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedRisottoAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Single(d => d.Id == dishId).PriceInBrl = NewRisottoPriceInBrl;

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(OrganizationRequiredKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Single(d => d.Id == dishId).PriceInBrl.ShouldBe(RisottoPriceInBrl);
    }

    [Fact]
    [Trait("Rule", "SH9")]
    public async Task SaveChangesAsync_DetachedDishUpdated_KeepsStoredCreatedAtAndCreatedBy()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedRisottoAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Update(DetachedRisotto(dishId, OrgA));

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        var stored = readContext.Dishes.Single(d => d.Id == dishId);
        stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
        stored.CreatedByUserId.ShouldBe(MariaId);
    }

    [Fact]
    [Trait("Rule", "SH8")]
    public async Task SaveChangesAsync_OwnDishRemoved_DeletesRow()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedRisottoAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Remove(context.Dishes.Single(d => d.Id == dishId));

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Count(d => d.Id == dishId).ShouldBe(0);
    }
}
