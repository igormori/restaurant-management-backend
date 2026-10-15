using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Organization.Data;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// One PostgreSQL container shared by the class; every test gets its own empty database.
/// </summary>
public sealed class AuditFieldsPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync()
    {
        var databaseName = "audit_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = databaseName }.ConnectionString;
    }
}

public class AuditFieldsMigrationTests : IClassFixture<AuditFieldsPostgresFixture>
{
    private const string AuditMigrationSuffix = "_AddAuditFields";
    // Each "before" name below must be the migration immediately preceding AddAuditFields in its module:
    // AddAuditFields must be the next migration after them, or these tests seed the wrong schema.
    private const string MenuBeforeAuditMigration = "20260917231406_InitialCreate";
    private const string OrganizationBeforeAuditMigration = "20261015050950_AddOrganizationIsActive";
    private const string IdentityBeforeAuditMigration = "20261015050952_AddUserSelectedOrganizationId";

    private static readonly Guid OrgA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid OrgB = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");
    private static readonly Guid DinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid LunchMenuId = Guid.Parse("55555555-0000-4000-8000-000000000002");
    private static readonly Guid DinnerLocationMenuId = Guid.Parse("66666666-0000-4000-8000-000000000001");
    private static readonly Guid LunchLocationMenuId = Guid.Parse("66666666-0000-4000-8000-000000000002");
    private static readonly Guid PastaCategoryId = Guid.Parse("77777777-0000-4000-8000-000000000001");
    private static readonly Guid CentroSettingsId = Guid.Parse("88888888-0000-4000-8000-000000000001");
    private static readonly Guid CentroLocationRowId = Guid.Parse("99999999-0000-4000-8000-000000000001");
    private static readonly Guid OwnerRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");

    private static readonly DateTime CreationTime = new(2026, 3, 14, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime LaterCreationTime = new(2026, 3, 16, 9, 15, 0, DateTimeKind.Utc);

    private readonly AuditFieldsPostgresFixture _fixture;

    public AuditFieldsMigrationTests(AuditFieldsPostgresFixture fixture) => _fixture = fixture;

    private async Task<DbContextOptions<T>> CreateOptionsAsync<T>() where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;

    private static async Task MigrateToAsync(DbContext context, string targetMigration)
        => await context.GetService<IMigrator>().MigrateAsync(targetMigration);

    private static string FindAuditMigration(DbContext context)
    {
        var matches = context.Database.GetMigrations().Where(m => m.EndsWith(AuditMigrationSuffix)).ToList();
        matches.Count.ShouldBe(1, "exactly one AddAuditFields migration must exist");
        return matches[0];
    }

    private static async Task<string> ReadIsNullableAsync(DbContext context, string table, string column)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State == System.Data.ConnectionState.Closed)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_nullable FROM information_schema.columns WHERE table_name = @t AND column_name = @c";
        command.Parameters.Add(new NpgsqlParameter("t", table));
        command.Parameters.Add(new NpgsqlParameter("c", column));
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SeedMenusAsync(MenuDbContext context)
    {
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO menus (id, organization_id, name, description, is_active, created_at, updated_at) VALUES
            ({DinnerMenuId}, {OrgA}, 'Cardápio de Jantar', 'Pratos da noite', true, {CreationTime}, {CreationTime}),
            ({LunchMenuId}, {OrgB}, 'Cardápio de Almoço', null, true, {LaterCreationTime}, {LaterCreationTime})
            """);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO location_menus (id, location_id, menu_id, is_active, created_at) VALUES
            ({DinnerLocationMenuId}, {CentroLocationId}, {DinnerMenuId}, true, {CreationTime}),
            ({LunchLocationMenuId}, {CentroLocationId}, {LunchMenuId}, true, {LaterCreationTime})
            """);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO menu_categories (id, menu_id, organization_id, location_id, name, description, sort_order, created_at) VALUES
            ({PastaCategoryId}, {DinnerMenuId}, {OrgA}, null, 'Massas', 'Massas frescas', 3, {CreationTime})
            """);
    }

    private async Task<(DbContextOptions<MenuDbContext> Options, string AuditMigration)> CreateMigratedMenuDatabaseWithRowsAsync()
    {
        var options = await CreateOptionsAsync<MenuDbContext>();
        await using var context = new MenuDbContext(options);
        var auditMigration = FindAuditMigration(context);
        await MigrateToAsync(context, MenuBeforeAuditMigration);
        await SeedMenusAsync(context);
        await MigrateToAsync(context, auditMigration);
        return (options, auditMigration);
    }

    // Scenario 1
    [Fact]
    public async Task Migrate_MenuTablesWithRows_MenusKeepTheirData()
    {
        // Arrange
        var (options, _) = await CreateMigratedMenuDatabaseWithRowsAsync();

        // Act
        await using var context = new MenuDbContext(options);
        var menus = await context.Menus.OrderBy(m => m.Name).ToListAsync();

        // Assert
        menus.Count.ShouldBe(2);
        var dinner = menus.Single(m => m.Id == DinnerMenuId);
        dinner.Name.ShouldBe("Cardápio de Jantar");
        dinner.Description.ShouldBe("Pratos da noite");
        dinner.OrganizationId.ShouldBe(OrgA);
        dinner.CreatedAt.ShouldBe(CreationTime);
        dinner.UpdatedAt.ShouldBe(CreationTime);
        dinner.CreatedByUserId.ShouldBeNull();
        dinner.UpdatedByUserId.ShouldBeNull();
    }

    // Scenario 2
    [Fact]
    public async Task Migrate_LocationMenusInTwoOrganizations_OrganizationIdBackfilledFromTheirMenu()
    {
        // Arrange
        var (options, _) = await CreateMigratedMenuDatabaseWithRowsAsync();

        // Act
        await using var context = new MenuDbContext(options);
        var locationMenus = await context.LocationMenus.ToListAsync();

        // Assert
        locationMenus.Single(lm => lm.Id == DinnerLocationMenuId).OrganizationId.ShouldBe(OrgA);
        locationMenus.Single(lm => lm.Id == LunchLocationMenuId).OrganizationId.ShouldBe(OrgB);
    }

    // Scenario 3
    [Fact]
    public async Task Migrate_LocationMenusWithRows_UpdatedAtBackfilledFromCreatedAtAndDataKept()
    {
        // Arrange
        var (options, _) = await CreateMigratedMenuDatabaseWithRowsAsync();

        // Act
        await using var context = new MenuDbContext(options);
        var lunch = await context.LocationMenus.SingleAsync(lm => lm.Id == LunchLocationMenuId);

        // Assert
        lunch.CreatedAt.ShouldBe(LaterCreationTime);
        lunch.UpdatedAt.ShouldBe(LaterCreationTime);
        lunch.LocationId.ShouldBe(CentroLocationId);
        lunch.MenuId.ShouldBe(LunchMenuId);
        lunch.IsActive.ShouldBeTrue();
    }

    // Scenario 4
    [Fact]
    public async Task Migrate_MenuCategoriesWithRows_UpdatedAtBackfilledFromCreatedAtAndDataKept()
    {
        // Arrange
        var (options, _) = await CreateMigratedMenuDatabaseWithRowsAsync();

        // Act
        await using var context = new MenuDbContext(options);
        var pasta = await context.MenuCategories.SingleAsync();

        // Assert
        pasta.Name.ShouldBe("Massas");
        pasta.Description.ShouldBe("Massas frescas");
        pasta.SortOrder.ShouldBe(3);
        pasta.OrganizationId.ShouldBe(OrgA);
        pasta.CreatedAt.ShouldBe(CreationTime);
        pasta.UpdatedAt.ShouldBe(CreationTime);
    }

    // Scenario 5
    [Fact]
    public async Task Migrate_MenuTablesWithRows_NewColumnsEndUpNotNull()
    {
        // Arrange
        var (options, _) = await CreateMigratedMenuDatabaseWithRowsAsync();

        // Act
        await using var context = new MenuDbContext(options);
        var locationMenuOrganization = await ReadIsNullableAsync(context, "location_menus", "organization_id");
        var locationMenuUpdatedAt = await ReadIsNullableAsync(context, "location_menus", "updated_at");
        var categoryUpdatedAt = await ReadIsNullableAsync(context, "menu_categories", "updated_at");

        // Assert
        locationMenuOrganization.ShouldBe("NO");
        locationMenuUpdatedAt.ShouldBe("NO");
        categoryUpdatedAt.ShouldBe("NO");
    }

    // Scenario 6
    [Fact]
    public async Task Migrate_MenuTablesWithoutRows_Succeeds()
    {
        // Arrange
        var options = await CreateOptionsAsync<MenuDbContext>();
        await using var context = new MenuDbContext(options);
        var auditMigration = FindAuditMigration(context);
        await MigrateToAsync(context, MenuBeforeAuditMigration);

        // Act
        await MigrateToAsync(context, auditMigration);

        // Assert
        (await context.Menus.CountAsync()).ShouldBe(0);
        (await context.LocationMenus.CountAsync()).ShouldBe(0);
        (await context.MenuCategories.CountAsync()).ShouldBe(0);
    }

    // Scenario 7
    [Fact]
    public async Task Migrate_OrganizationTablesWithRows_RowsKeepTheirDataWithNullAuditUsers()
    {
        // Arrange
        var options = await CreateOptionsAsync<OrganizationDbContext>();
        await using var context = new OrganizationDbContext(options);
        var auditMigration = FindAuditMigration(context);
        await MigrateToAsync(context, OrganizationBeforeAuditMigration);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO organizations (id, name, description, is_active, created_at, updated_at)
            VALUES ({OrgA}, 'Cantina da Nonna', 'Cozinha italiana', true, {CreationTime}, {CreationTime})
            """);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO organization_settings (id, organization_id, max_locations, plan_type, trial_end_date, is_trial_active, created_at, updated_at)
            VALUES ({CentroSettingsId}, {OrgA}, 1, 'TRIAL', {LaterCreationTime}, true, {CreationTime}, {CreationTime})
            """);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO locations (id, organization_id, name, address, city, state, postal_code, country, phone_number, status, created_at, updated_at)
            VALUES ({CentroLocationRowId}, {OrgA}, 'Unidade Centro', 'Rua Augusta, 1200', 'São Paulo', 'SP', '01304-001', 'Brasil', '+55 11 3000-1000', 0, {CreationTime}, {CreationTime})
            """);

        // Act
        await MigrateToAsync(context, auditMigration);

        // Assert
        var organization = await context.Organizations.SingleAsync();
        organization.Name.ShouldBe("Cantina da Nonna");
        organization.CreatedAt.ShouldBe(CreationTime);
        organization.CreatedByUserId.ShouldBeNull();
        organization.UpdatedByUserId.ShouldBeNull();
        var settings = await context.OrganizationSettings.SingleAsync();
        settings.OrganizationId.ShouldBe(OrgA);
        settings.PlanType.ShouldBe("TRIAL");
        settings.CreatedByUserId.ShouldBeNull();
        var location = await context.Locations.SingleAsync();
        location.Name.ShouldBe("Unidade Centro");
        location.OrganizationId.ShouldBe(OrgA);
        location.CreatedByUserId.ShouldBeNull();
        location.UpdatedByUserId.ShouldBeNull();
    }

    // Scenario 8
    [Fact]
    public async Task Migrate_UserRolesWithRows_RowsKeepTheirDataWithNullAuditUsers()
    {
        // Arrange
        var options = await CreateOptionsAsync<IdentityDbContext>();
        await using var context = new IdentityDbContext(options);
        var auditMigration = FindAuditMigration(context);
        await MigrateToAsync(context, IdentityBeforeAuditMigration);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, password_hash, failed_attempts, is_active, is_verified, verification_failed_attempts, created_at, updated_at)
            VALUES ({MariaId}, 'maria.souza@cantina.com.br', 'Maria', 'Souza', 'hash-placeholder', 0, true, true, 0, {CreationTime}, {CreationTime})
            """);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO user_roles (id, user_id, organization_id, location_id, role, created_at, updated_at)
            VALUES ({OwnerRoleId}, {MariaId}, {OrgA}, null, 'Owner', {CreationTime}, {CreationTime})
            """);

        // Act
        await MigrateToAsync(context, auditMigration);

        // Assert
        var role = await context.UserRoles.SingleAsync();
        role.UserId.ShouldBe(MariaId);
        role.OrganizationId.ShouldBe(OrgA);
        role.LocationId.ShouldBeNull();
        role.Role.ShouldBe("Owner");
        role.CreatedAt.ShouldBe(CreationTime);
        role.CreatedByUserId.ShouldBeNull();
        role.UpdatedByUserId.ShouldBeNull();
    }

    // Scenario 9
    [Fact]
    public async Task Migrate_AllMenuMigrations_ModelHasNoPendingChanges()
    {
        // Arrange
        var options = await CreateOptionsAsync<MenuDbContext>();
        await using var context = new MenuDbContext(options);

        // Act
        await context.Database.MigrateAsync();

        // Assert
        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    // Scenario 10
    [Fact]
    public async Task Migrate_AllOrganizationMigrations_ModelHasNoPendingChanges()
    {
        // Arrange
        var options = await CreateOptionsAsync<OrganizationDbContext>();
        await using var context = new OrganizationDbContext(options);

        // Act
        await context.Database.MigrateAsync();

        // Assert
        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    // Scenario 11
    [Fact]
    public async Task Migrate_AllIdentityMigrations_ModelHasNoPendingChanges()
    {
        // Arrange
        var options = await CreateOptionsAsync<IdentityDbContext>();
        await using var context = new IdentityDbContext(options);

        // Act
        await context.Database.MigrateAsync();

        // Assert
        context.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}
