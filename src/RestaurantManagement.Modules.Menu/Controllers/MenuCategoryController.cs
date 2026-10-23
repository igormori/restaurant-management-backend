using System.Security.Claims;
using RestaurantManagement.Shared.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;

namespace RestaurantManagement.Modules.Menu.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class MenuCategoryController : ControllerBase
    {
        private readonly IMenuCategoryService _categoryService;

        private readonly IStringLocalizer<SharedResource> _localizer;

        public MenuCategoryController(IMenuCategoryService categoryService, IStringLocalizer<SharedResource> localizer)
        {
            _categoryService = categoryService;
            _localizer = localizer;
        }

        /// <summary>
        /// Create a new menu category
        /// </summary>
        [HttpPost("menu-categories")]
        public async Task<ActionResult<MenuCategoryResponse>> CreateCategory([FromBody] CreateMenuCategoryRequest request)
        {
            var userId = GetCurrentUserId();

            var category = await _categoryService.CreateAsync(userId, request);
            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
        }

        /// <summary>
        /// Get a menu category by ID
        /// </summary>
        [HttpGet("menu-categories/{id}")]
        public async Task<ActionResult<MenuCategoryResponse>> GetCategoryById(Guid id)
        {
            var userId = GetCurrentUserId();

            var category = await _categoryService.GetByIdAsync(userId, id);
            if (category == null)
                return NotFound(new { message = _localizer["MenuCategoryNotFound"].Value });

            return Ok(category);
        }

        /// <summary>
        /// Get all categories for a specific menu
        /// </summary>
        [HttpGet("menus/{menuId}/categories")]
        public async Task<ActionResult<IEnumerable<MenuCategoryResponse>>> GetCategoriesByMenu(Guid menuId)
        {
            var userId = GetCurrentUserId();

            var categories = await _categoryService.GetByMenuIdAsync(userId, menuId);
            return Ok(categories);
        }

        /// <summary>
        /// Get all categories of the signed-in organization
        /// </summary>
        [HttpGet("menu-categories")]
        public async Task<ActionResult<IEnumerable<MenuCategoryResponse>>> GetCategories()
        {
            var userId = GetCurrentUserId();

            var categories = await _categoryService.GetAllAsync(userId);
            return Ok(categories);
        }

        /// <summary>
        /// Get all categories for a location
        /// </summary>
        [HttpGet("locations/{locationId}/menu-categories")]
        public async Task<ActionResult<IEnumerable<MenuCategoryResponse>>> GetCategoriesByLocation(Guid locationId)
        {
            var userId = GetCurrentUserId();

            var categories = await _categoryService.GetByLocationIdAsync(userId, locationId);
            return Ok(categories);
        }

        /// <summary>
        /// Update a menu category
        /// </summary>
        [HttpPut("menu-categories/{id}")]
        public async Task<ActionResult<MenuCategoryResponse>> UpdateCategory(Guid id, [FromBody] UpdateMenuCategoryRequest request)
        {
            var userId = GetCurrentUserId();

            var category = await _categoryService.UpdateAsync(userId, id, request);
            if (category == null)
                return NotFound(new { message = _localizer["MenuCategoryNotFound"].Value });

            return Ok(category);
        }

        /// <summary>
        /// Delete a menu category
        /// </summary>
        [HttpDelete("menu-categories/{id}")]
        public async Task<ActionResult> DeleteCategory(Guid id)
        {
            var userId = GetCurrentUserId();

            var result = await _categoryService.DeleteAsync(userId, id);
            if (!result)
                return NotFound(new { message = _localizer["MenuCategoryNotFound"].Value });

            return NoContent();
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
