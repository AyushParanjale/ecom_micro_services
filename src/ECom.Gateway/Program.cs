using ECom.Shared.Auth;
using ECom.Shared.Hosting;
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

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173"];
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

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
