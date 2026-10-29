using ECom.Auth.Api.Data;
using ECom.Auth.Api.Entities;
using ECom.Auth.Api.Services;
using ECom.Shared.Auth;
using ECom.Shared.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

await EComHostingExtensions.RunEComAsync(async () =>
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddEComSerilog();
    builder.Services.AddEComApiDefaults("ECom Auth API", "Identity: registration, login and JWT issuing");

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

    builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AuthDbContext>();

    builder.Services.AddEComJwtAuthentication(builder.Configuration);

    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<AdminSeedService>();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<AdminSeedService>().SeedAdminAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Admin seed failed.");
        }
    }

    app.UseEComApiDefaults("ECom Auth API");

    await app.RunAsync();
});

public partial class Program { }
