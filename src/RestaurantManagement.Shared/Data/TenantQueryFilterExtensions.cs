using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace RestaurantManagement.Shared.Data
{
    public static class TenantQueryFilterExtensions
    {
        /// <summary>
        /// Adds a query filter to every TenantEntity type (current organization) and, for LocationEntity
        /// types, also to the selected location when one is set. The DbContext passes an accessor to its
        /// own tenant context field, e.g. <c>modelBuilder.ApplyTenantQueryFilters(() =&gt; _tenantContext)</c>,
        /// so the values are read on every query and not captured when the model is built.
        /// </summary>
        public static ModelBuilder ApplyTenantQueryFilters(this ModelBuilder modelBuilder, Expression<Func<ITenantContext>> tenantContext)
            => throw new NotImplementedException();
    }
}
