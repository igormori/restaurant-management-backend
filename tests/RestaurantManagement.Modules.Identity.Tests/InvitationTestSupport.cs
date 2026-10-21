using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Services.Organization;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>A clock a test can move forward.</summary>
    public sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public AdjustableTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    public sealed record SentEmail(string To, string Subject, string Html);

    /// <summary>
    /// Cantina da Nonna (Batel, Mercado Municipal) with its five default roles, Ana the Owner and Maria
    /// a member; Trattoria do Sol (Centro) with Paolo; Rita has an account and no organization.
    /// Builds the real <see cref="InvitationService"/> over SQLite with the real grid builder and
    /// delegation rules, a captured email service and audit log, and a clock set to 2026-10-20 12:00 UTC.
    /// </summary>
    public sealed class InvitationHarness : IDisposable
    {
        public const string AcceptPageUrl = "https://app.cantinadanonna.com.br/aceitar-convite";
        public const string ProductName = "Mesa Viva";
        public static readonly DateTimeOffset Start = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

        public IdentityTestDatabase Database { get; }

        public IEmailService Email { get; } = Substitute.For<IEmailService>();

        public IAuditLog Audit { get; } = Substitute.For<IAuditLog>();

        public AdjustableTimeProvider Clock { get; } = new(Start);

        public CapturingLogger<InvitationService> Logger { get; } = new();

        private readonly string _organizationName;

        private InvitationHarness(IdentityTestDatabase database, string organizationName)
        {
            Database = database;
            _organizationName = organizationName;
        }

        public static async Task<InvitationHarness> CreateAsync(string organizationName = "Cantina da Nonna", string? anaFirstName = null)
        {
            var database = await CreateDatabaseAsync("Admin", "Manager", "Cashier", "Waiter", "Kitchen");
            if (anaFirstName is not null)
            {
                using var context = database.CreateContext();
                context.Users.Single(u => u.Id == AnaId).FirstName = anaFirstName;
                await context.SaveChangesAsync();
            }

            return new InvitationHarness(database, organizationName);
        }

        public void Dispose() => Database.Dispose();

        private IOrganizationLookup Lookup()
        {
            var lookup = Substitute.For<IOrganizationLookup>();
            lookup.GetLocationAsync(BatelId).Returns(new LocationSummaryDto { Id = BatelId, OrganizationId = CantinaId, Name = "Batel" });
            lookup.GetLocationAsync(MercadoId).Returns(new LocationSummaryDto { Id = MercadoId, OrganizationId = CantinaId, Name = "Mercado Municipal" });
            lookup.GetLocationAsync(TrattoriaCentroId).Returns(new LocationSummaryDto { Id = TrattoriaCentroId, OrganizationId = TrattoriaId, Name = "Centro" });
            lookup.GetOrganizationsAsync(Arg.Any<IReadOnlyCollection<Guid>>()).Returns(callInfo =>
                callInfo.Arg<IReadOnlyCollection<Guid>>().Where(id => id == CantinaId)
                    .Select(id => new OrganizationSummaryDto { Id = id, Name = _organizationName, IsActive = true }).ToList());
            return lookup;
        }

        /// <summary>The service as a signed-in user of Cantina da Nonna (the token holds Cantina).</summary>
        public InvitationService As(Guid userId, IAuditLog? audit = null, IEmailService? email = null)
        {
            var currentUser = Substitute.For<ICurrentUser>();
            currentUser.UserId.Returns(userId);
            currentUser.OrganizationId.Returns(CantinaId);
            var context = Database.CreateContext();
            var localizer = TestLocalizer.Create();
            return new InvitationService(
                context, Lookup(), Tenant(CantinaId), currentUser,
                new MemberGridBuilder(context, localizer), new MemberDelegationRules(context, Tenant(CantinaId), currentUser, localizer),
                audit ?? Audit, email ?? Email,
                Options.Create(new InvitationOptions { AcceptPageUrl = AcceptPageUrl }),
                Options.Create(new EmailOptions { FromName = ProductName }),
                Clock, new TenantScope(), TestPasswordHasher.Create(), localizer, Logger);
        }

        /// <summary>The service for a visitor who is not signed in (accepting with a new account).</summary>
        public InvitationService Anonymous()
        {
            var currentUser = Substitute.For<ICurrentUser>();
            var context = Database.CreateContext();
            var localizer = TestLocalizer.Create();
            return new InvitationService(
                context, Lookup(), Substitute.For<ITenantContext>(), currentUser,
                new MemberGridBuilder(context, localizer), new MemberDelegationRules(context, Substitute.For<ITenantContext>(), currentUser, localizer),
                Audit, Email,
                Options.Create(new InvitationOptions { AcceptPageUrl = AcceptPageUrl }),
                Options.Create(new EmailOptions { FromName = ProductName }),
                Clock, new TenantScope(), TestPasswordHasher.Create(), localizer, Logger);
        }

        public static CreateInvitationRequest Invite(string email, params (Guid LocationId, string[] RoleNames)[] locations) => new()
        {
            Email = email,
            Locations = locations.Select(l => new InvitationLocationRequest { LocationId = l.LocationId, RoleNames = l.RoleNames.ToList() }).ToList()
        };

        /// <summary>Gives a member stored levels at a location, as a grid edit would.</summary>
        public async Task HoldAsync(Guid userId, Guid locationId, params (AccessModule Module, AccessLevel Level)[] levels)
        {
            using var context = Database.CreateContext();
            var now = new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);
            foreach (var (module, level) in levels)
                context.Set<MemberLocationLevel>().Add(new MemberLocationLevel
                {
                    Id = Guid.NewGuid(), OrganizationId = CantinaId, UserId = userId, LocationId = locationId,
                    Module = module, Level = level, CreatedAt = now, UpdatedAt = now
                });
            await context.SaveChangesAsync();
        }

        public List<SentEmail> SentEmails()
            => Email.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(IEmailService.SendEmailAsync))
                .Select(c => c.GetArguments())
                .Select(a => new SentEmail((string)a[0]!, (string)a[1]!, (string)a[2]!))
                .ToList();

        /// <summary>The one-time token carried by the link of the latest email sent to the address.</summary>
        public string LatestTokenSentTo(string email)
        {
            var html = SentEmails().Last(e => e.To == email).Html;
            var match = Regex.Match(html, @"token=([A-Za-z0-9_\-%.~]+)");
            return Uri.UnescapeDataString(match.Groups[1].Value);
        }

        public int InvitationCount()
        {
            using var context = Database.CreateContext();
            return context.Set<OrganizationInvitation>().Count();
        }

        public bool IsMemberOfCantina(Guid userId)
        {
            using var context = Database.CreateContext();
            return context.OrganizationMembers.Any(m => m.UserId == userId && m.OrganizationId == CantinaId);
        }

        public static IEnumerable<AuditEntryDto> AuditEntries(IAuditLog audit, string action)
            => audit.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(IAuditLog.RecordAsync))
                .Select(c => (AuditEntryDto)c.GetArguments()[0]!)
                .Where(e => e.Action == action);
    }
}
