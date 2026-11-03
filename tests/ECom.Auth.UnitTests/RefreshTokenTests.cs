namespace ECom.Auth.UnitTests;

using ECom.Auth.Api.Entities;
using ECom.Shared.Exceptions;

[TestClass]
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static RefreshToken Create(
        Guid? userId = null,
        string hash = "abc123",
        Guid? familyId = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? expiresAt = null) =>
        new(userId ?? Guid.NewGuid(), hash, familyId ?? Guid.NewGuid(), createdAt ?? Now, expiresAt ?? Now.AddDays(7));

    [TestMethod]
    public void Constructor_ValidInputs_CreatesActiveToken()
    {
        var token = Create();

        Assert.AreNotEqual(Guid.Empty, token.Id);
        Assert.IsFalse(token.IsRevoked);
        Assert.IsNull(token.RevokedAt);
        Assert.IsNull(token.ReplacedByTokenId);
        Assert.IsTrue(token.IsActive(Now));
    }

    [TestMethod]
    public void Constructor_EmptyUserId_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => Create(userId: Guid.Empty));

    [TestMethod]
    public void Constructor_EmptyFamilyId_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => Create(familyId: Guid.Empty));

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Constructor_BlankHash_ThrowsDomainException(string hash) =>
        Assert.ThrowsExactly<DomainException>(() => Create(hash: hash));

    [TestMethod]
    public void Constructor_ExpiryNotAfterCreation_ThrowsDomainException()
    {
        Assert.ThrowsExactly<DomainException>(() => Create(expiresAt: Now));
        Assert.ThrowsExactly<DomainException>(() => Create(expiresAt: Now.AddSeconds(-1)));
    }

    [TestMethod]
    public void IsExpired_BeforeExpiry_False_AtAndAfterExpiry_True()
    {
        var token = Create();

        Assert.IsFalse(token.IsExpired(Now.AddDays(7).AddTicks(-1)));
        Assert.IsTrue(token.IsExpired(Now.AddDays(7)));
        Assert.IsTrue(token.IsExpired(Now.AddDays(8)));
    }

    [TestMethod]
    public void Revoke_MarksRevokedAndRecordsReplacement()
    {
        var token = Create();
        var replacement = Guid.NewGuid();

        token.Revoke(Now.AddMinutes(1), replacement);

        Assert.IsTrue(token.IsRevoked);
        Assert.AreEqual(Now.AddMinutes(1), token.RevokedAt);
        Assert.AreEqual(replacement, token.ReplacedByTokenId);
        Assert.IsFalse(token.IsActive(Now.AddMinutes(1)));
    }

    [TestMethod]
    public void Revoke_AlreadyRevoked_KeepsOriginalRevocation()
    {
        var token = Create();
        var first = Guid.NewGuid();
        token.Revoke(Now.AddMinutes(1), first);

        token.Revoke(Now.AddMinutes(30), Guid.NewGuid());

        Assert.AreEqual(Now.AddMinutes(1), token.RevokedAt);
        Assert.AreEqual(first, token.ReplacedByTokenId);
    }
}
