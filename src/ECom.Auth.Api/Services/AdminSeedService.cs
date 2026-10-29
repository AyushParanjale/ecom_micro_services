namespace ECom.Auth.Api.Services;

using ECom.Auth.Api.Entities;
using Microsoft.AspNetCore.Identity;

public sealed class AdminSeedService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeedService> _logger;

    public AdminSeedService(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<AdminSeedService> logger)
    {
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAdminAsync()
    {
        var email = _configuration["AdminSeed:Email"];
        var password = _configuration["AdminSeed:Password"];
        var name = _configuration["AdminSeed:Name"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("Admin seed skipped: AdminSeed:Email or AdminSeed:Password not configured.");
            return;
        }

        var existing = await _userManager.FindByEmailAsync(email.Trim());
        if (existing is not null)
        {
            _logger.LogInformation("Admin seed skipped: admin account already exists.");
            return;
        }

        var user = new ApplicationUser(email.Trim(), name.Trim(), UserRole.Admin);
        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
            _logger.LogInformation("Admin account created.");
        else
            _logger.LogWarning("Admin seed failed: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
