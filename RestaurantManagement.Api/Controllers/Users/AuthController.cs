using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.Models.Users;
using RestaurantManagement.Api.Services.Users;

namespace RestaurantManagement.Api.Controllers.Users
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            var response = await _userService.RegisterAsync(request);
            return Ok(response);
        }
    }
}