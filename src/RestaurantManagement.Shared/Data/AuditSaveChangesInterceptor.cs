using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Shared.Data
{
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        public AuditSaveChangesInterceptor(
            ICurrentUser currentUser,
            ITenantScope tenantScope,
            TimeProvider clock,
            IStringLocalizer<SharedResource> localizer)
        {
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => throw new NotImplementedException();

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
