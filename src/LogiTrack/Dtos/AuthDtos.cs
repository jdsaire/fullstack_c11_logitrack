using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Dtos;

/// <summary>Body of POST /api/auth/register. There is no role field: register never grants a role.</summary>
public class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Body of POST /api/auth/login.</summary>
public class LoginRequest
{
    [Required]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>A signed access token and when it expires.</summary>
public record TokenResponse(string Token, DateTime ExpiresAt);
