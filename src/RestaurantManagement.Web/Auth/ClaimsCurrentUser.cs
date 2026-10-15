using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Web.Auth
{
    public sealed class ClaimsCurrentUser : ICurrentUser
    {
        public ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
        }

        public Guid? UserId => throw new NotImplementedException();

        public Guid? OrganizationId => throw new NotImplementedException();
    }
}
