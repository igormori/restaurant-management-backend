using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services.Invitations;

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
        [Authorize]
        public async Task<ActionResult<InvitationResponse>> Create([FromBody] CreateInvitationRequest request)
        {
            var language = Request.Headers.AcceptLanguage.ToString().TrimStart().StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : "en";
            return Ok(await _invitationService.CreateAsync(request, language));
        }

        // DELETE api/invitations/{invitationId}
        [HttpDelete("{invitationId:guid}")]
        [Authorize]
        public async Task<IActionResult> Cancel(Guid invitationId)
        {
            await _invitationService.CancelAsync(invitationId);
            return NoContent();
        }

        // POST api/invitations/accept
        [HttpPost("accept")]
        [Authorize]
        public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request)
        {
            await _invitationService.AcceptAsync(request);
            return NoContent();
        }
    }
}
