using RestaurantManagement.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Npgsql;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Organization.Services
{
    public class LocationService : ILocationService
    {
        private readonly OrganizationDbContext _orgDb;
        private readonly IAccessGuard _accessGuard;
        private readonly ITenantContext _tenantContext;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly IAuditLog _auditLog;
        private readonly ILogger<LocationService> _logger;

        public LocationService(
            OrganizationDbContext orgDb,
            IAccessGuard accessGuard,
            ITenantContext tenantContext,
            IStringLocalizer<SharedResource> localizer,
            IAuditLog auditLog,
            ILogger<LocationService> logger)
        {
            _orgDb = orgDb;
            _accessGuard = accessGuard;
            _tenantContext = tenantContext;
            _localizer = localizer;
            _auditLog = auditLog;
            _logger = logger;
        }

        public async Task<LocationResponse> CreateLocationAsync(Guid userId, CreateLocationRequest request)
        {
            var organizationId = RequireOrganizationId();

            // 1. Only the Owner creates a location
            await RequireOwnerAsync();

            // 2. Create Location
            var location = new Location
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = request.Name,
                Address = request.Address,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country,
                PhoneNumber = request.PhoneNumber,
                Status = LocationStatus.Active,
                TimeZone = request.TimeZone!,
                Type = ParseType(request.Type) ?? LocationType.Store,
                Code = NormalizeCode(request.Code),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await EnsureCodeIsFreeAsync(location.Code, null);
            _orgDb.Locations.Add(location);
            await SaveAsync();

            return MapToResponse(location);
        }

        public async Task<LocationResponse> EditLocationAsync(Guid userId, Guid locationId, EditLocationRequest request)
        {
            var location = await _orgDb.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            await RequireManageLocationAsync(location, request.Status);

            var previousStatus = location.Status;
            location.Name = request.Name;
            location.Address = request.Address;
            location.City = request.City;
            location.State = request.State;
            location.PostalCode = request.PostalCode;
            location.Country = request.Country;
            location.PhoneNumber = request.PhoneNumber;
            location.Status = request.Status ?? location.Status;
            location.TimeZone = request.TimeZone!;
            location.Type = ParseType(request.Type) ?? location.Type;
            location.Code = NormalizeCode(request.Code);
            location.UpdatedAt = DateTime.UtcNow;

            await EnsureCodeIsFreeAsync(location.Code, location.Id);
            await SaveAsync();
            await RecordStatusChangeAsync(location, previousStatus);

            return MapToResponse(location);
        }

        public async Task DeleteLocationAsync(Guid userId, Guid locationId)
        {
            var location = await _orgDb.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            await RequireOwnerAsync();

            // Soft delete by setting status to Closed (or logic as discussed: status is status)
            // User requested "If status closes ... do not delete". But "Delete" endpoint generally means "I want it gone or inactive".
            // Implementation: Set to Closed as a form of soft delete or just updates status. 
            // Better: Just set status to Closed.
            // Wait, "Delete" usually implies "Gone". But requirement says "If user closes... don't delete data".
            // So "Delete" endpoint creates a "Closed" state.

            var previousStatus = location.Status;
            location.Status = LocationStatus.Closed;
            location.UpdatedAt = DateTime.UtcNow;

            await _orgDb.SaveChangesAsync();
            await RecordStatusChangeAsync(location, previousStatus);
        }

        // The change is already saved; a failed audit write must not undo it or fail the request.
        private async Task RecordStatusChangeAsync(Location location, LocationStatus previousStatus)
        {
            if (location.Status == previousStatus)
                return;

            try
            {
                await _auditLog.RecordAsync(new AuditEntryDto
                {
                    Action = AuditActions.LocationStatusChanged,
                    EntityType = "Location",
                    EntityId = location.Id,
                    LocationId = location.Id,
                    Before = previousStatus.ToString(),
                    After = location.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record the status change of location {LocationId}.", location.Id);
            }
        }

        public async Task<List<LocationResponse>> GetLocationsAsync(Guid userId)
        {
            var organizationId = RequireOrganizationId();

            // Any access at all (the Owner or any cell) may list the locations
            await RequireAnyAccessAsync();

            var locations = await _orgDb.Locations
                .OrderBy(l => l.Name)
                .ToListAsync();

            return locations.Select(MapToResponse).ToList();
        }

        private Task RequireOwnerAsync() => _accessGuard.RequireOwnerAsync();

        private Task RequireAnyAccessAsync() => _accessGuard.RequireAnyAccessAsync();

        // Locations Manage at the location; an edit that sets the status Closed needs the Owner.
        private Task RequireManageLocationAsync(Location location, LocationStatus? requestedStatus)
            => requestedStatus == LocationStatus.Closed
                ? _accessGuard.RequireOwnerAsync()
                : _accessGuard.RequireAtLocationAsync(AccessModule.Locations, AccessLevel.Manage, location.Id);

        private static LocationType? ParseType(string? type)
            => type is null ? null : Enum.Parse<LocationType>(type);

        private static string NormalizeCode(string? code) => code!.Trim().ToUpperInvariant();

        // The tenant query filter limits this check to the token organization.
        private async Task EnsureCodeIsFreeAsync(string code, Guid? ignoredLocationId)
        {
            var taken = await _orgDb.Locations.AnyAsync(l => l.Code == code && l.Id != ignoredLocationId);
            if (taken)
                throw new BusinessException(_localizer["LocationCodeAlreadyUsed"].Value, 400);
        }

        // A concurrent request can pass the check above; the unique index then rejects the second one.
        private async Task SaveAsync()
        {
            try
            {
                await _orgDb.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                throw new BusinessException(_localizer["LocationCodeAlreadyUsed"].Value, 400);
            }
        }

        private Guid RequireOrganizationId()
            => _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);

        private static LocationResponse MapToResponse(Location location)
        {
            return new LocationResponse
            {
                Id = location.Id,
                OrganizationId = location.OrganizationId,
                Name = location.Name,
                Address = location.Address,
                City = location.City,
                State = location.State,
                PostalCode = location.PostalCode,
                Country = location.Country,
                PhoneNumber = location.PhoneNumber,
                Status = location.Status,
                TimeZone = location.TimeZone,
                Type = location.Type,
                Code = location.Code,
                CreatedAt = location.CreatedAt,
                UpdatedAt = location.UpdatedAt
            };
        }
    }
}
