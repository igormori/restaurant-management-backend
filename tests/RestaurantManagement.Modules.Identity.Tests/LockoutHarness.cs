using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Lockout;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Sessions;
using RestaurantManagement.Modules.Identity.Services.Lockout;
using RestaurantManagement.Modules.Identity.Services.Sessions;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Exceptions;
using Sentry;
using Shouldly;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Maria Souza owns Cantina da Nonna and signs in from different devices. The harness drives the real
    /// session service on SQLite so the login lock tests read like the story: wrong passwords from some
    /// addresses, then a sign-in from another. Limits default to the shipped ones (5 per address, 20 per
    /// account within 60 minutes, 15-minute locks) and each test can lower them.
    /// </summary>
    public sealed class LockoutHarness : IDisposable
    {
        public const string MariaEmail = "maria@cantinadanonna.com.br";
        public const string CorrectPassword = "Massa@Fresca2026";
        public const string WrongPassword = "Errada@Pizza2026";

        public static readonly ClientInfo Tablet = At("203.0.113.10");
        public static readonly ClientInfo Laptop = At("198.51.100.24");

        public IdentityTestDatabase Database { get; } = new();

        public int AddressMax { get; set; } = 5;
        public int AccountMax { get; set; } = 20;
        public IHub Hub { get; set; } = Substitute.For<IHub>();

        public static ClientInfo At(string? ipAddress) => new()
        {
            DeviceDescription = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4) Chrome/124.0",
            IpAddress = ipAddress
        };

        public static async Task<LockoutHarness> CreateAsync()
        {
            var harness = new LockoutHarness();
            await harness.AddUserAsync(MariaEmail, "Maria");
            return harness;
        }

        public async Task AddUserAsync(string email, string firstName)
        {
            using var context = Database.CreateContext();
            context.Users.Add(new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword, 4),
                FirstName = firstName,
                LastName = "Souza",
                IsVerified = true
            });
            await context.SaveChangesAsync();
        }

        public async Task<SessionResult> LoginAsync(ClientInfo client, string password = CorrectPassword, string email = MariaEmail)
        {
            using var context = Database.CreateContext();
            var jwt = Options.Create(new JwtOptions
            {
                Key = "test-signing-key-at-least-32-characters-long",
                Issuer = "RestaurantManagementTests",
                Audience = "RestaurantManagementTests",
                ExpireMinutes = 60,
                RefreshTokenExpireDays = 30
            });
            var sut = new SessionService(
                context,
                TestSecurityOptions.Create(maxFailedLoginAttempts: AddressMax, accountMaxFailedLoginAttempts: AccountMax),
                jwt,
                TestLocalizer.Create(),
                Hub,
                Substitute.For<RestaurantManagement.Shared.Services.Organization.IOrganizationLookup>(),
                new RefreshTokenService(context, jwt),
                TestPasswordHasher.Create());
            return await sut.LoginAsync(new LoginRequest { Email = email, Password = password }, client, null);
        }

        /// <summary>Wrong passwords from one address, each refused with the generic 401.</summary>
        public async Task FailAsync(ClientInfo client, int times, string email = MariaEmail)
        {
            for (var attempt = 0; attempt < times; attempt++)
                await Should.ThrowAsync<BusinessException>(() => LoginAsync(client, WrongPassword, email));
        }

        /// <summary>Wrong passwords spread over addresses 203.0.113.&lt;first&gt; upward, the given count from each.</summary>
        public async Task FailFromAddressesAsync(int addressCount, int timesEach, int firstHost = 101)
        {
            for (var host = firstHost; host < firstHost + addressCount; host++)
                await FailAsync(At($"203.0.113.{host}"), timesEach);
        }

        /// <summary>Moves every stored failure and the account lock back in time, as if that long had passed.</summary>
        public async Task RewindAsync(int minutes)
        {
            using var context = Database.CreateContext();
            var failures = await context.Set<LoginFailure>().ToListAsync();
            failures.ForEach(f =>
            {
                f.FailedAt = f.FailedAt.AddMinutes(-minutes);
                f.AddressLockedUntil = f.AddressLockedUntil?.AddMinutes(-minutes);
            });
            var users = await context.Users.ToListAsync();
            users.ForEach(u => u.LockedUntil = u.LockedUntil?.AddMinutes(-minutes));
            await context.SaveChangesAsync();
        }

        /// <summary>Moves the stored time of the last account-locked email back, as if that long had passed.</summary>
        public async Task RewindLastLockEmailAsync(int minutes)
        {
            using var context = Database.CreateContext();
            var maria = await context.Users.SingleAsync(u => u.Email == MariaEmail);
            var stored = context.Entry(maria).Property<DateTime?>("AccountLockedEmailAt");
            stored.CurrentValue = stored.CurrentValue?.AddMinutes(-minutes);
            await context.SaveChangesAsync();
        }

        /// <summary>Runs the account-locked email processor once, so the queued request is sent and removed.</summary>
        public async Task SendQueuedEmailsAsync()
        {
            using var context = Database.CreateContext();
            var processor = new AccountLockedEmailProcessor(
                context, TestSecurityOptions.Create(), Substitute.For<IEmailService>(), new CapturingLogger<AccountLockedEmailProcessor>());
            await processor.ProcessDueAsync(DateTime.UtcNow.AddMinutes(1), CancellationToken.None);
        }

        public async Task<List<LoginFailure>> ReadFailuresAsync()
        {
            using var context = Database.CreateContext();
            return await context.Set<LoginFailure>().AsNoTracking().ToListAsync();
        }

        public async Task<User> ReadUserAsync(string email = MariaEmail)
        {
            using var context = Database.CreateContext();
            return await context.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        }

        public async Task<List<PendingEmail>> ReadQueueAsync()
        {
            using var context = Database.CreateContext();
            return await context.PendingEmails.AsNoTracking().ToListAsync();
        }

        public void Dispose() => Database.Dispose();
    }
}
