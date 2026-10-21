using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services.Members;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/members")]
    [Authorize]
    public class MembersController : ControllerBase
    {
        private readonly IMemberAccessService _memberAccessService;

        public MembersController(IMemberAccessService memberAccessService)
        {
            _memberAccessService = memberAccessService;
        }

        // PUT api/members/{userId}/access/{locationId}
        // The delegation rules do the authorization, so the action declares no policy of its own.
        [HttpPut("{userId:guid}/access/{locationId:guid}")]
        public async Task<ActionResult<MemberLocationAccessResponse>> SetAccess(Guid userId, Guid locationId, [FromBody] SetMemberAccessRequest request)
        {
            var response = await _memberAccessService.SetAccessAsync(userId, locationId, request.RoleNames, request.Levels);
            return Ok(response);
        }
    }
}
