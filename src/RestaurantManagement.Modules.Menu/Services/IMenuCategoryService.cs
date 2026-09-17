using RestaurantManagement.Modules.Menu.Models;

namespace RestaurantManagement.Modules.Menu.Services
{
    public interface IMenuCategoryService
    {
        Task<MenuCategoryResponse> CreateAsync(CreateMenuCategoryRequest request);
        Task<MenuCategoryResponse?> GetByIdAsync(Guid id);
        Task<IEnumerable<MenuCategoryResponse>> GetByMenuIdAsync(Guid menuId);
        Task<IEnumerable<MenuCategoryResponse>> GetByOrganizationIdAsync(Guid organizationId);
        Task<IEnumerable<MenuCategoryResponse>> GetByLocationIdAsync(Guid locationId);
        Task<MenuCategoryResponse?> UpdateAsync(Guid id, UpdateMenuCategoryRequest request);
        Task<bool> DeleteAsync(Guid id);
    }
}
