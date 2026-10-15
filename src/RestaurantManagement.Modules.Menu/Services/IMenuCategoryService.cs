using RestaurantManagement.Modules.Menu.Models;

namespace RestaurantManagement.Modules.Menu.Services
{
    public interface IMenuCategoryService
    {
        Task<MenuCategoryResponse> CreateAsync(Guid userId, CreateMenuCategoryRequest request);
        Task<MenuCategoryResponse?> GetByIdAsync(Guid userId, Guid id);
        Task<IEnumerable<MenuCategoryResponse>> GetByMenuIdAsync(Guid userId, Guid menuId);
        Task<IEnumerable<MenuCategoryResponse>> GetByOrganizationIdAsync(Guid userId, Guid organizationId);
        Task<IEnumerable<MenuCategoryResponse>> GetByLocationIdAsync(Guid userId, Guid locationId);
        Task<MenuCategoryResponse?> UpdateAsync(Guid userId, Guid id, UpdateMenuCategoryRequest request);
        Task<bool> DeleteAsync(Guid userId, Guid id);
    }
}
