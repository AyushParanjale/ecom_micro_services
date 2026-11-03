namespace ECom.Auth.Api.DTOs;

/// <summary>A freshly minted refresh token. <see cref="Token"/> is shown to the client once; only <see cref="TokenHash"/> is persisted.</summary>
public sealed record RefreshTokenResult(string Token, string TokenHash, DateTimeOffset ExpiresAtUtc);
