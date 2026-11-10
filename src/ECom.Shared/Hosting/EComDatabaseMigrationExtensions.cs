namespace ECom.Shared.Hosting;

using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class EComDatabaseMigrationExtensions
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    /// <summary>
    /// Creates the database if missing and applies any migrations not yet recorded in
    /// <c>__EFMigrationsHistory</c>. Disable with <c>Database:MigrateOnStartup=false</c>.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TContext>(this WebApplication app, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        if (!app.Configuration.GetValue(MigrateOnStartupKey, true))
        {
            app.Logger.LogInformation("Startup migration disabled for {DbContext}.", typeof(TContext).Name);
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        if (!db.Database.IsRelational())
        {
            return;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            app.Logger.LogInformation("{DbContext} database is up to date.", typeof(TContext).Name);
            return;
        }

        app.Logger.LogInformation(
            "Applying {Count} pending migration(s) to {DbContext}: {Migrations}",
            pending.Count, typeof(TContext).Name, string.Join(", ", pending));

        // EF Core takes a database-level lock here, so concurrent instances won't apply migrations twice.
        await db.Database.MigrateAsync(cancellationToken);

        app.Logger.LogInformation("{DbContext} migrations applied.", typeof(TContext).Name);
    }
}
