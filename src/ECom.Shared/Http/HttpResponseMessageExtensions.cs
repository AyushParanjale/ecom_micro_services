namespace ECom.Shared.Http;

using System.Net;
using System.Net.Http.Json;
using ECom.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;

public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Translates a non-success downstream response back into the shared exception types so the
    /// caller's <c>GlobalExceptionHandler</c> produces the same ProblemDetails as the monolith did.
    /// </summary>
    public static async Task EnsureSuccessOrThrowAsync(
        this HttpResponseMessage response,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
            return;

        var problem = await TryReadProblemAsync(response, cancellationToken);
        var detail = problem?.Detail ?? $"{serviceName} returned {(int)response.StatusCode}.";

        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new NotFoundException(detail),
            HttpStatusCode.Conflict => problem?.Title is { Length: > 0 } title
                ? new ConflictException(detail, title)
                : new ConflictException(detail),
            HttpStatusCode.BadRequest => new DomainException(detail),
            _ => new DownstreamServiceException(serviceName,
                $"{serviceName} returned {(int)response.StatusCode} {response.ReasonPhrase}: {detail}")
        };
    }

    /// <summary>Sends a request and wraps transport failures in <see cref="DownstreamServiceException"/>.</summary>
    public static async Task<HttpResponseMessage> SendDownstreamAsync(
        this HttpClient client,
        HttpRequestMessage request,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new DownstreamServiceException(serviceName, $"{serviceName} is unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamServiceException(serviceName, $"{serviceName} timed out.", ex);
        }
    }

    private static async Task<ProblemDetails?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            if (response.Content.Headers.ContentLength == 0)
                return null;
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
