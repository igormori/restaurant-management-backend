using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class InvitationService : IInvitationService
    {
        private readonly IdentityDbContext _db;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly ITenantContext _tenantContext;
        private readonly ICurrentUser _currentUser;
        private readonly IMemberGridBuilder _gridBuilder;
        private readonly IMemberDelegationRules _rules;
        private readonly IAuditLog _auditLog;
        private readonly IEmailService _emailService;
        private readonly IOptions<InvitationOptions> _invitationOptions;
        private readonly IOptions<EmailOptions> _emailOptions;
        private readonly TimeProvider _timeProvider;
        private readonly ITenantScope _tenantScope;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<InvitationService> _logger;

        public InvitationService(
            IdentityDbContext db,
            IOrganizationLookup organizationLookup,
            ITenantContext tenantContext,
            ICurrentUser currentUser,
            IMemberGridBuilder gridBuilder,
            IMemberDelegationRules rules,
            IAuditLog auditLog,
            IEmailService emailService,
            IOptions<InvitationOptions> invitationOptions,
            IOptions<EmailOptions> emailOptions,
            TimeProvider timeProvider,
            ITenantScope tenantScope,
            IPasswordHasher passwordHasher,
            IStringLocalizer<SharedResource> localizer,
            ILogger<InvitationService> logger)
        {
            _db = db;
            _organizationLookup = organizationLookup;
            _tenantContext = tenantContext;
            _currentUser = currentUser;
            _gridBuilder = gridBuilder;
            _rules = rules;
            _auditLog = auditLog;
            _emailService = emailService;
            _invitationOptions = invitationOptions;
            _emailOptions = emailOptions;
            _timeProvider = timeProvider;
            _tenantScope = tenantScope;
            _passwordHasher = passwordHasher;
            _localizer = localizer;
            _logger = logger;
        }

        public Task<InvitationResponse> CreateAsync(CreateInvitationRequest request, string language)
            => throw new NotImplementedException();

        public Task CancelAsync(Guid invitationId) => throw new NotImplementedException();

        public Task AcceptAsync(AcceptInvitationRequest request) => throw new NotImplementedException();

        public Task AcceptWithNewAccountAsync(AcceptInvitationWithAccountRequest request) => throw new NotImplementedException();
    }
}
