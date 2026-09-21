using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public MenuCategoryController(IMenuCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// Create a new menu category
        /// </summary>
        [HttpPost("menu-categories")]
        public async Task<ActionResult<MenuCategoryResponse>> CreateCategory([FromBody] CreateMenuCategoryRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var category = await _categoryService.CreateAsync(userId, request);
            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, category);
        }

        /// <summary>
        /// Get a menu category by ID
        /// </summary>
        [HttpGet("menu-categories/{id}")]
        public async Task<ActionResult<MenuCategoryResponse>> GetCategoryById(Guid id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var category = await _categoryService.GetByIdAsync(userId, id);
            if (category == null)
                return NotFound(new { message = "Menu category not found" });

            return Ok(category);
        }

        /// <summary>
        /// Get all categories for a specific menu
        /// </summary>
        [HttpGet("menus/{menuId}/categories")]
        public async Task<ActionResult<IEnumerable<MenuCategoryResponse>>> GetCategoriesByMenu(Guid menuId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var categories = await _categoryService.GetByMenuIdAsync(userId, menuId);
            return Ok(categories);
        }

        /// <summary>
        /// Get all categories for an organization
        /// </summary>
        [HttpGet("organizations/{organizationId}/menu-categories")]
        public async Task<ActionResult<IEnumerable<MenuCategoryResponse>>> GetCategoriesByOrganization(Guid organizationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var categories = await _categoryService.GetByOrganizationIdAsync(userId, organizationId);
            return Ok(categories);
        }

        /// <summary>
        /// Get all categories for a location
        /// </summary>
        [HttpGet("locations/{locationId}/menu-categories")]
        public async Task<ActionResult<IEnumerable<MenuCategoryResponse>>> GetCategoriesByLocation(Guid locationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var categories = await _categoryService.GetByLocationIdAsync(userId, locationId);
            return Ok(categories);
        }

        /// <summary>
        /// Update a menu category
        /// </summary>
        [HttpPut("menu-categories/{id}")]
        public async Task<ActionResult<MenuCategoryResponse>> UpdateCategory(Guid id, [FromBody] UpdateMenuCategoryRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var category = await _categoryService.UpdateAsync(userId, id, request);
            if (category == null)
                return NotFound(new { message = "Menu category not found" });

            return Ok(category);
        }

        /// <summary>
        /// Delete a menu category
        /// </summary>
        [HttpDelete("menu-categories/{id}")]
        public async Task<ActionResult> DeleteCategory(Guid id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var result = await _categoryService.DeleteAsync(userId, id);
            if (!result)
                return NotFound(new { message = "Menu category not found" });

            return NoContent();
        }
    }
}
