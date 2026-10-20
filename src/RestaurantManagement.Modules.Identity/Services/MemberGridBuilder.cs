using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class MemberGridBuilder : IMemberGridBuilder
    {
        private readonly IdentityDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MemberGridBuilder(IdentityDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public Task<BuiltGrid> BuildAsync(
            Guid organizationId, IReadOnlyCollection<string> roleNames, IReadOnlyDictionary<AccessModule, AccessLevel> changedLevels)
            => throw new NotImplementedException();
    }
}
