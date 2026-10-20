using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Menu.Services
{
    public class MenuCategoryService : IMenuCategoryService
    {
        private readonly MenuDbContext _context;
        private readonly IOrganizationLookup _organizationLookup;
        private readonly MenuAccessChecker _access;
        private readonly ITenantContext _tenantContext;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuCategoryService(
            MenuDbContext context,
            IOrganizationLookup organizationLookup,
            IUserRoleLookup userRoleLookup,
            ITenantContext tenantContext,
            IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _organizationLookup = organizationLookup;
            _access = new MenuAccessChecker(userRoleLookup, localizer);
            _tenantContext = tenantContext;
            _localizer = localizer;
        }

        public async Task<MenuCategoryResponse> CreateAsync(Guid userId, CreateMenuCategoryRequest request)
        {
            var organizationId = GetTokenOrganizationId();
            await _access.RequireOrganizationAdminAsync(userId, organizationId);

            // The tenant query filter hides other organizations' menus, so a foreign MenuId looks missing.
            var menuExists = await _context.Menus.AnyAsync(m => m.Id == request.MenuId);
            if (!menuExists)
                throw new BusinessException(_localizer["MenuNotFound"].Value, 404);

            if (request.LocationId.HasValue)
            {
                var location = await _organizationLookup.GetLocationAsync(request.LocationId.Value);
                if (location == null || location.OrganizationId != organizationId)
                    throw new BusinessException(_localizer["LocationNotFound"].Value, 404);
            }

            var category = new MenuCategory
            {
                Id = Guid.NewGuid(),
                MenuId = request.MenuId,
                OrganizationId = organizationId,
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

            await _access.CheckAdminAtLocationAsync(userId, category.OrganizationId, category.LocationId);

            return MapToResponse(category);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetByMenuIdAsync(Guid userId, Guid menuId)
        {
            var menu = await _context.Menus.FirstOrDefaultAsync(m => m.Id == menuId);
            if (menu == null)
                return Enumerable.Empty<MenuCategoryResponse>();

            await _access.RequireOrganizationAdminAsync(userId, menu.OrganizationId);

            var categories = await _context.MenuCategories
                .Where(c => c.MenuId == menuId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetAllAsync(Guid userId)
        {
            var organizationId = GetTokenOrganizationId();
            await _access.RequireOrganizationAdminAsync(userId, organizationId);

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

            await _access.CheckAdminAtLocationAsync(userId, location.OrganizationId, locationId);

            var categories = await _context.MenuCategories
                .Where(c => c.LocationId == locationId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<MenuCategoryResponse?> UpdateAsync(Guid userId, Guid id, UpdateMenuCategoryRequest request)
        {
            var category = await _context.MenuCategories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                return null;

            await _access.RequireOrganizationAdminAsync(userId, category.OrganizationId);

            category.Name = request.Name;
            category.Description = request.Description;
            category.SortOrder = request.SortOrder;

            await _context.SaveChangesAsync();

            return MapToResponse(category);
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid id)
        {
            var category = await _context.MenuCategories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                return false;

            await _access.RequireOrganizationAdminAsync(userId, category.OrganizationId);

            _context.MenuCategories.Remove(category);
            await _context.SaveChangesAsync();

            return true;
        }

        private Guid GetTokenOrganizationId()
        {
            return _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);
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
