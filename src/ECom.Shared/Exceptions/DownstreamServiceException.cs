namespace ECom.Shared.Exceptions;

/// <summary>
/// Raised when a downstream microservice is unreachable or returns an unexpected failure.
/// </summary>
public sealed class DownstreamServiceException : Exception
{
    public string ServiceName { get; }

    public DownstreamServiceException(string serviceName, string message) : base(message)
        => ServiceName = serviceName;

    public DownstreamServiceException(string serviceName, string message, Exception inner) : base(message, inner)
        => ServiceName = serviceName;
}
