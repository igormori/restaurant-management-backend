using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/invitations")]
    public class InvitationsController : ControllerBase
    {
        private readonly IInvitationService _invitationService;

        public InvitationsController(IInvitationService invitationService) => _invitationService = invitationService;

        // POST api/invitations
        [HttpPost]
        public Task<ActionResult<InvitationResponse>> Create([FromBody] CreateInvitationRequest request)
            => throw new NotImplementedException();

        // DELETE api/invitations/{invitationId}
        [HttpDelete("{invitationId:guid}")]
        public Task<IActionResult> Cancel(Guid invitationId) => throw new NotImplementedException();

        // POST api/invitations/accept
        [HttpPost("accept")]
        public Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request) => throw new NotImplementedException();
    }
}
