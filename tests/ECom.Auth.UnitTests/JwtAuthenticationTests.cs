namespace ECom.Auth.UnitTests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECom.Auth.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

[TestClass]
public sealed class JwtAuthenticationTests
{
    private static readonly Dictionary<string, string?> Config = new()
    {
        ["Jwt:Issuer"] = "test-issuer",
        ["Jwt:Audience"] = "test-audience",
        ["Jwt:SigningKey"] = "super-secret-key-that-is-at-least-32-chars-long!!",
        ["Jwt:EncryptionKey"] = "dGVzdC1lbmNyeXB0aW9uLWtleS0zMi1ieXRlcyEhISE=",
    };

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config ?? Config).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEComJwtAuthentication(configuration, signalRHubPath: "/hubs/orders");
        return services.BuildServiceProvider();
    }

    private static async Task<MessageReceivedContext> RunMessageReceivedAsync(
        ServiceProvider provider, string path, string? bearer = null, string? queryToken = null)
    {
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        if (bearer is not null)
            http.Request.Headers.Authorization = "Bearer " + bearer;
        if (queryToken is not null)
            http.Request.QueryString = new QueryString("?access_token=" + queryToken);

        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new MessageReceivedContext(http, scheme, options);
        await options.Events.MessageReceived(context);
        return context;
    }

    private static string IssueToken(ServiceProvider provider)
    {
        var jwt = provider.GetRequiredService<IOptions<JwtOptions>>();
        return new TokenService(jwt, TimeProvider.System)
            .GenerateToken(Guid.NewGuid(), "user@example.com", "Test User", "Admin").AccessToken;
    }

    [TestMethod]
    public void Options_EncryptedAccessToken_ValidatesAndYieldsRole()
    {
        using var provider = BuildProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(IssueToken(provider), options.TokenValidationParameters, out _);

        Assert.IsTrue(principal.IsInRole("Admin"));
    }

    [TestMethod]
    public async Task MessageReceived_EncryptedHeaderToken_IsNotRejected()
    {
        using var provider = BuildProvider();
        var ctx = await RunMessageReceivedAsync(provider, "/api/x", bearer: IssueToken(provider));
        Assert.IsNull(ctx.Result);
    }

    [TestMethod]
    public async Task MessageReceived_PlainSignedJwt_IsRejected()
    {
        using var provider = BuildProvider();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Config["Jwt:SigningKey"]!));
        var plain = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "test-issuer", "test-audience", [new Claim(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));

        var ctx = await RunMessageReceivedAsync(provider, "/api/x", bearer: plain);

        Assert.IsNotNull(ctx.Result);
        Assert.IsNotNull(ctx.Result.Failure);
    }

    [TestMethod]
    public async Task MessageReceived_QueryTokenAcceptedOnlyOnHubPath()
    {
        using var provider = BuildProvider();
        var token = IssueToken(provider);

        var hub = await RunMessageReceivedAsync(provider, "/hubs/orders", queryToken: token);
        var other = await RunMessageReceivedAsync(provider, "/api/orders", queryToken: token);

        Assert.AreEqual(token, hub.Token);
        Assert.IsNull(other.Token);
    }

    [TestMethod]
    public void JwtOptions_MissingEncryptionKey_FailsValidation()
    {
        var config = new Dictionary<string, string?>(Config) { ["Jwt:EncryptionKey"] = "CONFIGURE_VIA_USER_SECRETS_BASE64_32_BYTES" };
        using var provider = BuildProvider(config);

        Assert.ThrowsExactly<OptionsValidationException>(() => _ = provider.GetRequiredService<IOptions<JwtOptions>>().Value);
    }
}
