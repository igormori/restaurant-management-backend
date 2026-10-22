using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Shared.Data.Entities;

namespace RestaurantManagement.Shared.Data.Tenancy
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
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                if (entityType.BaseType is not null || !typeof(TenantEntity).IsAssignableFrom(clrType))
                    continue;

                entityType.SetQueryFilter(BuildFilter(clrType, tenantContext.Body));
            }

            return modelBuilder;
        }

        private static LambdaExpression BuildFilter(Type entityType, Expression tenant)
        {
            var entity = Expression.Parameter(entityType, "e");
            var filter = MatchesOrganization(entity, tenant);

            if (typeof(LocationEntity).IsAssignableFrom(entityType))
                filter = Expression.AndAlso(filter, MatchesLocation(entity, tenant));

            return Expression.Lambda(filter, entity);
        }

        // Comparing Guid? values means a missing organization equals no column value, so no row matches.
        private static Expression MatchesOrganization(Expression entity, Expression tenant)
            => Expression.Equal(
                Expression.Convert(Expression.Property(entity, nameof(TenantEntity.OrganizationId)), typeof(Guid?)),
                Expression.Property(tenant, nameof(ITenantContext.OrganizationId)));

        private static Expression MatchesLocation(Expression entity, Expression tenant)
        {
            var selected = Expression.Property(tenant, nameof(ITenantContext.LocationId));
            return Expression.OrElse(
                Expression.Equal(selected, Expression.Constant(null, typeof(Guid?))),
                Expression.Equal(
                    Expression.Convert(Expression.Property(entity, nameof(LocationEntity.LocationId)), typeof(Guid?)),
                    selected));
        }
    }
}
