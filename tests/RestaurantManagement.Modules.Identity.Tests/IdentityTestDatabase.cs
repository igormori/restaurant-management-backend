using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
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

        public IdentityDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptors)
                .Options;

            return new QueueAwareIdentityDbContext(options);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    /// <summary>
    /// The real context plus the pending reset request table, so tests can use the queue before the
    /// module's own configuration of it exists. Registering an entity twice is harmless.
    /// </summary>
    internal sealed class QueueAwareIdentityDbContext : IdentityDbContext
    {
        public QueueAwareIdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Entities.PasswordResetRequest>();
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

    /// <summary>
    /// Builds the real verification code hasher with a fixed test key, so tests can seed
    /// stored fingerprints and check what the services saved.
    /// </summary>
    public static class TestVerificationCodeHasher
    {
        public const string HashKey = "cantina-test-verification-hash-key-0123456789";

        public static IVerificationCodeHasher Create(string hashKey = HashKey)
        {
            return new VerificationCodeHasher(Options.Create(new VerificationCodeOptions { HashKey = hashKey }));
        }
    }

    /// <summary>The real password hasher, so tests seed and check hashes the way production does.</summary>
    public static class TestPasswordHasher
    {
        public static IPasswordHasher Create() => new BcryptPasswordHasher();
    }

    /// <summary>
    /// JWT (JSON Web Token) settings shared by the session and refresh token tests:
    /// access passes last 60 minutes, refresh tokens 30 days.
    /// </summary>
    public static class TestJwtOptions
    {
        public const int RefreshTokenExpireDays = 30;

        public static IOptions<JwtOptions> Create()
        {
            return Options.Create(new JwtOptions
            {
                Key = "test-signing-key-at-least-32-characters-long",
                Issuer = "RestaurantManagementTests",
                Audience = "RestaurantManagementTests",
                ExpireMinutes = 60,
                RefreshTokenExpireDays = RefreshTokenExpireDays
            });
        }
    }

    /// <summary>Devices a sign-in can come from: the kitchen tablet and the manager's laptop.</summary>
    public static class TestClients
    {
        public static readonly ClientInfo KitchenTablet = new()
        {
            DeviceDescription = "Mozilla/5.0 (iPad; CPU OS 17_4 like Mac OS X) Safari/604.1",
            IpAddress = "203.0.113.10"
        };

        public static readonly ClientInfo ManagerLaptop = new()
        {
            DeviceDescription = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4) Chrome/124.0",
            IpAddress = "198.51.100.24"
        };
    }
}
