using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/members")]
    public class MembersController : ControllerBase
    {
        // PUT api/members/{userId}/access/{locationId}
        [HttpPut("{userId:guid}/access/{locationId:guid}")]
        public Task<ActionResult<MemberLocationAccessResponse>> SetAccess(Guid userId, Guid locationId, [FromBody] SetMemberAccessRequest request)
            => throw new NotImplementedException();
    }
}
