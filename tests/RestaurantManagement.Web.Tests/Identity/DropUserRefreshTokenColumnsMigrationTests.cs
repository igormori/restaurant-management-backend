using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The release migration that drops the old single refresh token from users. Rows are seeded right
/// after AddRefreshTokensTable, when users still hold an old token, then the drop migration is applied.
/// Raw SQL on purpose: the old columns no longer exist on the User entity.
/// </summary>
public class DropUserRefreshTokenColumnsMigrationTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string RefreshTokensMigrationSuffix = "_AddRefreshTokensTable";
    private const string DropColumnsMigrationSuffix = "_DropUserRefreshTokenColumns";

    // Every users column except the two being dropped, so values can be compared before and after.
    private const string UserRowsWithoutOldTokenSql = """
        SELECT coalesce(json_agg((to_jsonb(t) - 'refresh_token_hash' - 'refresh_token_expiry') ORDER BY t.id)::text, '[]')
        FROM users t
        """;
    private const string RefreshTokenRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM refresh_tokens t";
    private const string OldTokenColumnsSql = """
        SELECT coalesce(string_agg(column_name, ',' ORDER BY column_name), '')
        FROM information_schema.columns
        WHERE table_name = 'users' AND column_name IN ('refresh_token_hash', 'refresh_token_expiry')
        """;

    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid MariaTabletTokenId = Guid.Parse("dddddddd-0000-4000-8000-000000000001");

    private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime LastLogin = new(2026, 10, 15, 19, 45, 0, DateTimeKind.Utc);
    private static readonly DateTime OldTokenExpiry = new(2026, 11, 14, 19, 45, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public DropUserRefreshTokenColumnsMigrationTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

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

    private static async Task InsertUserWithOldTokenAsync(DbContext context, Guid id, string email, string firstName, bool isActive)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, phone_number, password_hash, failed_attempts, is_active, is_verified,
                               verification_failed_attempts, last_login_at, created_at, updated_at, refresh_token_hash, refresh_token_expiry, selected_organization_id)
            VALUES ({id}, {email}, {firstName}, 'Souza', '+55 11 98765-4321', 'hash-placeholder', 2, {isActive}, true,
                    0, {LastLogin}, {CreationTime}, {CreationTime}, '$2a$11$oldBcryptRefreshTokenHashForTheSingleDeviceXyz', {OldTokenExpiry}, {CantinaId})
            """);

    private static async Task InsertRefreshTokenAsync(DbContext context, Guid id, Guid userId)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO refresh_tokens (id, user_id, organization_id, family_id, token_hash, created_at, expires_at,
                                        revoked_at, revocation_reason, replaced_by_token_id, device_description, ip_address)
            VALUES ({id}, {userId}, {CantinaId}, {Guid.Parse("eeeeeeee-0000-4000-8000-000000000001")},
                    '9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08', {CreationTime}, {OldTokenExpiry},
                    null, null, null, 'Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1', '203.0.113.10')
            """);

    /// <summary>Seeds users holding an old token (and one new refresh token row) at AddRefreshTokensTable.</summary>
    private async Task<(IdentityDbContext Context, string DropMigration)> CreateSeededDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;

        var context = new IdentityDbContext(options);
        var refreshTokensMigration = FindMigration(context, RefreshTokensMigrationSuffix);
        var dropMigration = FindMigration(context, DropColumnsMigrationSuffix);
        await context.GetService<IMigrator>().MigrateAsync(refreshTokensMigration);
        await InsertUserWithOldTokenAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria", isActive: true);
        await InsertUserWithOldTokenAsync(context, JoaoId, "joao.lima@cantina.com.br", "João", isActive: false);
        await InsertRefreshTokenAsync(context, MariaTabletTokenId, MariaId);
        return (context, dropMigration);
    }

    // Scenario 67
    [Fact]
    [Trait("Rule", "D11")]
    public async Task Migrate_UsersHoldingOldToken_EveryOtherUserColumnUnchanged()
    {
        // Arrange
        var (context, dropMigration) = await CreateSeededDatabaseAsync();
        await using var _ = context;
        var usersBefore = await ReadScalarAsync(context, UserRowsWithoutOldTokenSql);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(dropMigration);

        // Assert
        (await ReadScalarAsync(context, UserRowsWithoutOldTokenSql)).ShouldBe(usersBefore);
    }

    // Scenario 68
    [Fact]
    [Trait("Rule", "D10")]
    public async Task Migrate_DropUserRefreshTokenColumns_UsersHasNoOldTokenColumns()
    {
        // Arrange
        var (context, dropMigration) = await CreateSeededDatabaseAsync();
        await using var _ = context;

        // Act
        await context.GetService<IMigrator>().MigrateAsync(dropMigration);

        // Assert
        (await ReadScalarAsync(context, OldTokenColumnsSql)).ShouldBeEmpty();
    }

    // Scenario 69
    [Fact]
    [Trait("Rule", "D12")]
    public async Task Migrate_RefreshTokensWithRows_RowsUnchanged()
    {
        // Arrange
        var (context, dropMigration) = await CreateSeededDatabaseAsync();
        await using var _ = context;
        var tokensBefore = await ReadScalarAsync(context, RefreshTokenRowsSql);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(dropMigration);

        // Assert
        (await ReadScalarAsync(context, RefreshTokenRowsSql)).ShouldBe(tokensBefore);
    }
}
