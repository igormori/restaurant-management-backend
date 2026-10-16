using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.RateLimiting;

namespace RestaurantManagement.Modules.Identity.Controllers
{
    [ApiController]
    [Route("api/auth")]
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
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.Register)]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            var response = await _registrationService.RegisterAsync(request);
            return Ok(response);
        }

        // POST api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.Login)]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            var result = await _sessionService.LoginAsync(request, RefreshTokenCookie.ClientOf(HttpContext));
            return RespondWithCookie(result);
        }

        // POST api/auth/refresh
        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
        public async Task<ActionResult<AuthResponse>> Refresh()
        {
            var result = await _sessionService.RefreshTokenAsync(RefreshTokenCookie.Read(Request));
            return RespondWithCookie(result);
        }

        // POST api/auth/select-organization
        [HttpPost("select-organization")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> SelectOrganization([FromBody] SelectOrganizationRequest request)
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var userId))
                return Unauthorized();

            var result = await _sessionService.SelectOrganizationAsync(
                userId, request, RefreshTokenCookie.Read(Request), RefreshTokenCookie.ClientOf(HttpContext));
            return RespondWithCookie(result);
        }

        // POST api/auth/logout
        [HttpPost("logout")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.Refresh)]
        public async Task<IActionResult> Logout()
        {
            await _sessionService.LogoutAsync(RefreshTokenCookie.Read(Request));
            RefreshTokenCookie.Clear(Response);
            return NoContent();
        }

        // POST api/auth/logout-all
        [HttpPost("logout-all")]
        [Authorize]
        public async Task<IActionResult> LogoutAll()
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var userId))
                return Unauthorized();

            await _sessionService.LogoutAllAsync(userId);
            RefreshTokenCookie.Clear(Response);
            return NoContent();
        }

        private ActionResult<AuthResponse> RespondWithCookie(SessionResult result)
        {
            RefreshTokenCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);
            return Ok(result.Auth);
        }

        // POST api/auth/verify
        [HttpPost("verify")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.Verify)]
        public async Task<ActionResult<MessageResponse>> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var response = await _verificationService.VerifyEmailAsync(request);
            return Ok(response);
        }

        // POST api/auth/resend-verification
        [HttpPost("resend-verification")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.ResendVerification)]
        public async Task<ActionResult<MessageResponse>> ResendVerification([FromBody] ResendVerificationRequest request)
        {
            var response = await _verificationService.ResendVerificationCodeAsync(request);
            return Ok(response);
        }

        // POST api/auth/forgot-password
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.ForgotPassword)]
        public async Task<ActionResult<MessageResponse>> ForgotPassword(
            [FromBody] ForgotPasswordRequest request, [FromServices] IPasswordResetService resetService)
        {
            return Ok(await resetService.RequestResetCodeAsync(request));
        }

        // POST api/auth/reset-password
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting(AuthRateLimitPolicies.ResetPassword)]
        public async Task<ActionResult<MessageResponse>> ResetPassword(
            [FromBody] ResetPasswordRequest request, [FromServices] IPasswordResetService resetService)
        {
            return Ok(await resetService.ResetPasswordAsync(request));
        }

        // POST api/auth/change-password
        [HttpPost("change-password")]
        [Authorize]
        [EnableRateLimiting(AuthRateLimitPolicies.ChangePassword)]
        public async Task<ActionResult<MessageResponse>> ChangePassword(
            [FromBody] ChangePasswordRequest request, [FromServices] IPasswordChangeService changeService)
        {
            var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(subject, out var userId))
                return Unauthorized();

            var response = await changeService.ChangePasswordAsync(userId, request);
            RefreshTokenCookie.Clear(Response);
            return Ok(response);
        }
    }
}
