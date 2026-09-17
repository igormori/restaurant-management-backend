using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Menu.Models;

namespace RestaurantManagement.Modules.Menu.Services
{
    public class MenuCategoryService : IMenuCategoryService
    {
        private readonly MenuDbContext _context;

        public MenuCategoryService(MenuDbContext context)
        {
            _context = context;
        }

        public async Task<MenuCategoryResponse> CreateAsync(CreateMenuCategoryRequest request)
        {
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

        public async Task<MenuCategoryResponse?> GetByIdAsync(Guid id)
        {
            var category = await _context.MenuCategories
                .FirstOrDefaultAsync(c => c.Id == id);

            return category == null ? null : MapToResponse(category);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetByMenuIdAsync(Guid menuId)
        {
            var categories = await _context.MenuCategories
                .Where(c => c.MenuId == menuId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetByOrganizationIdAsync(Guid organizationId)
        {
            var categories = await _context.MenuCategories
                .Where(c => c.OrganizationId == organizationId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<IEnumerable<MenuCategoryResponse>> GetByLocationIdAsync(Guid locationId)
        {
            var categories = await _context.MenuCategories
                .Where(c => c.LocationId == locationId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return categories.Select(MapToResponse);
        }

        public async Task<MenuCategoryResponse?> UpdateAsync(Guid id, UpdateMenuCategoryRequest request)
        {
            var category = await _context.MenuCategories.FindAsync(id);
            if (category == null)
                return null;

            category.Name = request.Name;
            category.Description = request.Description;
            category.SortOrder = request.SortOrder;

            await _context.SaveChangesAsync();

            return MapToResponse(category);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var category = await _context.MenuCategories.FindAsync(id);
            if (category == null)
                return false;

            _context.MenuCategories.Remove(category);
            await _context.SaveChangesAsync();

            return true;
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
