namespace ECom.Shared.Exceptions;

public sealed class GatewayException : Exception
{
    public GatewayException(string message) : base(message) { }
    public GatewayException(string message, Exception inner) : base(message, inner) { }
}
