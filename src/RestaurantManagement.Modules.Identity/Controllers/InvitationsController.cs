using Microsoft.AspNetCore.Authorization;
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
        [Authorize]
        public async Task<ActionResult<InvitationResponse>> Create([FromBody] CreateInvitationRequest request)
        {
            var language = Request.Headers.AcceptLanguage.ToString().TrimStart().StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt" : "en";
            return Ok(await _invitationService.CreateAsync(request, language));
        }

        // DELETE api/invitations/{invitationId}
        [HttpDelete("{invitationId:guid}")]
        public Task<IActionResult> Cancel(Guid invitationId) => throw new NotImplementedException();

        // POST api/invitations/accept
        [HttpPost("accept")]
        public Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request) => throw new NotImplementedException();
    }
}
