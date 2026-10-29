namespace ECom.Order.UnitTests;

using System.Net;
using System.Text;

/// <summary>Records each request (and its body) and answers with a caller-supplied response or exception.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<(HttpMethod Method, Uri? Uri, string? Body)> Requests { get; } = new();

    public static FakeHttpMessageHandler Returning(HttpStatusCode status, string? json = null, string mediaType = "application/json") =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = json is null ? new StringContent(string.Empty) : new StringContent(json, Encoding.UTF8, mediaType)
        });

    public static FakeHttpMessageHandler Throwing(Exception ex) => new(_ => throw ex);

    public static HttpClient CreateClient(FakeHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://downstream.test/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri, body));
        return _respond(request);
    }
}
