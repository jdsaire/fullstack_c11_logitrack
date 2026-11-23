using LogiTrack.Dtos;
using LogiTrack.Models;
using LogiTrack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string LoginFailed = "Invalid email or password.";
    private const string RegistrationFailed = "Registration failed.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokens;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokens)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokens = tokens;
    }

    /// <summary>Creates an account. New accounts get no role.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // Password rule messages help the caller; a taken email or user name gets one generic message.
            foreach (var error in result.Errors)
            {
                var message = error.Code.StartsWith("Password", StringComparison.Ordinal)
                    ? error.Description
                    : RegistrationFailed;
                ModelState.AddModelError(nameof(request.Password), message);
            }

            return ValidationProblem();
        }

        return StatusCode(StatusCodes.Status201Created, new { email = user.Email });
    }

    /// <summary>Checks the credentials and returns a signed access token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Problem(LoginFailed, statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Problem(LoginFailed, statusCode: StatusCodes.Status401Unauthorized);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokens.CreateToken(user, roles);
        return Ok(new TokenResponse(token, expiresAt));
    }
}
