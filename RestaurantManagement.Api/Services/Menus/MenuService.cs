using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Menus;
using RestaurantManagement.Api.Models.Menus;
using RestaurantManagement.Api.Utils.Exceptions;

namespace RestaurantManagement.Api.Services.Menus
{
    public class MenuService : IMenuService
    {
        private readonly RestaurantDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuService(RestaurantDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<MenuResponse> CreateMenuAsync(Guid userId, CreateMenuRequest request)
        {
            // 1. Verify user permissions
            await CheckUserPermission(userId, request.OrganizationId);

            // 2. Organization check (redundant but safe)
            var organization = await _db.Organizations.FindAsync(request.OrganizationId);
            if (organization == null)
                throw new BusinessException(_localizer["OrganizationNotFound"].Value, 404);

            // 3. Create Menu
            var menu = new Menu
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                Name = request.Name,
                Description = request.Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            using var transaction = await _db.Database.BeginTransactionAsync();

            _db.Menus.Add(menu);
            await _db.SaveChangesAsync(); 

            // 4. If locations are provided, attach the menu
            if (request.LocationIds != null && request.LocationIds.Any())
            {
                // Verify locations belong to organization
                var validLocations = await _db.Locations
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
                    _db.LocationMenus.Add(locationMenu);
                }
                
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            return await GetMenuResponseAsync(menu.Id);
        }
        public async Task<MenuResponse> EditMenuAsync(Guid userId, Guid menuId, EditMenuRequest request)
        {
            var menu = await _db.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await CheckUserPermission(userId, menu.OrganizationId);

            menu.Name = request.Name;
            menu.Description = request.Description;
            menu.IsActive = request.IsActive;
            menu.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return await GetMenuResponseAsync(menuId);
        }

        public async Task DeleteMenuAsync(Guid userId, Guid menuId)
        {
            var menu = await _db.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await CheckUserPermission(userId, menu.OrganizationId);

            _db.Menus.Remove(menu);
            await _db.SaveChangesAsync();
        }

        public async Task AttachMenuToLocationAsync(Guid userId, Guid menuId, Guid locationId)
        {
            var menu = await _db.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await CheckUserPermission(userId, menu.OrganizationId);

            var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null || location.OrganizationId != menu.OrganizationId)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            var exists = await _db.LocationMenus
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
                _db.LocationMenus.Add(locationMenu);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DetachMenuFromLocationAsync(Guid userId, Guid menuId, Guid locationId)
        {
            var menu = await _db.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            await CheckUserPermission(userId, menu.OrganizationId);

            var link = await _db.LocationMenus
                .FirstOrDefaultAsync(lm => lm.MenuId == menuId && lm.LocationId == locationId);

            if (link != null)
            {
                _db.LocationMenus.Remove(link);
                await _db.SaveChangesAsync();
            }
        }

        public async Task<List<MenuResponse>> GetMenusByOrganizationAsync(Guid userId, Guid organizationId)
        {
            await CheckUserPermission(userId, organizationId);

            var menus = await _db.Menus
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
            var location = await _db.Locations.FindAsync(locationId);
            if (location == null)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            await CheckUserPermission(userId, location.OrganizationId);

            var menus = await _db.LocationMenus
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
            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(r => r.UserId == userId && r.OrganizationId == organizationId);

            if (userRole == null || (userRole.Role != "Owner" && userRole.Role != "Admin"))
            {
                throw new BusinessException(_localizer["UnauthorizedMessage"].Value, 403);
            }
        }

        private async Task<MenuResponse> GetMenuResponseAsync(Guid menuId)
        {
            var menu = await _db.Menus
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
