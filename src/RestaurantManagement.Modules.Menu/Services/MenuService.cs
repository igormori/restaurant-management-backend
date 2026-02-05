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
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Menu.Services
{
    public class MenuService : IMenuService
    {
        private readonly MenuDbContext _menuDb;
        private readonly OrganizationDbContext _orgDb;
        private readonly IdentityDbContext _identityDb;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuService(
            MenuDbContext menuDb,
            OrganizationDbContext orgDb,
            IdentityDbContext identityDb,
            IStringLocalizer<SharedResource> localizer)
        {
            _menuDb = menuDb;
            _orgDb = orgDb;
            _identityDb = identityDb;
            _localizer = localizer;
        }

        public async Task<MenuResponse> CreateMenuAsync(Guid userId, CreateMenuRequest request)
        {
            // 1. Verify user permissions
            await CheckUserPermission(userId, request.OrganizationId);

            // 2. Organization check (redundant but safe)
            var organization = await _orgDb.Organizations.FindAsync(request.OrganizationId);
            if (organization == null)
                throw new BusinessException(_localizer["OrganizationNotFound"].Value, 404);

            // 3. Create Menu
            var menu = new Entities.Menu
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
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
            if (request.LocationIds != null && request.LocationIds.Any())
            {
                // Verify locations belong to organization
                var validLocations = await _orgDb.Locations
                    .Where(l => request.LocationIds.Contains(l.Id) && l.OrganizationId == request.OrganizationId)
                    .Select(l => l.Id)
                    .ToListAsync();

                foreach (var locationId in validLocations)
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

            await CheckUserPermission(userId, menu.OrganizationId);

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

            await CheckUserPermission(userId, menu.OrganizationId);

            _menuDb.Menus.Remove(menu);
            await _menuDb.SaveChangesAsync();
        }

        public async Task AttachMenuToLocationAsync(Guid userId, Guid menuId, Guid locationId)
        {
            var menu = await _menuDb.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await CheckUserPermission(userId, menu.OrganizationId);

            var location = await _orgDb.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null || location.OrganizationId != menu.OrganizationId)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

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

            await CheckUserPermission(userId, menu.OrganizationId);

            var link = await _menuDb.LocationMenus
                .FirstOrDefaultAsync(lm => lm.MenuId == menuId && lm.LocationId == locationId);

            if (link != null)
            {
                _menuDb.LocationMenus.Remove(link);
                await _menuDb.SaveChangesAsync();
            }
        }

        public async Task<List<MenuResponse>> GetMenusByOrganizationAsync(Guid userId, Guid organizationId)
        {
            await CheckUserPermission(userId, organizationId);

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
            var location = await _orgDb.Locations.FindAsync(locationId);
            if (location == null)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            await CheckUserPermission(userId, location.OrganizationId);

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

        private async Task CheckUserPermission(Guid userId, Guid organizationId)
        {
            var userRole = await _identityDb.UserRoles
                .FirstOrDefaultAsync(r => r.UserId == userId && r.OrganizationId == organizationId);

            if (userRole == null || (userRole.Role != "Owner" && userRole.Role != "Admin"))
            {
                throw new BusinessException(_localizer["UnauthorizedMessage"].Value, 403);
            }
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
