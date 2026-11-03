namespace ECom.Auth.UnitTests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECom.Auth.Api.Services;
using ECom.Shared.Auth;
using Microsoft.IdentityModel.Tokens;

[TestClass]
public sealed class TokenServiceTests
{
    private const string EncryptionKeyBase64 = "dGVzdC1lbmNyeXB0aW9uLWtleS0zMi1ieXRlcyEhISE="; // 32 bytes
    private const string OtherEncryptionKeyBase64 = "REVWLU9OTFktTk9ULUEtU0VDUkVULUVOQ1JZUFQtSzE="; // 32 bytes

    private static readonly DateTimeOffset FixedNow = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static JwtOptions ValidOptions => new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "super-secret-key-that-is-at-least-32-chars-long!!",
        EncryptionKey = EncryptionKeyBase64,
        ExpirationMinutes = 60,
        RefreshTokenExpirationDays = 7,
    };

    private static TokenService CreateService(JwtOptions? options = null, TimeProvider? timeProvider = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? ValidOptions);
        return new TokenService(opts, timeProvider ?? TimeProvider.System);
    }

    private static TokenValidationParameters ValidationParameters(
        JwtOptions opts,
        string? decryptionKeyBase64 = null,
        SecurityKey? signingKey = null) => new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = opts.Issuer,
            ValidAudience = opts.Audience,
            IssuerSigningKey = signingKey ?? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey)),
            TokenDecryptionKey = new SymmetricSecurityKey(Convert.FromBase64String(decryptionKeyBase64 ?? opts.EncryptionKey)),
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role,
        };

    private static ClaimsPrincipal Decrypt(string token) =>
        new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters(ValidOptions), out _);

    [TestMethod]
    public void GenerateToken_ValidInputs_ReturnsNonEmptyAccessToken()
    {
        var result = CreateService().GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [TestMethod]
    public void GenerateToken_TokenType_IsBearer()
    {
        var result = CreateService().GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.AreEqual("Bearer", result.TokenType);
    }

    [TestMethod]
    public void GenerateToken_ExpiresAtUtc_RespectsExpirationMinutes()
    {
        var opts = ValidOptions with { ExpirationMinutes = 30 };
        var result = CreateService(opts, new FixedTimeProvider(FixedNow))
            .GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.AreEqual(FixedNow.AddMinutes(30), result.ExpiresAtUtc);
    }

    [TestMethod]
    public void JwtOptions_Defaults_AreFifteenMinutesAndSevenDays()
    {
        Assert.AreEqual(15, new JwtOptions().ExpirationMinutes);
        Assert.AreEqual(7, new JwtOptions().RefreshTokenExpirationDays);
    }

    // --- JWE ---

    [TestMethod]
    public void GenerateToken_ProducesFiveSegmentJwe()
    {
        var result = CreateService().GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.HasCount(5, result.AccessToken.Split('.'));
    }

    [TestMethod]
    public void GenerateToken_PayloadIsNotReadableWithoutDecryption()
    {
        var userId = Guid.NewGuid();
        var result = CreateService().GenerateToken(userId, "user@example.com", "Test User", "Admin");

        Assert.IsFalse(result.AccessToken.Contains(userId.ToString(), StringComparison.OrdinalIgnoreCase));
        foreach (var segment in result.AccessToken.Split('.').Where(s => s.Length > 0))
        {
            string decoded;
            try { decoded = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(segment)); }
            catch (FormatException) { continue; }
            Assert.DoesNotContain("user@example.com", decoded);
            Assert.DoesNotContain("Admin", decoded);
        }
    }

    [TestMethod]
    public void GenerateToken_TokenContainsUserIdAsSubject()
    {
        var userId = Guid.NewGuid();
        var principal = Decrypt(CreateService().GenerateToken(userId, "user@example.com", "Test User", "Customer").AccessToken);
        Assert.AreEqual(userId.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [TestMethod]
    public void GenerateToken_TokenContainsRoleClaim()
    {
        var principal = Decrypt(CreateService().GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Admin").AccessToken);
        Assert.IsTrue(principal.IsInRole("Admin"));
    }

    [TestMethod]
    public void GenerateToken_TokenContainsExpectedIssuerAndAudience()
    {
        var token = CreateService().GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer").AccessToken;
        new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters(ValidOptions), out var validated);

        var jwt = (JwtSecurityToken)validated;
        Assert.AreEqual(ValidOptions.Issuer, jwt.Issuer);
        Assert.IsTrue(jwt.Audiences.Contains(ValidOptions.Audience));
    }

    [TestMethod]
    public void GenerateToken_TokenRejectedWithWrongSigningKey()
    {
        var token = CreateService().GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer").AccessToken;
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-key-that-is-at-least-32-chars-long!!!!"));
        var parameters = ValidationParameters(ValidOptions, signingKey: wrongKey);

        Assert.ThrowsExactly<SecurityTokenSignatureKeyNotFoundException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _));
    }

    [TestMethod]
    public void GenerateToken_TokenRejectedWithWrongDecryptionKey()
    {
        var token = CreateService().GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer").AccessToken;
        var parameters = ValidationParameters(ValidOptions, decryptionKeyBase64: OtherEncryptionKeyBase64);

        Assert.ThrowsExactly<SecurityTokenDecryptionFailedException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _));
    }

    [TestMethod]
    public void GenerateToken_ExpiredToken_IsRejected()
    {
        var svc = CreateService(timeProvider: new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(-3)));
        var token = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer").AccessToken;

        Assert.ThrowsExactly<SecurityTokenExpiredException>(() => Decrypt(token));
    }

    [TestMethod]
    public void GenerateToken_InvalidEncryptionKey_Throws()
    {
        var svc = CreateService(ValidOptions with { EncryptionKey = "not-base64!!" });
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer"));
    }

    // --- Options ---

    [TestMethod]
    public void JwtOptions_EncryptionKey_ValidBase64Of32Bytes_Decodes()
    {
        Assert.HasCount(32, ValidOptions.TryGetEncryptionKeyBytes()!);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-base64!!")]
    [DataRow("CONFIGURE_VIA_USER_SECRETS_BASE64_32_BYTES")]
    [DataRow("c2hvcnQ=")] // 5 bytes
    [DataRow("dGVzdC1lbmNyeXB0aW9uLWtleS0zMi1ieXRlcyEhISEhISEh")] // > 32 bytes
    public void JwtOptions_EncryptionKey_Invalid_ReturnsNull(string key)
    {
        Assert.IsNull((ValidOptions with { EncryptionKey = key }).TryGetEncryptionKeyBytes());
    }

    // --- Refresh tokens ---

    [TestMethod]
    public void GenerateRefreshToken_ReturnsUniqueOpaqueTokens()
    {
        var svc = CreateService();
        var a = svc.GenerateRefreshToken();
        var b = svc.GenerateRefreshToken();

        Assert.AreNotEqual(a.Token, b.Token);
        Assert.IsGreaterThanOrEqualTo(80, a.Token.Length);
    }

    [TestMethod]
    public void GenerateRefreshToken_HashDiffersFromTokenAndIsStable()
    {
        var svc = CreateService();
        var r = svc.GenerateRefreshToken();

        Assert.AreNotEqual(r.Token, r.TokenHash);
        Assert.AreEqual(r.TokenHash, svc.HashRefreshToken(r.Token));
        Assert.AreEqual(64, r.TokenHash.Length);
    }

    [TestMethod]
    public void GenerateRefreshToken_ExpiryUsesConfiguredDays()
    {
        var opts = ValidOptions with { RefreshTokenExpirationDays = 3 };
        var r = CreateService(opts, new FixedTimeProvider(FixedNow)).GenerateRefreshToken();
        Assert.AreEqual(FixedNow.AddDays(3), r.ExpiresAtUtc);
    }
}
