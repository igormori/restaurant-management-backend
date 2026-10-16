using RestaurantManagement.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Shared.Utils.Exceptions;
using System.Security.Claims;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Modules.Organization.Services;

namespace RestaurantManagement.Modules.Organization.Controllers
{
    [ApiController]
    [Route("api/locations")]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;

        private readonly IStringLocalizer<SharedResource> _localizer;

        public LocationController(ILocationService locationService, IStringLocalizer<SharedResource> localizer)
        {
            _locationService = locationService;
            _localizer = localizer;
        }

        // GET api/locations
        [HttpGet]
        public async Task<ActionResult<List<LocationResponse>>> GetLocations()
        {
            var userId = GetCurrentUserId();
            var response = await _locationService.GetLocationsAsync(userId);
            return Ok(response);
        }

        // POST api/locations
        [HttpPost]
        public async Task<ActionResult<LocationResponse>> CreateLocation([FromBody] CreateLocationRequest request)
        {
            var userId = GetCurrentUserId();
            var response = await _locationService.CreateLocationAsync(userId, request);
            return Ok(response);
        }

        // PUT api/locations/{locationId}
        [HttpPut("{locationId:guid}")]
        public async Task<ActionResult<LocationResponse>> EditLocation(Guid locationId, [FromBody] EditLocationRequest request)
        {
            var userId = GetCurrentUserId();
            var response = await _locationService.EditLocationAsync(userId, locationId, request);
            return Ok(response);
        }

        // DELETE api/locations/{locationId}
        [HttpDelete("{locationId:guid}")]
        public async Task<IActionResult> DeleteLocation(Guid locationId)
        {
            var userId = GetCurrentUserId();
            await _locationService.DeleteLocationAsync(userId, locationId);
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
