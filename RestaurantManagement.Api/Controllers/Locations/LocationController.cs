using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RestaurantManagement.Api.Models.Locations;
using RestaurantManagement.Api.Services.Locations;

namespace RestaurantManagement.Api.Controllers.Locations
{
    [ApiController]
    [Route("api/locations")]
    [Authorize]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;

        public LocationController(ILocationService locationService)
        {
            _locationService = locationService;
        }

        // GET api/locations/{organizationId}
        [HttpGet("{organizationId:guid}")]
        public async Task<ActionResult<List<LocationResponse>>> GetLocations(Guid organizationId)
        {
            var userId = GetCurrentUserId();
            var response = await _locationService.GetLocationsByOrganizationAsync(userId, organizationId);
            return Ok(response);
        }

        // POST api/locations/{organizationId}
        [HttpPost("{organizationId:guid}")]
        public async Task<ActionResult<LocationResponse>> CreateLocation(Guid organizationId, [FromBody] CreateLocationRequest request)
        {
            var userId = GetCurrentUserId();
            var response = await _locationService.CreateLocationAsync(userId, organizationId, request);
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
            throw new UnauthorizedAccessException("Invalid User ID");
        }
    }
}
