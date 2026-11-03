namespace ECom.Shared.Auth;

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";
    public const int EncryptionKeyByteLength = 32;

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Base64-encoded 32-byte key used to encrypt access tokens (JWE, A128CBC-HS256). Must differ from <see cref="SigningKey"/>.</summary>
    public string EncryptionKey { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 7;

    /// <summary>Decodes <see cref="EncryptionKey"/>, or returns null when it is not valid base64 of the required length.</summary>
    public byte[]? TryGetEncryptionKeyBytes()
    {
        if (string.IsNullOrWhiteSpace(EncryptionKey))
            return null;

        Span<byte> buffer = stackalloc byte[EncryptionKeyByteLength + 3];
        return Convert.TryFromBase64String(EncryptionKey, buffer, out var written) && written == EncryptionKeyByteLength
            ? buffer[..written].ToArray()
            : null;
    }
}
