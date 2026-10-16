using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Options;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The refresh_tokens table on a real PostgreSQL: unique fingerprints, cascade delete with the user,
/// the migration on existing users, and parallel rotation of one token.
/// User deletes use raw SQL on purpose: the database must enforce them whatever the write path.
/// </summary>
public class RefreshTokensTableTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string RetireMigrationSuffix = "_RetireUnhashedVerificationCodes";
    private const string RefreshTokensMigrationSuffix = "_AddRefreshTokensTable";

    private const string UserRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM users t";

    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("c2c2c2c2-0000-4000-8000-000000000002");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid MariaTabletTokenId = Guid.Parse("dddddddd-0000-4000-8000-000000000001");
    private static readonly Guid MariaLaptopTokenId = Guid.Parse("dddddddd-0000-4000-8000-000000000002");
    private static readonly Guid JoaoTokenId = Guid.Parse("dddddddd-0000-4000-8000-000000000003");

    private const string TabletFingerprint = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
    private const string LaptopFingerprint = "60303ae22b998861bce3b28f33eec1be758a213c86c93c076dbe9f558c11c752";
    private const string JoaoFingerprint = "fd61a03af4f77d870fc21e05e7e80678095c92d808cfb3b5c279ee04c74aca13";

    private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public RefreshTokensTableTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    private async Task<DbContextOptions<IdentityDbContext>> CreateOptionsAsync()
        => new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;

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

    private static async Task InsertUserAsync(DbContext context, Guid id, string email, string firstName)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, password_hash, failed_attempts, is_active, is_verified, verification_failed_attempts, created_at, updated_at)
            VALUES ({id}, {email}, {firstName}, 'Souza', 'hash-placeholder', 0, true, true, 0, {CreationTime}, {CreationTime})
            """);

    private static async Task InsertTokenAsync(IdentityDbContext context, Guid id, Guid userId, Guid? organizationId, string fingerprint)
    {
        context.Set<RefreshToken>().Add(new RefreshToken
        {
            Id = id,
            UserId = userId,
            OrganizationId = organizationId,
            FamilyId = Guid.NewGuid(),
            TokenHash = fingerprint,
            CreatedAt = CreationTime,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            DeviceDescription = "Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1",
            IpAddress = "203.0.113.10"
        });
        await context.SaveChangesAsync();
    }

    private async Task<IdentityDbContext> CreateMigratedContextWithMariaAndJoaoAsync()
    {
        var context = new IdentityDbContext(await CreateOptionsAsync());
        await context.Database.MigrateAsync();
        await InsertUserAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria");
        await InsertUserAsync(context, JoaoId, "joao.lima@trattoria.com.br", "João");
        return context;
    }

    // Scenario 1
    [Fact]
    [Trait("Rule", "T8")]
    public async Task InsertRefreshToken_SameFingerprintTwice_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAndJoaoAsync();
        await InsertTokenAsync(context, MariaTabletTokenId, MariaId, CantinaId, TabletFingerprint);
        context.ChangeTracker.Clear();

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(
            () => InsertTokenAsync(context, JoaoTokenId, JoaoId, TrattoriaId, TabletFingerprint));

        // Assert
        exception.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    // Scenario 2
    [Fact]
    [Trait("Rule", "T12")]
    public async Task DeleteUser_HasRefreshTokens_UserAndTokensDeleted()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAndJoaoAsync();
        await InsertTokenAsync(context, MariaTabletTokenId, MariaId, CantinaId, TabletFingerprint);
        await InsertTokenAsync(context, MariaLaptopTokenId, MariaId, null, LaptopFingerprint);

        // Act
        var deleted = await context.Database.ExecuteSqlAsync($"DELETE FROM users WHERE id = {MariaId}");

        // Assert
        deleted.ShouldBe(1);
        (await context.Set<RefreshToken>().CountAsync(t => t.UserId == MariaId)).ShouldBe(0);
    }

    // Scenario 3
    [Fact]
    [Trait("Rule", "T12")]
    public async Task DeleteUser_AnotherUserHasRefreshToken_OtherUsersTokenKept()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAndJoaoAsync();
        await InsertTokenAsync(context, MariaTabletTokenId, MariaId, CantinaId, TabletFingerprint);
        await InsertTokenAsync(context, JoaoTokenId, JoaoId, TrattoriaId, JoaoFingerprint);

        // Act
        await context.Database.ExecuteSqlAsync($"DELETE FROM users WHERE id = {MariaId}");

        // Assert
        var remaining = await context.Set<RefreshToken>().AsNoTracking().SingleAsync();
        remaining.Id.ShouldBe(JoaoTokenId);
        remaining.UserId.ShouldBe(JoaoId);
    }

    // Scenario 5
    [Fact]
    [Trait("Rule", "D5")]
    public async Task Migrate_AddRefreshTokensTableOnExistingUsers_NoUserRowChanged()
    {
        // Arrange
        await using var context = new IdentityDbContext(await CreateOptionsAsync());
        var refreshTokensMigration = FindMigration(context, RefreshTokensMigrationSuffix);
        await context.GetService<IMigrator>().MigrateAsync(FindMigration(context, RetireMigrationSuffix));
        await InsertUserAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria");
        await InsertUserAsync(context, JoaoId, "joao.lima@trattoria.com.br", "João");
        var usersBefore = await ReadScalarAsync(context, UserRowsSql);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(refreshTokensMigration);

        // Assert
        (await ReadScalarAsync(context, UserRowsSql)).ShouldBe(usersBefore);
    }

    // Scenario 7
    [Fact]
    [Trait("Rule", "F3")]
    public async Task RotateAsync_SameTokenTwiceInParallel_ExactlyOneSucceedsAndFamilyRevoked()
    {
        // Arrange
        var options = await CreateOptionsAsync();
        var jwtOptions = Options.Create(new JwtOptions { RefreshTokenExpireDays = 30 });
        string rawToken;
        await using (var setupContext = new IdentityDbContext(options))
        {
            await setupContext.Database.MigrateAsync();
            await InsertUserAsync(setupContext, MariaId, "maria.souza@cantina.com.br", "Maria");
            var issued = await new RefreshTokenService(setupContext, jwtOptions)
                .IssueAsync(MariaId, CantinaId, "Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1", "203.0.113.10");
            rawToken = issued.Token;
        }

        await using var tabletContext = new IdentityDbContext(options);
        await using var stolenCopyContext = new IdentityDbContext(options);
        var tabletService = new RefreshTokenService(tabletContext, jwtOptions);
        var stolenCopyService = new RefreshTokenService(stolenCopyContext, jwtOptions);

        // Act
        var results = await Task.WhenAll(
            tabletService.RotateAsync(rawToken),
            stolenCopyService.RotateAsync(rawToken));

        // Assert: the second use counts as reuse (Rule 5), so the whole family ends up revoked
        results.Count(r => r != null).ShouldBe(1);
        await using var assertContext = new IdentityDbContext(options);
        var family = await assertContext.Set<RefreshToken>().AsNoTracking().Where(t => t.UserId == MariaId).ToListAsync();
        family.ShouldAllBe(t => t.RevokedAt != null);
        family.ShouldContain(t => t.RevocationReason == RefreshToken.ReasonReuseDetected);
    }

    // Scenario 8
    [Fact]
    [Trait("Rule", "D6")]
    public void ServiceProvider_RefreshTokenService_IsRegisteredForTheApp()
    {
        // Arrange
        using var factory = new AuthApiFactory();
        using var scope = factory.Services.CreateScope();

        // Act
        var service = scope.ServiceProvider.GetService<IRefreshTokenService>();

        // Assert
        service.ShouldBeOfType<RefreshTokenService>();
    }
}
