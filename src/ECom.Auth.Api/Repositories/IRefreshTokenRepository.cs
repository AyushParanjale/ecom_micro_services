namespace ECom.Auth.Api.Repositories;

using ECom.Auth.Api.Entities;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically revokes a token only if it is still active. Returns false when another request
    /// already consumed it, which lets the caller detect concurrent reuse.
    /// </summary>
    Task<bool> TryRevokeAsync(
        Guid tokenId,
        DateTimeOffset now,
        Guid? replacedByTokenId,
        CancellationToken cancellationToken = default);

    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
