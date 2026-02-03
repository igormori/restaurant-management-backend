using System;
using System.Threading.Tasks;
using RestaurantManagement.Api.Models.Menus;

namespace RestaurantManagement.Api.Services.Menus
{
    public interface IMenuService
    {
        Task<MenuResponse> CreateMenuAsync(Guid userId, CreateMenuRequest request);
        Task<MenuResponse> EditMenuAsync(Guid userId, Guid menuId, EditMenuRequest request);
        Task DeleteMenuAsync(Guid userId, Guid menuId);
        Task AttachMenuToLocationAsync(Guid userId, Guid menuId, Guid locationId);
        Task DetachMenuFromLocationAsync(Guid userId, Guid menuId, Guid locationId);
        Task<List<MenuResponse>> GetMenusByOrganizationAsync(Guid userId, Guid organizationId);
        Task<List<MenuResponse>> GetMenusByLocationAsync(Guid userId, Guid locationId);
    }
}
