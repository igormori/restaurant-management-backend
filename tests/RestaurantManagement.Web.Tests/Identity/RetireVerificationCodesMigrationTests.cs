using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The release migration that retires every verification code stored as readable text.
/// Rows are seeded right after the column rename (HashVerificationCodes), when code_hash still
/// holds the readable codes written before release, then the retire migration is applied.
/// </summary>
public class RetireVerificationCodesMigrationTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string HashMigrationSuffix = "_HashVerificationCodes";
    private const string RetireMigrationSuffix = "_RetireUnhashedVerificationCodes";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid MariaCodeId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001");
    private static readonly Guid JoaoCodeId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");
    private const string MariaReadableCode = "482913";
    private const string JoaoReadableCode = "915274";

    private static readonly DateTime CreationTime = new(2026, 10, 15, 13, 5, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public RetireVerificationCodesMigrationTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    private static string FindMigration(DbContext context, string suffix)
    {
        var matches = context.Database.GetMigrations().Where(m => m.EndsWith(suffix)).ToList();
        matches.Count.ShouldBe(1, $"exactly one {suffix.TrimStart('_')} migration must exist");
        return matches[0];
    }

    private static async Task InsertUnverifiedUserAsync(DbContext context, Guid id, string email, string firstName)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, password_hash, failed_attempts, is_active, is_verified, verification_failed_attempts, created_at, updated_at)
            VALUES ({id}, {email}, {firstName}, 'Souza', 'hash-placeholder', 0, true, false, 0, {CreationTime}, {CreationTime})
            """);

    private static async Task InsertReadableCodeAsync(DbContext context, Guid id, Guid userId, string code, bool isUsed)
    {
        // Still valid when the test runs, so a rejection can only come from the retire migration
        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO user_verification_codes (id, user_id, code_hash, expires_at, is_used, created_at)
            VALUES ({id}, {userId}, {code}, {expiresAt}, {isUsed}, {CreationTime})
            """);
    }

    /// <summary>Seeds an unused and a used readable code at the rename migration, then applies the retire migration.</summary>
    private async Task<DbContextOptions<IdentityDbContext>> CreateRetiredDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new IdentityDbContext(options);
        var hashMigration = FindMigration(context, HashMigrationSuffix);
        var retireMigration = FindMigration(context, RetireMigrationSuffix);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(hashMigration);
        await InsertUnverifiedUserAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria");
        await InsertUnverifiedUserAsync(context, JoaoId, "joao.lima@cantina.com.br", "João");
        await InsertReadableCodeAsync(context, MariaCodeId, MariaId, MariaReadableCode, isUsed: false);
        await InsertReadableCodeAsync(context, JoaoCodeId, JoaoId, JoaoReadableCode, isUsed: true);
        await migrator.MigrateAsync(retireMigration);
        await migrator.MigrateAsync(); // bring the schema to the current model before using today's services

        return options;
    }

    private static VerificationService CreateVerificationService(IdentityDbContext context)
    {
        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(callInfo => new LocalizedString(callInfo.Arg<string>(), callInfo.Arg<string>()));
        var hasher = new VerificationCodeHasher(Microsoft.Extensions.Options.Options.Create(
            new VerificationCodeOptions { HashKey = AuthApiFactory.TestVerificationCodeHashKey }));

        return new VerificationService(
            context,
            Microsoft.Extensions.Options.Options.Create(new SecurityOptions { MaxVerificationAttempts = 5, VerificationLockoutDurationMinutes = 15, VerificationCodeExpiryMinutes = 15, ResendCooldownSeconds = 60 }),
            localizer,
            Substitute.For<IEmailService>(),
            Substitute.For<ILogger<VerificationService>>(),
            hasher);
    }

    // Hash verification codes: Scenario 36
    [Fact]
    public async Task VerifyEmailAsync_CodeStoredBeforeRelease_IsRejected()
    {
        // Arrange
        var options = await CreateRetiredDatabaseAsync();
        await using var context = new IdentityDbContext(options);
        var sut = CreateVerificationService(context);

        // Act
        var thrown = await Should.ThrowAsync<BusinessException>(
            () => sut.VerifyEmailAsync(new VerifyEmailRequest { Email = "maria.souza@cantina.com.br", Code = MariaReadableCode }));

        // Assert
        thrown.StatusCode.ShouldBe(401);
        await using var verifyContext = new IdentityDbContext(options);
        (await verifyContext.Users.SingleAsync(u => u.Id == MariaId)).IsVerified.ShouldBeFalse();
    }

    // Hash verification codes: Scenario 37
    [Fact]
    public async Task Migrate_CodesStoredBeforeRelease_MarksEveryCodeUsed()
    {
        // Arrange
        var options = await CreateRetiredDatabaseAsync();

        // Act
        await using var context = new IdentityDbContext(options);
        var codes = await context.UserVerificationCodes.ToListAsync();

        // Assert
        codes.Count.ShouldBe(2);
        codes.ShouldAllBe(c => c.IsUsed);
    }

    // Hash verification codes: Scenario 38
    [Fact]
    public async Task Migrate_CodesStoredBeforeRelease_BlanksEveryStoredCode()
    {
        // Arrange
        var options = await CreateRetiredDatabaseAsync();

        // Act
        await using var context = new IdentityDbContext(options);
        var storedValues = await context.UserVerificationCodes.Select(c => c.CodeHash).ToListAsync();

        // Assert
        storedValues.Count.ShouldBe(2);
        storedValues.ShouldAllBe(value => value == string.Empty);
    }

    // Hash verification codes: Scenario 39
    [Fact]
    public async Task Migrate_CodesStoredBeforeRelease_KeepsCreatedAt()
    {
        // Arrange
        var options = await CreateRetiredDatabaseAsync();

        // Act
        await using var context = new IdentityDbContext(options);
        var mariaCode = await context.UserVerificationCodes.SingleAsync(c => c.Id == MariaCodeId);

        // Assert
        mariaCode.CreatedAt.ShouldBe(CreationTime);
    }
}
