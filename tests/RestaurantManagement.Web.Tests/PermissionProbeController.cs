using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Test-only endpoints, registered by <see cref="PermissionProbeApiFactory"/>, that stand in for
/// real features: one per permission policy, and one that declares no access rule at all.
/// </summary>
[ApiController]
[Route("api/test-probe")]
public class PermissionProbeController : ControllerBase
{
    [HttpGet("members")]
    [Authorize(Policy = Permissions.MembersManage)]
    public IActionResult Members() => Ok();

    [HttpGet("menu")]
    [Authorize(Policy = Permissions.MenuManage)]
    public IActionResult Menu() => Ok();

    [HttpGet("reports")]
    [Authorize(Policy = Permissions.ReportsView)]
    public IActionResult Reports() => Ok();

    [HttpGet("undeclared")]
    public IActionResult Undeclared() => Ok();
}
