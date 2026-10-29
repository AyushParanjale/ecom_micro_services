namespace ECom.Shared.Exceptions;

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message = "Invalid email or password.") : base(message) { }
}
