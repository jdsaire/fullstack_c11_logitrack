using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LogiTrack.Dtos;

namespace LogiTrack.Tests.Support;

/// <summary>Registers accounts and signs them in. Passwords are generated per test run and never stored.</summary>
public static class AuthHelper
{
    public static string NewPassword() => $"Lt!{Guid.NewGuid():N}A1";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@logitrack.test";

    public static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string? password = null)
    {
        password ??= NewPassword();

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.Created), await register.Content.ReadAsStringAsync());

        return await LoginAsync(client, email, password);
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await login.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
    }

    /// <summary>A client carrying a token for a new account (the Manager account when asked).</summary>
    public static async Task<HttpClient> SignedInClientAsync(LogiTrackFactory factory, bool asManager = false)
    {
        var client = factory.CreateClient();
        var email = asManager ? LogiTrackFactory.ManagerEmail : NewEmail();
        var token = await RegisterAndLoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
