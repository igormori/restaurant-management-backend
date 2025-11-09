using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using RestaurantManagement.Api.Models.Organizations;
using RestaurantManagement.Api.Services.Organizations;

namespace RestaurantManagement.Api.Controllers.Organizations
{
    [ApiController]
    [Route("api/organizations")]
    public class OrganizationController : ControllerBase
    {
        private readonly IOrganizationService _organizationService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public OrganizationController(IOrganizationService organizationService, IStringLocalizer<SharedResource> localizer)
        {
            _organizationService = organizationService;
            _localizer = localizer;
        }

        // POST api/organizations/register/{userId}
        [HttpPost("register/{userId:guid}")]
        [Authorize]
        public async Task<ActionResult<OrganizationResponse>> Register(Guid userId, [FromBody] CreateOrganizationRequest request)
        {
            // Check if logged user is the same as the passed id on the parametere.
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(subject, out var currentUserId) || currentUserId != userId)
            {
                throw new InvalidOperationException(_localizer["UnauthorizedMessage"].Value);
            }

            var response = await _organizationService.CreateOrganizationAsync(userId, request);
            return Ok(response);
        }

        // PUT api/organizations/edit/{organizationId}
        [HttpPut("edit/{organizationId:guid}")]
        [Authorize(Roles ="Owner,Admin")]
        public async Task<ActionResult<OrganizationResponse>> Edit(Guid organizationId, [FromBody] EditOrganizationRequest request)
        {
            var response = await _organizationService.EditOrganizationAsync(organizationId, request);
            return Ok(response);
        }
    }
}