using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RestaurantManagement.Api.Models.Organizations;
using RestaurantManagement.Api.Services.Organizations;

namespace RestaurantManagement.Api.Controllers.Organizations
{
    [ApiController]
    [Route("api/users/{userId:guid}/organizations")]
    public class OrganizationController : ControllerBase
    {
        private readonly IOrganizationService _organizationService;

        public OrganizationController(IOrganizationService organizationService)
        {
            _organizationService = organizationService;
        }

        // POST api/users/{userId}/organizations/register
        [HttpPost("register")]
        [Authorize]
        public async Task<ActionResult<OrganizationResponse>> Register(Guid userId, [FromBody] CreateOrganizationRequest request)
        {
            // if (!ModelState.IsValid)
            //     return ValidationProblem(ModelState);
            var response = await _organizationService.CreateForUserAsync(userId, request);
            return Ok(response);
        }
    }
}