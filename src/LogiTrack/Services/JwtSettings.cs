namespace LogiTrack.Services;

/// <summary>Token settings. Issuer, audience and expiry live in appsettings; the key lives in user-secrets.</summary>
public class JwtSettings
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;

    public string Key { get; set; } = string.Empty;
}
