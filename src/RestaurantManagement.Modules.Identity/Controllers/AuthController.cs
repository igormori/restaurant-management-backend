using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IRegistrationService _registrationService;
        private readonly ISessionService _sessionService;
        private readonly IVerificationService _verificationService;

        public AuthController(
            IRegistrationService registrationService,
            ISessionService sessionService,
            IVerificationService verificationService)
        {
            _registrationService = registrationService;
            _sessionService = sessionService;
            _verificationService = verificationService;
        }

        // POST api/auth/register
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            var response = await _registrationService.RegisterAsync(request);
            return Ok(response);
        }

        // POST api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            var response = await _sessionService.LoginAsync(request);
            return Ok(response);
        }

        // POST api/auth/refresh
        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request)
        {
            var response = await _sessionService.RefreshTokenAsync(request);
            return Ok(response);
        }

        // POST api/auth/verify
        [HttpPost("verify")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var response = await _verificationService.VerifyEmailAsync(request);
            return Ok(response);
        }

        // POST api/auth/resend-verification
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
        {
            var respoonse = await _verificationService.ResendVerificationCodeAsync(request);
            return Ok(respoonse);
        }
    }
}