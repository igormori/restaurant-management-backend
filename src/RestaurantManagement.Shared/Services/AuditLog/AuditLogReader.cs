using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Models;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Shared.Services.AuditLog
{
    public class AuditLogReader : IAuditLogReader
    {
        private const int DefaultPageSize = 50;
        private const int MaxPageSize = 100;

        private readonly AuditLogDbContext _context;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public AuditLogReader(AuditLogDbContext context, IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _localizer = localizer;
        }

        public async Task<AuditLogPageResponse> GetEntriesAsync(AuditLogQuery query)
        {
            Validate(query);

            var page = Math.Max(query.Page ?? 1, 1);
            var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

            // The tenant query filter limits this to the token's organization.
            var entries = _context.AuditLogEntries.AsNoTracking();
            if (query.From is { } from)
                entries = entries.Where(e => e.CreatedAt >= from.UtcDateTime);
            if (query.To is { } to)
                entries = entries.Where(e => e.CreatedAt <= to.UtcDateTime);
            if (!string.IsNullOrEmpty(query.Action))
                entries = entries.Where(e => e.Action == query.Action);

            var totalCount = await entries.CountAsync();
            var items = await entries
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new AuditLogPageResponse
            {
                Items = items.Select(MapToResponse).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private void Validate(AuditLogQuery query)
        {
            if (query.From > query.To)
                throw new BusinessException(_localizer["AuditLogInvalidDateRange"].Value, 400);
            if (!string.IsNullOrEmpty(query.Action) && !AuditActions.All.Contains(query.Action))
                throw new BusinessException(_localizer["AuditLogUnknownAction"].Value, 400);
        }

        private static AuditLogEntryResponse MapToResponse(AuditLogEntry entry) => new()
        {
            Id = entry.Id,
            LocationId = entry.LocationId,
            UserId = entry.CreatedByUserId,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Before = entry.Before,
            After = entry.After,
            Reason = entry.Reason,
            IpAddress = entry.IpAddress,
            CreatedAt = entry.CreatedAt
        };
    }
}
