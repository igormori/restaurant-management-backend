using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The migration that drops the redundant users.selected_organization_id (each refresh token now
/// carries its own organization). Raw SQL on purpose: the column no longer exists on the User entity.
/// </summary>
public class DropUserSelectedOrganizationMigrationTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string PreviousMigrationSuffix = "_AddPasswordResetCodes";
    private const string DropMigrationSuffix = "_DropUserSelectedOrganization";

    private const string UserRowsWithoutColumnSql = """
        SELECT coalesce(json_agg((to_jsonb(t) - 'selected_organization_id') ORDER BY t.id)::text, '[]')
        FROM users t
        """;
    private const string ColumnSql = """
        SELECT coalesce(string_agg(column_name, ','), '')
        FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'selected_organization_id'
        """;

    private static readonly Guid UserId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly DateTime CreationTime = new(2026, 10, 16, 18, 30, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public DropUserSelectedOrganizationMigrationTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    private static string FindMigration(DbContext context, string suffix)
    {
        var matches = context.Database.GetMigrations().Where(m => m.EndsWith(suffix)).ToList();
        matches.Count.ShouldBe(1, $"exactly one {suffix.TrimStart('_')} migration must exist");
        return matches[0];
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

    private async Task<(IdentityDbContext Context, string DropMigration)> CreateSeededDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;

        var context = new IdentityDbContext(options);
        var previous = FindMigration(context, PreviousMigrationSuffix);
        var drop = FindMigration(context, DropMigrationSuffix);
        await context.GetService<IMigrator>().MigrateAsync(previous);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, phone_number, password_hash, failed_attempts, is_active, is_verified,
                               verification_failed_attempts, created_at, updated_at, selected_organization_id)
            VALUES ({UserId}, 'maria.souza@cantina.com.br', 'Maria', 'Souza', '+55 11 98765-4321', 'hash-placeholder', 0, true, true,
                    0, {CreationTime}, {CreationTime}, {CantinaId})
            """);
        return (context, drop);
    }

    [Fact]
    public async Task Migrate_DropUserSelectedOrganization_UsersHasNoSelectedOrganizationColumn()
    {
        // Arrange
        var (context, drop) = await CreateSeededDatabaseAsync();
        await using var _ = context;

        // Act
        await context.GetService<IMigrator>().MigrateAsync(drop);

        // Assert
        (await ReadScalarAsync(context, ColumnSql)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Migrate_UsersWithSelectedOrganization_EveryOtherUserColumnUnchanged()
    {
        // Arrange
        var (context, drop) = await CreateSeededDatabaseAsync();
        await using var _ = context;
        var usersBefore = await ReadScalarAsync(context, UserRowsWithoutColumnSql);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(drop);

        // Assert
        (await ReadScalarAsync(context, UserRowsWithoutColumnSql)).ShouldBe(usersBefore);
    }
}
