using Shouldly;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Shared.Tests;

public class AuditSaveChangesInterceptorTests
{
    private const int ForbiddenStatusCode = 403;
    private const string TenantMismatchKey = "TenantMismatch";
    private const string OrganizationRequiredKey = "OrganizationRequired";

    private static readonly Guid OrgA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid OrgB = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid OrgC = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CarlosId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");

    private static readonly DateTimeOffset CreationTime = new(2026, 3, 14, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdateTime = new(2026, 3, 15, 11, 45, 0, TimeSpan.Zero);

    private static TestDish Margherita(Guid organizationId = default) => new()
    {
        Name = "Pizza Margherita",
        PriceInBrl = 54.90m,
        OrganizationId = organizationId
    };

    private static async Task<Guid> SeedDishAsync(AuditTestDatabase database, Guid organizationId)
    {
        using var seedContext = database.CreatePlainContext();
        var dish = new TestDish
        {
            Id = Guid.NewGuid(),
            Name = "Risoto de Cogumelos",
            PriceInBrl = 68.00m,
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

    private static async Task<Guid> SeedOrganizationAsync(AuditTestDatabase database)
    {
        using var seedContext = database.CreatePlainContext();
        var organization = new TestOrganization
        {
            Id = Guid.NewGuid(),
            Name = "Cantina da Nonna",
            CreatedAt = CreationTime.UtcDateTime,
            UpdatedAt = CreationTime.UtcDateTime,
            CreatedByUserId = MariaId,
            UpdatedByUserId = MariaId
        };
        seedContext.Organizations.Add(organization);
        await seedContext.SaveChangesAsync();
        return organization.Id;
    }

    // Scenario 1
    [Fact]
    public async Task SaveChangesAsync_NewTenantRowCarriesAnotherOrganizationId_Throws403AndWritesNothing()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Add(Margherita(OrgB));

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(TenantMismatchKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Count().ShouldBe(0);
    }

    // Scenario 2
    [Fact]
    public void SaveChanges_NewTenantRowCarriesAnotherOrganizationId_Throws403AndWritesNothing()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Add(Margherita(OrgB));

        // Act
        var thrown = Should.Throw<BusinessException>(() => context.SaveChanges());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Count().ShouldBe(0);
    }

    // Scenario 3
    [Fact]
    public async Task SaveChangesAsync_ValidOrganizationRowBatchedWithMismatchedDish_WritesNeitherRow()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        context.Organizations.Add(new TestOrganization { Name = "Trattoria Bella Vita" });
        context.Dishes.Add(Margherita(OrgB));

        // Act
        await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        using var readContext = database.CreatePlainContext();
        readContext.Organizations.Count().ShouldBe(0);
        readContext.Dishes.Count().ShouldBe(0);
    }

    // Scenario 4
    [Fact]
    public async Task SaveChangesAsync_ScopeOrganizationDiffersFromEntityOrganization_Throws403AndWritesNothing()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(
            CreationTime, userId: MariaId, userOrganizationId: OrgA, scopeOrganizationId: OrgC);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Add(Margherita(OrgA));

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(TenantMismatchKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Count().ShouldBe(0);
    }

    // Scenario 5
    [Fact]
    public async Task SaveChangesAsync_OrganizationIdChangedOnExistingRow_Throws403AndKeepsStoredOrganization()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedDishAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = context.Dishes.Single(d => d.Id == dishId);
        dish.OrganizationId = OrgB;

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(TenantMismatchKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Single(d => d.Id == dishId).OrganizationId.ShouldBe(OrgA);
    }

    // Scenario 6
    [Fact]
    public async Task SaveChangesAsync_NoCurrentOrganizationAndNoScope_Throws403AndWritesNothing()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId);
        using var context = database.CreateContext(interceptor);
        context.Dishes.Add(Margherita());

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(() => context.SaveChangesAsync());

        // Assert
        thrown.StatusCode.ShouldBe(ForbiddenStatusCode);
        thrown.Message.ShouldBe(OrganizationRequiredKey);
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Count().ShouldBe(0);
    }

    // Scenario 7
    [Fact]
    public async Task SaveChangesAsync_ScopeOpenWithDifferentUserOrganization_RowGetsScopeOrganization()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(
            CreationTime, userId: MariaId, userOrganizationId: OrgA, scopeOrganizationId: OrgC);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        dish.OrganizationId.ShouldBe(OrgC);
    }

    // Scenario 8
    [Fact]
    public async Task SaveChangesAsync_ScopeOpenAndUserHasNoOrganization_SavesUnderScopeOrganization()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(
            CreationTime, userId: MariaId, scopeOrganizationId: OrgC);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Single(d => d.Id == dish.Id).OrganizationId.ShouldBe(OrgC);
    }

    // Scenario 9
    [Fact]
    public async Task SaveChangesAsync_NewDishByUserInOrgA_GetsOrganizationAndCreatorOfOrgA()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        var stored = readContext.Dishes.Single(d => d.Id == dish.Id);
        stored.OrganizationId.ShouldBe(OrgA);
        stored.CreatedByUserId.ShouldBe(MariaId);
        stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
    }

    // Scenario 10
    [Fact]
    public async Task SaveChangesAsync_NewTenantRow_UpdatedAtEqualsCreatedAtAndUpdatedByEqualsCreatedBy()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        dish.UpdatedAt.ShouldBe(dish.CreatedAt);
        dish.UpdatedByUserId.ShouldBe(MariaId);
    }

    // Scenario 11
    [Fact]
    public async Task SaveChangesAsync_NewRowWithEmptyId_GetsNewId()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        dish.Id.ShouldNotBe(Guid.Empty);
    }

    // Scenario 12
    [Fact]
    public async Task SaveChangesAsync_NewRowWithPresetId_KeepsPresetId()
    {
        // Arrange
        var presetId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        dish.Id = presetId;
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        dish.Id.ShouldBe(presetId);
    }

    // Scenario 13
    [Fact]
    public async Task SaveChangesAsync_NewRowAlreadyCarriesCurrentOrganizationId_Saves()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita(OrgA);
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        readContext.Dishes.Single(d => d.Id == dish.Id).OrganizationId.ShouldBe(OrgA);
    }

    // Scenario 14
    [Fact]
    public async Task SaveChangesAsync_CallerSuppliesCreatedByUserId_OverwrittenWithCurrentUser()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        dish.CreatedByUserId = CarlosId;
        dish.UpdatedByUserId = CarlosId;
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        dish.CreatedByUserId.ShouldBe(MariaId);
        dish.UpdatedByUserId.ShouldBe(MariaId);
    }

    // Scenario 15
    [Fact]
    public void SaveChanges_NewDishByUserInOrgA_GetsOrganizationAndAuditFields()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        context.Dishes.Add(dish);

        // Act
        context.SaveChanges();

        // Assert
        dish.OrganizationId.ShouldBe(OrgA);
        dish.CreatedByUserId.ShouldBe(MariaId);
        dish.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
    }

    // Scenario 16
    [Fact]
    public async Task SaveChangesAsync_NewNonTenantRowWithNoOrganizationAndNoScope_SavesWithAuditFields()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId);
        using var context = database.CreateContext(interceptor);
        var organization = new TestOrganization { Name = "Cantina da Nonna" };
        context.Organizations.Add(organization);

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        var stored = readContext.Organizations.Single(o => o.Id == organization.Id);
        stored.CreatedByUserId.ShouldBe(MariaId);
        stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
    }

    // Scenario 17
    [Fact]
    public async Task SaveChangesAsync_NewNonTenantRowWithNoSignedInUser_SavesWithNullAuditUsers()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime);
        using var context = database.CreateContext(interceptor);
        var organization = new TestOrganization { Name = "Cantina da Nonna" };
        context.Organizations.Add(organization);

        // Act
        await context.SaveChangesAsync();

        // Assert
        organization.CreatedByUserId.ShouldBeNull();
        organization.UpdatedByUserId.ShouldBeNull();
    }

    // Scenario 18
    [Fact]
    public async Task SaveChangesAsync_ExistingDishModified_SetsUpdatedAtAndUpdatedByUserId()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedDishAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = context.Dishes.Single(d => d.Id == dishId);
        dish.PriceInBrl = 72.50m;

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        var stored = readContext.Dishes.Single(d => d.Id == dishId);
        stored.UpdatedAt.ShouldBe(UpdateTime.UtcDateTime);
        stored.UpdatedByUserId.ShouldBe(CarlosId);
    }

    // Scenario 19
    [Fact]
    public async Task SaveChangesAsync_ExistingDishModified_LeavesCreatedAtAndCreatedByUnchanged()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedDishAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = context.Dishes.Single(d => d.Id == dishId);
        dish.PriceInBrl = 72.50m;

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        var stored = readContext.Dishes.Single(d => d.Id == dishId);
        stored.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
        stored.CreatedByUserId.ShouldBe(MariaId);
    }

    // Scenario 20
    [Fact]
    public async Task SaveChangesAsync_ExistingNonTenantRowModifiedWithNoOrganization_SetsUpdatedAudit()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var organizationId = await SeedOrganizationAsync(database);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId);
        using var context = database.CreateContext(interceptor);
        var organization = context.Organizations.Single(o => o.Id == organizationId);
        organization.Name = "Cantina da Nonna Centro";

        // Act
        await context.SaveChangesAsync();

        // Assert
        using var readContext = database.CreatePlainContext();
        var stored = readContext.Organizations.Single(o => o.Id == organizationId);
        stored.UpdatedAt.ShouldBe(UpdateTime.UtcDateTime);
        stored.UpdatedByUserId.ShouldBe(CarlosId);
        stored.CreatedByUserId.ShouldBe(MariaId);
    }

    // Scenario 21
    [Fact]
    public async Task SaveChangesAsync_NewRowWithFixedClock_CreatedAtAndUpdatedAtEqualFixedTimeAsUtc()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var interceptor = AuditTestDatabase.CreateInterceptor(CreationTime, userId: MariaId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = Margherita();
        context.Dishes.Add(dish);

        // Act
        await context.SaveChangesAsync();

        // Assert
        dish.CreatedAt.ShouldBe(CreationTime.UtcDateTime);
        dish.UpdatedAt.ShouldBe(CreationTime.UtcDateTime);
        dish.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        dish.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    // Scenario 22
    [Fact]
    public async Task SaveChangesAsync_ModifiedRowWithFixedClock_UpdatedAtEqualsFixedTimeAsUtc()
    {
        // Arrange
        using var database = new AuditTestDatabase();
        var dishId = await SeedDishAsync(database, OrgA);
        var interceptor = AuditTestDatabase.CreateInterceptor(UpdateTime, userId: CarlosId, userOrganizationId: OrgA);
        using var context = database.CreateContext(interceptor);
        var dish = context.Dishes.Single(d => d.Id == dishId);
        dish.PriceInBrl = 72.50m;

        // Act
        await context.SaveChangesAsync();

        // Assert
        dish.UpdatedAt.ShouldBe(UpdateTime.UtcDateTime);
        dish.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
