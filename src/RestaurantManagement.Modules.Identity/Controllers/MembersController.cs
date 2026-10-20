using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/members")]
    [Authorize]
    public class MembersController : ControllerBase
    {
        private readonly IMemberAccessService _memberAccessService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public MembersController(IMemberAccessService memberAccessService, IStringLocalizer<SharedResource> localizer)
        {
            _memberAccessService = memberAccessService;
            _localizer = localizer;
        }

        // PUT api/members/{userId}/access/{locationId}
        // The delegation rules do the authorization, so the action declares no policy of its own.
        [HttpPut("{userId:guid}/access/{locationId:guid}")]
        public async Task<ActionResult<MemberLocationAccessResponse>> SetAccess(Guid userId, Guid locationId, [FromBody] SetMemberAccessRequest request)
        {
            var changedLevels = ParseLevels(request.Levels);
            var response = await _memberAccessService.SetAccessAsync(userId, locationId, request.RoleNames, changedLevels);
            return Ok(response);
        }

        private Dictionary<AccessModule, AccessLevel> ParseLevels(Dictionary<string, string> levels)
        {
            var parsed = new Dictionary<AccessModule, AccessLevel>();
            foreach (var (moduleName, levelName) in levels)
            {
                if (!Enum.TryParse<AccessModule>(moduleName, true, out var module) || !Enum.IsDefined(module)
                    || !Enum.TryParse<AccessLevel>(levelName, true, out var level) || !Enum.IsDefined(level))
                    throw new BusinessException(_localizer["ValidationFailed"].Value, 400);

                parsed[module] = level;
            }

            return parsed;
        }
    }
}
