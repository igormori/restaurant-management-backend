using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Shared.Data
{
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private const int ForbiddenStatusCode = 403;
        private const string TenantMismatchKey = "TenantMismatch";
        private const string OrganizationRequiredKey = "OrganizationRequired";

        private readonly ICurrentUser _currentUser;
        private readonly ITenantScope _tenantScope;
        private readonly TimeProvider _clock;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public AuditSaveChangesInterceptor(
            ICurrentUser currentUser,
            ITenantScope tenantScope,
            TimeProvider clock,
            IStringLocalizer<SharedResource> localizer)
        {
            _currentUser = currentUser;
            _tenantScope = tenantScope;
            _clock = clock;
            _localizer = localizer;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is not null)
                ApplyAudit(eventData.Context);

            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not null)
                ApplyAudit(eventData.Context);

            return ValueTask.FromResult(result);
        }

        private void ApplyAudit(DbContext context)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var userId = _currentUser.UserId;
            var organizationId = _tenantScope.OrganizationId ?? _currentUser.OrganizationId;

            foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Added)
                    StampAdded(entry, now, userId, organizationId);
                else if (entry.State == EntityState.Modified)
                    StampModified(entry, now, userId, organizationId);
                else if (entry.State == EntityState.Deleted && entry.Entity is TenantEntity)
                    RequireOwnOrganization(entry, organizationId);
            }
        }

        private void StampAdded(EntityEntry<BaseEntity> entry, DateTime now, Guid? userId, Guid? organizationId)
        {
            if (entry.Entity is TenantEntity tenantEntity)
                AssignOrganization(tenantEntity, organizationId);

            var entity = entry.Entity;
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.CreatedByUserId = userId;
            entity.UpdatedByUserId = userId;
        }

        private void StampModified(EntityEntry<BaseEntity> entry, DateTime now, Guid? userId, Guid? organizationId)
        {
            if (entry.Entity is TenantEntity)
                RequireOwnOrganization(entry, organizationId);

            // Entities attached with Update() mark every property modified, which would overwrite the creation audit.
            entry.Property(e => e.CreatedAt).IsModified = false;
            entry.Property(e => e.CreatedByUserId).IsModified = false;

            entry.Entity.UpdatedAt = now;
            entry.Entity.UpdatedByUserId = userId;
        }

        private void AssignOrganization(TenantEntity entity, Guid? organizationId)
        {
            if (organizationId is null)
                throw Forbidden(OrganizationRequiredKey);

            if (entity.OrganizationId != Guid.Empty && entity.OrganizationId != organizationId)
                throw Forbidden(TenantMismatchKey);

            entity.OrganizationId = organizationId.Value;
        }

        // Both values are checked: an entity attached with Update() has no real original value,
        // so OriginalValue equals CurrentValue and a change alone would go unnoticed.
        private void RequireOwnOrganization(EntityEntry<BaseEntity> entry, Guid? organizationId)
        {
            if (organizationId is null)
                throw Forbidden(OrganizationRequiredKey);

            var property = entry.Property(nameof(TenantEntity.OrganizationId));
            if (!Equals(property.OriginalValue, organizationId.Value) || !Equals(property.CurrentValue, organizationId.Value))
                throw Forbidden(TenantMismatchKey);
        }

        private BusinessException Forbidden(string messageKey)
            => new(_localizer[messageKey].Value, ForbiddenStatusCode);
    }
}
