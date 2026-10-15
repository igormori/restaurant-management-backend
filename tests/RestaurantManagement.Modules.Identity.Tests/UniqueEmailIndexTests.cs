using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Testcontainers.PostgreSql;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Verifies the database-level uniqueness constraint on email: that it survives a real
    /// concurrent race, and that the migration adding it applies cleanly to existing,
    /// non-duplicate data. Both require a real Postgres, not SQLite.
    /// </summary>
    public class UniqueEmailIndexTests : IClassFixture<IdentityPostgresFixture>
    {
        private readonly IdentityPostgresFixture _postgresFixture;

        public UniqueEmailIndexTests(IdentityPostgresFixture postgresFixture)
        {
            _postgresFixture = postgresFixture;
        }

        [Fact]
        public async Task RegisterAsync_TwoConcurrentRequestsSameEmail_CreatesOneUserAndRejectsOther()
        {
            // Arrange
            const string email = "concurrent@test.com";

            var emailService = Substitute.For<IEmailService>();
            var logger = Substitute.For<ILogger<RegistrationService>>();

            using var context1 = _postgresFixture.CreateContext();
            using var context2 = _postgresFixture.CreateContext();
            var sut1 = new RegistrationService(context1, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger);
            var sut2 = new RegistrationService(context2, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger);

            var request1 = new RegisterRequest { Email = email, Password = "Password1!", FirstName = "First", LastName = "Attempt" };
            var request2 = new RegisterRequest { Email = email, Password = "Password1!", FirstName = "Second", LastName = "Attempt" };

            // Act: fire both registrations at the same time so both can pass the AnyAsync
            // pre-check before either insert commits.
            var task1 = InvokeCapturingExceptionAsync(() => sut1.RegisterAsync(request1));
            var task2 = InvokeCapturingExceptionAsync(() => sut2.RegisterAsync(request2));
            var results = await Task.WhenAll(task1, task2);

            // Assert
            results.Should().ContainSingle(ex => ex != null && ex is BusinessException);
            results.Should().ContainSingle(ex => ex == null);

            using var verifyContext = _postgresFixture.CreateContext();
            (await verifyContext.Users.CountAsync(u => u.Email == email)).Should().Be(1);
        }

        [Fact]
        public async Task Migrate_DatabaseWithDistinctEmails_SucceedsAndKeepsRows()
        {
            // Arrange: a fresh database migrated up to just before the unique email index,
            // seeded with two rows that have distinct emails.
            await using var container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await container.StartAsync();

            var options = new DbContextOptionsBuilder<Data.IdentityDbContext>()
                .UseNpgsql(container.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options;

            await using (var context = new Data.IdentityDbContext(options))
            {
                var migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync("20260918020450_AddVerificationAttemptTracking");

                // Insert via raw SQL, not the ORM: the context's compiled model already
                // includes the email_normalized shadow column, which does not exist yet at
                // this migration point, so SaveChangesAsync would fail reading it back.
                foreach (var (email, firstName) in new[] { ("first@test.com", "First"), ("second@test.com", "Second") })
                {
                    await context.Database.ExecuteSqlInterpolatedAsync($@"
                        INSERT INTO users
                            (id, email, password_hash, first_name, last_name, is_active, is_verified,
                             failed_attempts, verification_failed_attempts, created_at, updated_at)
                        VALUES
                            ({Guid.NewGuid()}, {email}, 'hash', {firstName}, 'User', true, false,
                             0, 0, {DateTime.UtcNow}, {DateTime.UtcNow})");
                }
            }

            // Act: apply the remaining migrations, including the new unique email index.
            await using (var context = new Data.IdentityDbContext(options))
            {
                await context.Database.MigrateAsync();
            }

            // Assert
            await using var verifyContext = new Data.IdentityDbContext(options);
            var emails = await verifyContext.Users.Select(u => u.Email).ToListAsync();
            emails.Should().BeEquivalentTo(new[] { "first@test.com", "second@test.com" });
        }

        private static async Task<Exception?> InvokeCapturingExceptionAsync(Func<Task> action)
        {
            try
            {
                await action();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
