namespace ECom.Shared.Exceptions;

public sealed class ConflictException : Exception
{
    public string? ConflictTitle { get; }

    public ConflictException(string message) : base(message) { }

    public ConflictException(string message, string title) : base(message) => ConflictTitle = title;
}
