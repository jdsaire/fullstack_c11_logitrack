using LogiTrack.Models;
using Microsoft.AspNetCore.Identity;

namespace LogiTrack.Data;

/// <summary>
/// Creates the Manager role and gives it to the one account whose email is set in user-secrets
/// (Manager:Email), once that account exists. No password is stored anywhere.
/// </summary>
public class ManagerSeeder
{
    public const string ManagerRole = "Manager";

    private readonly RoleManager<IdentityRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly string? _managerEmail;
    private readonly ILogger<ManagerSeeder> _logger;

    public ManagerSeeder(
        RoleManager<IdentityRole> roles,
        UserManager<ApplicationUser> users,
        IConfiguration configuration,
        ILogger<ManagerSeeder> logger)
    {
        _roles = roles;
        _users = users;
        _managerEmail = configuration["Manager:Email"];
        _logger = logger;
    }

    /// <summary>At startup: make sure the role exists and the configured account holds it.</summary>
    public async Task SeedAsync()
    {
        await EnsureRoleAsync();

        if (string.IsNullOrWhiteSpace(_managerEmail))
        {
            _logger.LogWarning("Manager:Email is not set; no account will be given the Manager role.");
            return;
        }

        var user = await _users.FindByEmailAsync(_managerEmail);
        if (user is not null)
        {
            await PromoteIfConfiguredAsync(user);
        }
    }

    /// <summary>After a registration: promote the account if its email is the configured one.</summary>
    public async Task PromoteIfConfiguredAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(_managerEmail)
            || !string.Equals(user.Email, _managerEmail, StringComparison.OrdinalIgnoreCase)
            || await _users.IsInRoleAsync(user, ManagerRole))
        {
            return;
        }

        await EnsureRoleAsync();
        await _users.AddToRoleAsync(user, ManagerRole);
    }

    private async Task EnsureRoleAsync()
    {
        if (!await _roles.RoleExistsAsync(ManagerRole))
        {
            await _roles.CreateAsync(new IdentityRole(ManagerRole));
        }
    }
}
