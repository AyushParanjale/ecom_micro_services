namespace ECom.Auth.UnitTests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECom.Auth.Api.Services;
using ECom.Shared.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

[TestClass]
public sealed class TokenServiceTests
{
    private static JwtOptions ValidOptions => new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "super-secret-key-that-is-at-least-32-chars-long!!",
        ExpirationMinutes = 60,
    };

    private static TokenService CreateService(JwtOptions? options = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(options ?? ValidOptions);
        return new TokenService(opts);
    }

    [TestMethod]
    public void GenerateToken_ValidInputs_ReturnsNonEmptyAccessToken()
    {
        var svc = CreateService();
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [TestMethod]
    public void GenerateToken_TokenType_IsBearer()
    {
        var svc = CreateService();
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.AreEqual("Bearer", result.TokenType);
    }

    [TestMethod]
    public void GenerateToken_ExpiresAtUtc_IsInFuture()
    {
        var before = DateTimeOffset.UtcNow;
        var svc = CreateService();
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.IsTrue(result.ExpiresAtUtc > before);
    }

    [TestMethod]
    public void GenerateToken_ExpiresAtUtc_RespectsExpirationMinutes()
    {
        var opts = ValidOptions with { ExpirationMinutes = 30 };
        var svc = CreateService(opts);
        var before = DateTimeOffset.UtcNow;
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        Assert.IsTrue(result.ExpiresAtUtc <= before.AddMinutes(31));
        Assert.IsTrue(result.ExpiresAtUtc >= before.AddMinutes(29));
    }

    [TestMethod]
    public void GenerateToken_TokenContainsUserIdAsSubject()
    {
        var userId = Guid.NewGuid();
        var svc = CreateService();
        var result = svc.GenerateToken(userId, "user@example.com", "Test User", "Customer");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);
        var sub = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        Assert.AreEqual(userId.ToString(), sub);
    }

    [TestMethod]
    public void GenerateToken_TokenContainsRoleClaim()
    {
        var svc = CreateService();
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Admin");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);
        var role = token.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
        Assert.AreEqual("Admin", role);
    }

    [TestMethod]
    public void GenerateToken_TokenContainsExpectedIssuer()
    {
        var svc = CreateService();
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);
        Assert.AreEqual(ValidOptions.Issuer, token.Issuer);
    }

    [TestMethod]
    public void GenerateToken_TokenContainsExpectedAudience()
    {
        var svc = CreateService();
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);
        Assert.IsTrue(token.Audiences.Contains(ValidOptions.Audience));
    }

    [TestMethod]
    public void GenerateToken_TokenValidatesWithCorrectKey()
    {
        var opts = ValidOptions;
        var svc = CreateService(opts);
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");

        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = opts.Issuer,
            ValidAudience = opts.Audience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero,
        };

        var principal = handler.ValidateToken(result.AccessToken, validationParams, out _);
        Assert.IsNotNull(principal);
    }

    [TestMethod]
    public void GenerateToken_TokenRejectedWithWrongKey()
    {
        var svc = CreateService();
        var result = svc.GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Customer");

        var handler = new JwtSecurityTokenHandler();
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-key-that-is-at-least-32-chars-long!!!!"));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            IssuerSigningKey = wrongKey,
        };

        Assert.ThrowsExactly<SecurityTokenSignatureKeyNotFoundException>(() =>
            handler.ValidateToken(result.AccessToken, validationParams, out _));
    }
}
