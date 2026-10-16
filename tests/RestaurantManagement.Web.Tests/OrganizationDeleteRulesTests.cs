using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RestaurantManagement.Modules.Organization.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Delete rules between organizations and their locations and settings, enforced by PostgreSQL.
/// Deletes use raw SQL on purpose: the database must refuse them whatever the write path.
/// </summary>
public class OrganizationDeleteRulesTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string RestrictMigrationSuffix = "_RestrictOrganizationCascadeDeletes";
    private const string AuditMigrationSuffix = "_AddAuditFields";

    private const string OrganizationRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM organizations t";
    private const string SettingsRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM organization_settings t";
    private const string LocationRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM locations t";
    private const string DeleteRulesSql = """
        SELECT coalesce(string_agg(constraint_name || ':' || delete_rule, ',' ORDER BY constraint_name), '')
        FROM information_schema.referential_constraints
        """;

    private const int ActiveStatus = 0;
    private const int ClosedStatus = 1;

    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid BistroId = Guid.Parse("c2c2c2c2-0000-4000-8000-000000000002");
    private static readonly Guid CentroLocationId = Guid.Parse("d1d1d1d1-0000-4000-8000-000000000001");
    private static readonly Guid PinheirosLocationId = Guid.Parse("d2d2d2d2-0000-4000-8000-000000000002");
    private static readonly Guid CantinaSettingsId = Guid.Parse("e1e1e1e1-0000-4000-8000-000000000001");
    private static readonly Guid BistroSettingsId = Guid.Parse("e2e2e2e2-0000-4000-8000-000000000002");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");

    private static readonly DateTime CreationTime = new(2026, 3, 14, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime TrialEndDate = new(2026, 4, 13, 18, 30, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public OrganizationDeleteRulesTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    private async Task<DbContextOptions<OrganizationDbContext>> CreateOptionsAsync()
        => new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;

    private static string FindMigration(DbContext context, string suffix)
    {
        var matches = context.Database.GetMigrations().Where(m => m.EndsWith(suffix)).ToList();
        matches.Count.ShouldBe(1, $"exactly one {suffix.TrimStart('_')} migration must exist");
        return matches[0];
    }

    /// <summary>Requires the restrict migration to exist, then applies every migration.</summary>
    private async Task<OrganizationDbContext> CreateFullyMigratedContextAsync()
    {
        var context = new OrganizationDbContext(await CreateOptionsAsync(), NoTenant.Context);
        FindMigration(context, RestrictMigrationSuffix);
        await context.Database.MigrateAsync();
        return context;
    }

    private static async Task<string> ReadScalarAsync(DbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State == System.Data.ConnectionState.Closed)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task InsertOrganizationAsync(DbContext context, Guid id, string name)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO organizations (id, name, description, is_active, created_at, updated_at, created_by_user_id, updated_by_user_id)
            VALUES ({id}, {name}, 'Cozinha italiana', true, {CreationTime}, {CreationTime}, {MariaId}, {MariaId})
            """);

    private static async Task InsertSettingsAsync(DbContext context, Guid id, Guid organizationId)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO organization_settings (id, organization_id, max_locations, plan_type, trial_end_date, is_trial_active, created_at, updated_at, created_by_user_id, updated_by_user_id)
            VALUES ({id}, {organizationId}, 1, 'TRIAL', {TrialEndDate}, true, {CreationTime}, {CreationTime}, {MariaId}, null)
            """);

    private static async Task InsertLocationAsync(DbContext context, Guid id, Guid organizationId, string name, int status)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO locations (id, organization_id, name, address, city, state, postal_code, country, phone_number, status, created_at, updated_at, created_by_user_id, updated_by_user_id)
            VALUES ({id}, {organizationId}, {name}, 'Rua Augusta, 1200', 'São Paulo', 'SP', '01304-001', 'Brasil', '+55 11 3000-1000', {status}, {CreationTime}, {CreationTime}, {MariaId}, null)
            """);

    // Scenario 1
    [Fact]
    public async Task DeleteOrganization_HasActiveLocation_IsRefusedAndLocationKept()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertOrganizationAsync(context, CantinaId, "Cantina da Nonna");
        await InsertLocationAsync(context, CentroLocationId, CantinaId, "Unidade Centro", ActiveStatus);

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlAsync($"DELETE FROM organizations WHERE id = {CantinaId}"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        (await context.Locations.IgnoreQueryFilters().SingleAsync()).Id.ShouldBe(CentroLocationId);
    }

    // Scenario 2
    [Fact]
    public async Task DeleteOrganization_OnlyLocationIsClosed_IsRefusedAndLocationKept()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertOrganizationAsync(context, CantinaId, "Cantina da Nonna");
        await InsertLocationAsync(context, CentroLocationId, CantinaId, "Unidade Centro", ClosedStatus);

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlAsync($"DELETE FROM organizations WHERE id = {CantinaId}"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        (await context.Locations.IgnoreQueryFilters().SingleAsync()).Id.ShouldBe(CentroLocationId);
    }

    // Scenario 3
    [Fact]
    public async Task DeleteOrganization_HasSettingsAndNoLocations_IsRefusedAndSettingsKept()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertOrganizationAsync(context, CantinaId, "Cantina da Nonna");
        await InsertSettingsAsync(context, CantinaSettingsId, CantinaId);

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlAsync($"DELETE FROM organizations WHERE id = {CantinaId}"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        (await context.OrganizationSettings.IgnoreQueryFilters().SingleAsync()).Id.ShouldBe(CantinaSettingsId);
    }

    // Scenario 4
    [Fact]
    public async Task DeleteOrganization_NoLocationsAndNoSettings_IsDeleted()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertOrganizationAsync(context, CantinaId, "Cantina da Nonna");

        // Act
        var deleted = await context.Database.ExecuteSqlAsync($"DELETE FROM organizations WHERE id = {CantinaId}");

        // Assert
        deleted.ShouldBe(1);
        (await context.Organizations.CountAsync()).ShouldBe(0);
    }

    // Scenario 5
    [Fact]
    public async Task Migrate_OrganizationForeignKeys_AllRestrictDeletes()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var deleteRules = await ReadScalarAsync(context, DeleteRulesSql);

        // Assert
        deleteRules.ShouldBe(
            "fk_locations_organizations_organization_id:RESTRICT,"
            + "fk_organization_settings_organizations_organization_id:RESTRICT");
    }

    // Scenario 6
    [Fact]
    public async Task Migrate_OrganizationTablesWithRows_NoRowAddedChangedOrRemoved()
    {
        // Arrange
        await using var context = new OrganizationDbContext(await CreateOptionsAsync(), NoTenant.Context);
        var restrictMigration = FindMigration(context, RestrictMigrationSuffix);
        await context.GetService<IMigrator>().MigrateAsync(FindMigration(context, AuditMigrationSuffix));
        await InsertOrganizationAsync(context, CantinaId, "Cantina da Nonna");
        await InsertOrganizationAsync(context, BistroId, "Bistrô Paulista");
        await InsertSettingsAsync(context, CantinaSettingsId, CantinaId);
        await InsertSettingsAsync(context, BistroSettingsId, BistroId);
        await InsertLocationAsync(context, CentroLocationId, CantinaId, "Unidade Centro", ActiveStatus);
        await InsertLocationAsync(context, PinheirosLocationId, CantinaId, "Unidade Pinheiros", ClosedStatus);
        var organizationsBefore = await ReadScalarAsync(context, OrganizationRowsSql);
        var settingsBefore = await ReadScalarAsync(context, SettingsRowsSql);
        var locationsBefore = await ReadScalarAsync(context, LocationRowsSql);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(restrictMigration);

        // Assert
        (await ReadScalarAsync(context, OrganizationRowsSql)).ShouldBe(organizationsBefore);
        (await ReadScalarAsync(context, SettingsRowsSql)).ShouldBe(settingsBefore);
        (await ReadScalarAsync(context, LocationRowsSql)).ShouldBe(locationsBefore);
    }
}
