using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Test-only endpoints, registered by <see cref="PermissionProbeApiFactory"/>, that stand in for
/// real features: one per kind of access policy (module and level, such as "reports.read"), and
/// one that declares no access rule at all.
/// </summary>
[ApiController]
[Route("api/test-probe")]
public class PermissionProbeController : ControllerBase
{
    [HttpGet("members")]
    [Authorize(Policy = "members.manage")]
    public IActionResult Members() => Ok();

    [HttpGet("menu")]
    [Authorize(Policy = "menus.manage")]
    public IActionResult Menu() => Ok();

    [HttpGet("reports")]
    [Authorize(Policy = "reports.read")]
    public IActionResult Reports() => Ok();

    [HttpGet("undeclared")]
    public IActionResult Undeclared() => Ok();
}
