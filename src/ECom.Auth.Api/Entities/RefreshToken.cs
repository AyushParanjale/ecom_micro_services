namespace ECom.Auth.Api.Entities;

using ECom.Shared.Exceptions;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public Guid FamilyId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsRevoked => RevokedAt.HasValue;

    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(userId));
        if (familyId == Guid.Empty)
            throw new ArgumentException("FamilyId must not be empty.", nameof(familyId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new DomainException("TokenHash is required.");
        if (expiresAt <= createdAt)
            throw new DomainException("Refresh token expiry must be after its creation time.");

        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>Idempotent: revoking an already-revoked token keeps the original revocation.</summary>
    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
            return;

        RevokedAt = now;
        ReplacedByTokenId = replacedByTokenId;
    }
}
