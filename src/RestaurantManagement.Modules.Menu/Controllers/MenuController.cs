using RestaurantManagement.Shared;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using RestaurantManagement.Shared.Utils.Exceptions;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;

namespace RestaurantManagement.Modules.Menu.Controllers
{
    [ApiController]
    [Route("api/menus")]
    public class MenuController : ControllerBase
    {
        private readonly IMenuService _menuService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuController(IMenuService menuService, IStringLocalizer<SharedResource> localizer)
        {
            _menuService = menuService;
            _localizer = localizer;
        }

        [HttpPost("create")]
        [Authorize]
        public async Task<ActionResult<MenuResponse>> Create([FromBody] CreateMenuRequest request)
        {
            var userId = GetCurrentUserId();

            var response = await _menuService.CreateMenuAsync(userId, request);
            return Ok(response);
        }

        [HttpPut("{menuId}")]
        [Authorize]
        public async Task<ActionResult<MenuResponse>> Edit(Guid menuId, [FromBody] EditMenuRequest request)
        {
            var userId = GetCurrentUserId();

            var response = await _menuService.EditMenuAsync(userId, menuId, request);
            return Ok(response);
        }

        [HttpDelete("{menuId}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid menuId)
        {
            var userId = GetCurrentUserId();

            await _menuService.DeleteMenuAsync(userId, menuId);
            return Ok(new { message = _localizer["MenuDeletedSuccessfully"].Value });
        }

        [HttpPost("{menuId}/locations/{locationId}")]
        [Authorize]
        public async Task<IActionResult> AttachLocation(Guid menuId, Guid locationId)
        {
            var userId = GetCurrentUserId();

            await _menuService.AttachMenuToLocationAsync(userId, menuId, locationId);
            return Ok(new { message = _localizer["MenuAttachedSuccessfully"].Value });
        }

        [HttpDelete("{menuId}/locations/{locationId}")]
        [Authorize]
        public async Task<IActionResult> DetachLocation(Guid menuId, Guid locationId)
        {
            var userId = GetCurrentUserId();

            await _menuService.DetachMenuFromLocationAsync(userId, menuId, locationId);
            return Ok(new { message = _localizer["MenuDetachedSuccessfully"].Value });
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<List<MenuResponse>>> GetAll()
        {
            var userId = GetCurrentUserId();

            var response = await _menuService.GetMenusAsync(userId);
            return Ok(response);
        }

        [HttpGet("location/{locationId}")]
        [Authorize]
        public async Task<ActionResult<List<MenuResponse>>> GetByLocation(Guid locationId)
        {
            var userId = GetCurrentUserId();

            var response = await _menuService.GetMenusByLocationAsync(userId, locationId);
            return Ok(response);
        }

        private Guid GetCurrentUserId()
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (Guid.TryParse(subject, out var userId))
            {
                return userId;
            }
            throw new BusinessException(_localizer["UnauthorizedMessage"].Value, 401);
        }
    }
}
