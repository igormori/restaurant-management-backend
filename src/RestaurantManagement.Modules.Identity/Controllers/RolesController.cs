using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/roles")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly IAccessGuard _accessGuard;

        public RolesController(IRoleService roleService, IAccessGuard accessGuard)
        {
            _roleService = roleService;
            _accessGuard = accessGuard;
        }

        // GET api/roles
        [HttpGet]
        public async Task<ActionResult<List<RoleResponse>>> List()
        {
            await _accessGuard.RequireAnyAccessAsync();
            return Ok(await _roleService.ListAsync());
        }

        // GET api/roles/{roleId}
        [HttpGet("{roleId:guid}")]
        public async Task<ActionResult<RoleResponse>> Get(Guid roleId)
        {
            await _accessGuard.RequireAnyAccessAsync();
            return Ok(await _roleService.GetAsync(roleId));
        }

        // POST api/roles
        [HttpPost]
        public async Task<ActionResult<RoleResponse>> Create([FromBody] CreateRoleRequest request) => Ok(await _roleService.CreateAsync(request));

        // PUT api/roles/{roleId}/levels
        [HttpPut("{roleId:guid}/levels")]
        public async Task<ActionResult<RoleResponse>> UpdateLevels(Guid roleId, [FromBody] UpdateRoleLevelsRequest request)
            => Ok(await _roleService.UpdateLevelsAsync(roleId, request));

        // PUT api/roles/{roleId}/name
        [HttpPut("{roleId:guid}/name")]
        public async Task<ActionResult<RoleResponse>> Rename(Guid roleId, [FromBody] RenameRoleRequest request)
            => Ok(await _roleService.RenameAsync(roleId, request));
    }
}
