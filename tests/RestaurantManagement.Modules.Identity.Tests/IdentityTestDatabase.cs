using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Opens a SQLite in-memory connection and builds an IdentityDbContext against it.
    /// Kept open for the lifetime of the instance so the in-memory database survives
    /// across multiple DbContext usages within a single test.
    /// </summary>
    public class IdentityTestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public IdentityTestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public IdentityDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new IdentityDbContext(options);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    /// <summary>
    /// Builds an IOptions&lt;SecurityOptions&gt; with explicit expiry/cooldown values for tests.
    /// </summary>
    public static class TestSecurityOptions
    {
        public static IOptions<SecurityOptions> Create(
            int verificationCodeExpiryMinutes = 15,
            int resendCooldownSeconds = 60,
            int maxFailedLoginAttempts = 5,
            int lockoutDurationMinutes = 15,
            int maxVerificationAttempts = 5,
            int verificationLockoutDurationMinutes = 15)
        {
            return Options.Create(new SecurityOptions
            {
                VerificationCodeExpiryMinutes = verificationCodeExpiryMinutes,
                ResendCooldownSeconds = resendCooldownSeconds,
                MaxFailedLoginAttempts = maxFailedLoginAttempts,
                LockoutDurationMinutes = lockoutDurationMinutes,
                MaxVerificationAttempts = maxVerificationAttempts,
                VerificationLockoutDurationMinutes = verificationLockoutDurationMinutes
            });
        }
    }
}
