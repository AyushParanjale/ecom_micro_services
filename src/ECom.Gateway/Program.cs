using ECom.Shared.Auth;
using ECom.Shared.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;

await EComHostingExtensions.RunEComAsync(async () =>
{
    var builder = WebApplication.CreateBuilder(args);

    // Route tables are per environment: ocelot.json (local) or ocelot.Production.json.
    var ocelotFile = builder.Environment.IsProduction() ? "ocelot.Production.json" : "ocelot.json";
    builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);

    builder.AddEComSerilog();

    // Same issuer/audience/key as every service; routes with AuthenticationOptions are validated here.
    builder.Services.AddEComJwtAuthentication(builder.Configuration);

    builder.Services.AddCors();
    // Bound from configuration at resolve time so environment variables
    // (e.g. Cors__AllowedOrigins__0) and test overrides are honoured.
    builder.Services.AddOptions<CorsOptions>()
        .Configure<IConfiguration>((options, configuration) =>
        {
            var allowedOrigins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().TrimEnd('/'))
                .ToArray();

            options.AddDefaultPolicy(policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });

    builder.Services.AddHealthChecks();
    builder.Services.AddOcelot(builder.Configuration);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseHealthChecks("/health");
    app.UseWebSockets();
    await app.UseOcelot();

    await app.RunAsync();
});

public partial class Program { }
