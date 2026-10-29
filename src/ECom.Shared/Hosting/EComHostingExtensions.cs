namespace ECom.Shared.Hosting;

using System.Text.Json.Serialization;
using ECom.Shared.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Sinks.MSSqlServer;

public static class EComHostingExtensions
{
    /// <summary>
    /// Runs a service entry point with a Serilog bootstrap logger and fatal-error logging.
    /// </summary>
    public static async Task RunEComAsync(Func<Task> run)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            await run();
        }
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Serilog configured from appsettings, plus a SQL Server sink (warnings and above) into the
    /// service's own database when <c>ConnectionStrings:DefaultConnection</c> is set.
    /// </summary>
    public static WebApplicationBuilder AddEComSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", context.HostingEnvironment.ApplicationName);

            var connStr = context.Configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connStr))
            {
                var dbSection = context.Configuration.GetSection("SerilogDb");
                var tableName = dbSection["TableName"] ?? "Logs";
                var autoCreate = bool.TryParse(dbSection["AutoCreateSqlTable"], out var b) && b;
                configuration.WriteTo.MSSqlServer(
                    connectionString: connStr,
                    sinkOptions: new MSSqlServerSinkOptions { TableName = tableName, AutoCreateSqlTable = autoCreate },
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
            }
        });

        return builder;
    }

    /// <summary>
    /// Controllers (enums as strings), Swagger with a bearer definition, ProblemDetails,
    /// the shared <see cref="GlobalExceptionHandler"/> and health checks.
    /// </summary>
    public static IServiceCollection AddEComApiDefaults(this IServiceCollection services, string title, string description)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = title,
                Version = "v1",
                Description = description
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token (without the 'Bearer ' prefix)"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHealthChecks();
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Standard pipeline: Swagger (Development), exception handler, request logging,
    /// authentication/authorization, controllers and <c>/health</c>.
    /// </summary>
    public static WebApplication UseEComApiDefaults(this WebApplication app, string title)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{title} v1"));
        }

        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");

        return app;
    }
}
