namespace ECom.Shared.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DownstreamClientExtensions
{
    /// <summary>
    /// Registers a typed HttpClient for another microservice. The base address comes from
    /// <c>ServiceUrls:{serviceUrlKey}</c> and the caller's bearer token is forwarded.
    /// </summary>
    public static IHttpClientBuilder AddDownstreamClient<TClient, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceUrlKey)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.AddHttpContextAccessor();
        services.AddTransient<BackendApiAuthenticationHttpClientHandler>();

        var baseUrl = configuration[$"ServiceUrls:{serviceUrlKey}"]
            ?? throw new InvalidOperationException($"'ServiceUrls:{serviceUrlKey}' is not configured.");

        return services.AddHttpClient<TClient, TImplementation>(client =>
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler<BackendApiAuthenticationHttpClientHandler>();
    }
}
