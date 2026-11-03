namespace ECom.Auth.Api.Repositories;

using ECom.Auth.Api.Data;
using ECom.Auth.Api.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _context;

    public RefreshTokenRepository(AuthDbContext context) => _context = context;

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // AsNoTracking: revocation below uses set-based updates that bypass the change tracker,
    // so a tracked copy would go stale.
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<bool> TryRevokeAsync(
        Guid tokenId,
        DateTimeOffset now,
        Guid? replacedByTokenId,
        CancellationToken cancellationToken = default)
    {
        var affected = await _context.RefreshTokens
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, (DateTimeOffset?)now)
                .SetProperty(t => t.ReplacedByTokenId, replacedByTokenId),
                cancellationToken);

        return affected == 1;
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now), cancellationToken);
    }
}
