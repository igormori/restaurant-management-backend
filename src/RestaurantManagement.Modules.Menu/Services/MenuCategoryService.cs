using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Menu.Services
{
    public class MenuCategoryService : IMenuCategoryService
    {
        private readonly MenuDbContext _context;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly IUserRoleLookup _userRoleLookup;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuCategoryService(
            MenuDbContext context,
            IOrganizationLookup organizationLookup,
            IUserRoleLookup userRoleLookup,
            IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _organizationLookup = organizationLookup;
            _userRoleLookup = userRoleLookup;
            _localizer = localizer;
        }

        public async Task<MenuCategoryResponse> CreateAsync(Guid userId, CreateMenuCategoryRequest request)
        {
            await CheckUserPermission(userId, request.OrganizationId);

            var category = new MenuCategory
            {
                Id = Guid.NewGuid(),
                MenuId = request.MenuId,
                OrganizationId = request.OrganizationId,
                LocationId = request.LocationId,
                Name = request.Name,
                Description = request.Description,
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow
            };

            _context.MenuCategories.Add(category);
            await _context.SaveChangesAsync();

            return MapToResponse(category);
        }

        public async Task<MenuCategoryResponse?> GetByIdAsync(Guid userId, Guid id)
        {
            var category = await _context.MenuCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return null;

            await CheckUserPermission(userId, category.OrganizationId);

            return MapToResponse(category);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetByMenuIdAsync(Guid userId, Guid menuId)
        {
            var menu = await _context.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                return Enumerable.Empty<MenuCategoryResponse>();

            await CheckUserPermission(userId, menu.OrganizationId);

            var categories = await _context.MenuCategories
                .Where(c => c.MenuId == menuId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetByOrganizationIdAsync(Guid userId, Guid organizationId)
        {
            await CheckUserPermission(userId, organizationId);

            var categories = await _context.MenuCategories
                .Where(c => c.OrganizationId == organizationId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetByLocationIdAsync(Guid userId, Guid locationId)
        {
            var location = await _organizationLookup.GetLocationAsync(locationId);
            if (location == null)
                return Enumerable.Empty<MenuCategoryResponse>();

            await CheckUserPermission(userId, location.OrganizationId);

            var categories = await _context.MenuCategories
                .Where(c => c.LocationId == locationId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<MenuCategoryResponse?> UpdateAsync(Guid userId, Guid id, UpdateMenuCategoryRequest request)
        {
            var category = await _context.MenuCategories.FindAsync(id);
            if (category == null)
                return null;

            await CheckUserPermission(userId, category.OrganizationId);

            category.Name = request.Name;
            category.Description = request.Description;
            category.SortOrder = request.SortOrder;

            await _context.SaveChangesAsync();

            return MapToResponse(category);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid id)
        {
            var category = await _context.MenuCategories.FindAsync(id);
            if (category == null)
                return false;

            await CheckUserPermission(userId, category.OrganizationId);

            _context.MenuCategories.Remove(category);
            await _context.SaveChangesAsync();

            return true;
        }

        private async Task CheckUserPermission(Guid userId, Guid organizationId)
        {
            var role = await _userRoleLookup.GetRoleAsync(userId, organizationId);

            if (role == null || (role != Roles.Owner && role != Roles.Admin))
            {
                throw new BusinessException(_localizer["UnauthorizedMessage"].Value, 403);
            }
        }

        private static MenuCategoryResponse MapToResponse(MenuCategory category)
        {
            return new MenuCategoryResponse
            {
                Id = category.Id,
                MenuId = category.MenuId,
                OrganizationId = category.OrganizationId,
                LocationId = category.LocationId,
                Name = category.Name,
                Description = category.Description,
                SortOrder = category.SortOrder,
                CreatedAt = category.CreatedAt
            };
        }
    }
}
