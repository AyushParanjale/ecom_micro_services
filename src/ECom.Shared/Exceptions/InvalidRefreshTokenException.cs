namespace ECom.Shared.Exceptions;

public sealed class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException(string message = "Invalid or expired refresh token.") : base(message) { }
}
