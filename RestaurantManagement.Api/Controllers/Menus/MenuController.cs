using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using RestaurantManagement.Api.Models.Menus;
using RestaurantManagement.Api.Services.Menus;

namespace RestaurantManagement.Api.Controllers.Menus
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
        [Authorize(Roles = "Owner,Admin")]
        public async Task<ActionResult<MenuResponse>> Create([FromBody] CreateMenuRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var response = await _menuService.CreateMenuAsync(userId, request);
            return Ok(response);
        }

        [HttpPut("{menuId}")]
        [Authorize(Roles = "Owner,Admin")]
        public async Task<ActionResult<MenuResponse>> Edit(Guid menuId, [FromBody] EditMenuRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var response = await _menuService.EditMenuAsync(userId, menuId, request);
            return Ok(response);
        }

        [HttpDelete("{menuId}")]
        [Authorize(Roles = "Owner,Admin")]
        public async Task<IActionResult> Delete(Guid menuId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            await _menuService.DeleteMenuAsync(userId, menuId);
            return Ok(new { message = _localizer["MenuDeletedSuccessfully"].Value });
        }

        [HttpPost("{menuId}/locations/{locationId}")]
        [Authorize(Roles = "Owner,Admin")]
        public async Task<IActionResult> AttachLocation(Guid menuId, Guid locationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            await _menuService.AttachMenuToLocationAsync(userId, menuId, locationId);
            return Ok(new { message = _localizer["MenuAttachedSuccessfully"].Value });
        }

        [HttpDelete("{menuId}/locations/{locationId}")]
        [Authorize(Roles = "Owner,Admin")]
        public async Task<IActionResult> DetachLocation(Guid menuId, Guid locationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            await _menuService.DetachMenuFromLocationAsync(userId, menuId, locationId);
            return Ok(new { message = _localizer["MenuDetachedSuccessfully"].Value });
        }

        [HttpGet("organization/{organizationId}")]
        [Authorize(Roles = "Owner,Admin,Manager,Employee")] 
        public async Task<ActionResult<List<MenuResponse>>> GetByOrganization(Guid organizationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var response = await _menuService.GetMenusByOrganizationAsync(userId, organizationId);
            return Ok(response);
        }

        [HttpGet("location/{locationId}")]
        [Authorize(Roles = "Owner,Admin,Manager,Employee")]
        public async Task<ActionResult<List<MenuResponse>>> GetByLocation(Guid locationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var response = await _menuService.GetMenusByLocationAsync(userId, locationId);
            return Ok(response);
        }
    }
}
