using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceHub.Api.Authentication;
using ServiceHub.Api.Contracts.Auth;
using ServiceHub.Api.Services;

namespace ServiceHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = await auth.RegisterAsync(request, cancellationToken);
        if (errors.Length > 0)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["registration"] = errors })
                { Status = 400, Title = "Registration failed." });
        return StatusCode(StatusCodes.Status201Created,
            new RegistrationResponse("Account created. Please log in."));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await auth.LoginAsync(request);
        return response is null
            ? Problem(statusCode: 401, title: "Invalid email or password.")
            : Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id))
            return Unauthorized();
        var user = await auth.GetCurrentUserAsync(id);
        return user is null ? Unauthorized() : Ok(user);
    }

    // Authorization demonstration only; no business data or management operations.
    [Authorize(Roles = ApplicationRoles.BusinessOwner)]
    [HttpGet("business-owner")]
    public IActionResult BusinessOwnerAccess() => Ok(new { message = "BusinessOwner access confirmed." });
}
