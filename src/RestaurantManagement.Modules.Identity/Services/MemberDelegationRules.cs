using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class MemberDelegationRules : IMemberDelegationRules
    {
        private readonly IdentityDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly ICurrentUser _currentUser;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MemberDelegationRules(
            IdentityDbContext db, ITenantContext tenantContext, ICurrentUser currentUser, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _tenantContext = tenantContext;
            _currentUser = currentUser;
            _localizer = localizer;
        }

        public Task RequireAsync(Guid? targetUserId, IReadOnlyCollection<LocationGrant> grants)
            => throw new NotImplementedException();
    }
}
