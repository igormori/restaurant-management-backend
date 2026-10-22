using RestaurantManagement.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Shared.Data.Tenancy;

namespace RestaurantManagement.Modules.Menu.Services
{
    public class MenuService : IMenuService
    {
        private readonly MenuDbContext _menuDb;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly MenuAccessChecker _access;
        private readonly ITenantContext _tenantContext;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuService(
            MenuDbContext menuDb,
            IOrganizationLookup organizationLookup,
            IAccessGuard accessGuard,
            ITenantContext tenantContext,
            IStringLocalizer<SharedResource> localizer)
        {
            _menuDb = menuDb;
            _organizationLookup = organizationLookup;
            _access = new MenuAccessChecker(accessGuard);
            _tenantContext = tenantContext;
            _localizer = localizer;
        }

        public async Task<MenuResponse> CreateMenuAsync(Guid userId, CreateMenuRequest request)
        {
            var organizationId = GetTokenOrganizationId();

            // 1. Organization check (redundant but safe)
            var organizationExists = await _organizationLookup.OrganizationExistsAsync(organizationId);
            if (!organizationExists)
                throw new BusinessException(_localizer["OrganizationNotFound"].Value, 404);

            // 2. Verify access: Manage at each named location, or at every non-closed location when none is named
            var namedLocations = request.LocationIds?.Any() == true
                ? await _organizationLookup.GetLocationIdsInOrganizationAsync(organizationId, request.LocationIds)
                : new List<Guid>();
            if (request.LocationIds?.Any() == true)
                await _access.RequireManageMenuAsync(namedLocations);
            else
                await _access.RequireManageEverywhereAsync();

            // 3. Create Menu
            var menu = new Entities.Menu
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = request.Name,
                Description = request.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            using var transaction = await _menuDb.Database.BeginTransactionAsync();

            _menuDb.Menus.Add(menu);
            await _menuDb.SaveChangesAsync();

            // 4. If locations are provided, attach the menu
            if (namedLocations.Count > 0)
            {
                foreach (var locationId in namedLocations)
                {
                    var locationMenu = new LocationMenu
                    {
                        Id = Guid.NewGuid(),
                        LocationId = locationId,
                        MenuId = menu.Id,
                        IsActive = true, // Default to true on creation
                        CreatedAt = DateTime.UtcNow
                    };
                    _menuDb.LocationMenus.Add(locationMenu);
                }

                await _menuDb.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return await GetMenuResponseAsync(menu.Id);
        }
        public async Task<MenuResponse> EditMenuAsync(Guid userId, Guid menuId, EditMenuRequest request)
        {
            var menu = await _menuDb.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await RequireManageMenuAsync(menuId);

            menu.Name = request.Name;
            menu.Description = request.Description;
            menu.IsActive = request.IsActive;
            menu.UpdatedAt = DateTime.UtcNow;

            await _menuDb.SaveChangesAsync();

            return await GetMenuResponseAsync(menuId);
        }

        public async Task DeleteMenuAsync(Guid userId, Guid menuId)
        {
            var menu = await _menuDb.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await RequireManageMenuAsync(menuId);

            _menuDb.Menus.Remove(menu);
            await _menuDb.SaveChangesAsync();
        }

        public async Task AttachMenuToLocationAsync(Guid userId, Guid menuId, Guid locationId)
        {
            var menu = await _menuDb.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            var location = await _organizationLookup.GetLocationAsync(locationId);
            if (location == null || location.OrganizationId != menu.OrganizationId)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            await _access.RequireManageAtLocationAsync(locationId);

            var exists = await _menuDb.LocationMenus
                .AnyAsync(lm => lm.MenuId == menuId && lm.LocationId == locationId);

            if (!exists)
            {
                var locationMenu = new LocationMenu
                {
                    Id = Guid.NewGuid(),
                    MenuId = menuId,
                    LocationId = locationId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _menuDb.LocationMenus.Add(locationMenu);
                await _menuDb.SaveChangesAsync();
            }
        }

        public async Task DetachMenuFromLocationAsync(Guid userId, Guid menuId, Guid locationId)
        {
            var menu = await _menuDb.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await _access.RequireManageAtLocationAsync(locationId);

            var link = await _menuDb.LocationMenus
                .FirstOrDefaultAsync(lm => lm.MenuId == menuId && lm.LocationId == locationId);

            if (link != null)
            {
                _menuDb.LocationMenus.Remove(link);
                await _menuDb.SaveChangesAsync();
            }
        }

        public async Task<List<MenuResponse>> GetMenusAsync(Guid userId)
        {
            var organizationId = GetTokenOrganizationId();
            await _access.RequireReadEverywhereAsync();

            var menus = await _menuDb.Menus
                .Where(m => m.OrganizationId == organizationId)
                .Include(m => m.LocationMenus)
                .ToListAsync();

            return menus.Select(m => new MenuResponse
            {
                Id = m.Id,
                OrganizationId = m.OrganizationId,
                Name = m.Name,
                Description = m.Description,
                IsActive = m.IsActive,
                LocationIds = m.LocationMenus.Select(lm => lm.LocationId).ToList()
            }).ToList();
        }

        public async Task<List<MenuResponse>> GetMenusByLocationAsync(Guid userId, Guid locationId)
        {
            var location = await _organizationLookup.GetLocationAsync(locationId);
            if (location == null)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            await _access.RequireReadAtLocationAsync(locationId);

            var menus = await _menuDb.LocationMenus
                .Where(lm => lm.LocationId == locationId)
                .Include(lm => lm.Menu)
                .ThenInclude(m => m.LocationMenus) // Re-include to get all locationIds for response
                .Select(lm => lm.Menu)
                .ToListAsync();

            return menus.Select(m => new MenuResponse
            {
                Id = m.Id,
                OrganizationId = m.OrganizationId,
                Name = m.Name,
                Description = m.Description,
                IsActive = m.IsActive,
                LocationIds = m.LocationMenus.Select(lm => lm.LocationId).ToList()
            }).ToList();
        }

        private async Task RequireManageMenuAsync(Guid menuId)
        {
            var linkedLocationIds = await _menuDb.LocationMenus
                .Where(lm => lm.MenuId == menuId)
                .Select(lm => lm.LocationId)
                .ToListAsync();
            await _access.RequireManageMenuAsync(linkedLocationIds);
        }

        private Guid GetTokenOrganizationId()
        {
            return _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
        }

        private async Task<MenuResponse> GetMenuResponseAsync(Guid menuId)
        {
            var menu = await _menuDb.Menus
                .Include(m => m.LocationMenus)
                .FirstOrDefaultAsync(m => m.Id == menuId);

            if (menu == null) return null!;

            return new MenuResponse
            {
                Id = menu.Id,
                OrganizationId = menu.OrganizationId,
                Name = menu.Name,
                Description = menu.Description,
                IsActive = menu.IsActive,
                LocationIds = menu.LocationMenus.Select(lm => lm.LocationId).ToList()
            };
        }
    }
}
