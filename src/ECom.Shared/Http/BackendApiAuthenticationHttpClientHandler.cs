namespace ECom.Shared.Http;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

/// <summary>
/// Forwards the incoming request's bearer token on east-west calls so the user's identity
/// travels along the call chain and downstream services authorize against the same user.
/// </summary>
public sealed class BackendApiAuthenticationHttpClientHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BackendApiAuthenticationHttpClientHandler(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();

        if (!string.IsNullOrWhiteSpace(authorization) &&
            AuthenticationHeaderValue.TryParse(authorization, out var header) &&
            string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = header;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
