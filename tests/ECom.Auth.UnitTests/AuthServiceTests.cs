namespace ECom.Auth.UnitTests;

using ECom.Auth.Api.DTOs;
using ECom.Auth.Api.Entities;
using ECom.Auth.Api.Repositories;
using ECom.Auth.Api.Services;
using ECom.Shared.Exceptions;
using Microsoft.AspNetCore.Identity;
using Moq;

[TestClass]
public sealed class AuthServiceTests
{
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Tokens { get; } = new();

        public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
        {
            Tokens.Add(token);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task<bool> TryRevokeAsync(Guid tokenId, DateTimeOffset now, Guid? replacedByTokenId, CancellationToken cancellationToken = default)
        {
            var token = Tokens.First(t => t.Id == tokenId);
            if (token.IsRevoked)
                return Task.FromResult(false);
            token.Revoke(now, replacedByTokenId);
            return Task.FromResult(true);
        }

        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            foreach (var t in Tokens.Where(t => t.FamilyId == familyId))
                t.Revoke(now);
            return Task.CompletedTask;
        }
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<ITokenService> CreateTokenServiceMock()
    {
        var mock = new Mock<ITokenService>();
        mock.Setup(t => t.GenerateToken(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new TokenResult("test-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1)));
        mock.Setup(t => t.GenerateRefreshToken())
            .Returns(() =>
            {
                var raw = Guid.NewGuid().ToString("N");
                return new RefreshTokenResult(raw, "hash-" + raw, DateTimeOffset.UtcNow.AddDays(7));
            });
        mock.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns<string>(r => "hash-" + r);
        return mock;
    }

    private static AuthService CreateAuthService(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<ITokenService>? tokenService = null,
        IRefreshTokenRepository? refreshTokens = null,
        TimeProvider? timeProvider = null) =>
        new(
            userManager.Object,
            (tokenService ?? CreateTokenServiceMock()).Object,
            refreshTokens ?? new FakeRefreshTokenRepository(),
            timeProvider ?? TimeProvider.System);

    private static Mock<UserManager<ApplicationUser>> UserManagerFor(ApplicationUser user)
    {
        var um = CreateUserManagerMock();
        um.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        um.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);
        um.Setup(u => u.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        return um;
    }

    private static ApplicationUser NewUser(UserRole role = UserRole.Customer) =>
        new("user@example.com", "Test User", role) { Id = Guid.NewGuid() };

    private static LoginRequest LoginReq => new() { Email = "user@example.com", Password = "Password1" };

    // --- Registration ---

    [TestMethod]
    public async Task Register_ValidRequest_CreatesCustomerUser()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var response = await CreateAuthService(umMock).RegisterAsync(new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Password1",
            Name = "New User",
        });

        Assert.AreEqual("Customer", response.Role);
    }

    [TestMethod]
    public async Task Register_ValidRequest_RoleIsAlwaysCustomer()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        ApplicationUser? capturedUser = null;
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, _) => capturedUser = u)
            .ReturnsAsync(IdentityResult.Success);

        await CreateAuthService(umMock).RegisterAsync(new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Password1",
            Name = "New User",
        });

        Assert.IsNotNull(capturedUser);
        Assert.AreEqual(UserRole.Customer, capturedUser!.Role);
    }

    [TestMethod]
    public async Task Register_EmailIsTrimmedBeforeLookup()
    {
        var umMock = CreateUserManagerMock();
        string? lookupEmail = null;
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .Callback<string>(e => lookupEmail = e)
            .ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        await CreateAuthService(umMock).RegisterAsync(new RegisterRequest
        {
            Email = "  trimmed@example.com  ",
            Password = "Password1",
            Name = "Test",
        });

        Assert.AreEqual("trimmed@example.com", lookupEmail);
    }

    [TestMethod]
    public async Task Register_DuplicateEmail_ThrowsConflictException()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser("existing@example.com", "Existing", UserRole.Customer));

        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            CreateAuthService(umMock).RegisterAsync(new RegisterRequest
            {
                Email = "existing@example.com",
                Password = "Password1",
                Name = "Test",
            }));
    }

    [TestMethod]
    public async Task Register_ResponseDoesNotContainPasswordHash()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var response = await CreateAuthService(umMock).RegisterAsync(new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Password1",
            Name = "New User",
        });

        var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.IsFalse(responseJson.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Register_IdentityFailure_ThrowsDomainException()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            CreateAuthService(umMock).RegisterAsync(new RegisterRequest
            {
                Email = "new@example.com",
                Password = "weak",
                Name = "Test",
            }));
    }

    // --- Login ---

    [TestMethod]
    public async Task Login_ValidCredentials_ReturnsTokenResponse()
    {
        var response = await CreateAuthService(UserManagerFor(NewUser())).LoginAsync(LoginReq);

        Assert.IsFalse(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.AreEqual("Bearer", response.TokenType);
        Assert.IsTrue(response.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public async Task Login_UnknownEmail_ThrowsInvalidCredentialsException()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            CreateAuthService(umMock).LoginAsync(new LoginRequest { Email = "unknown@example.com", Password = "Password1" }));
    }

    [TestMethod]
    public async Task Login_WrongPassword_ThrowsInvalidCredentialsException()
    {
        var user = NewUser();
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        umMock.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);

        await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            CreateAuthService(umMock).LoginAsync(new LoginRequest { Email = "user@example.com", Password = "WrongPassword1" }));
    }

    [TestMethod]
    public async Task Login_UnknownEmail_SameExceptionTypeAsWrongPassword()
    {
        var user = NewUser();
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync("unknown@example.com")).ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        umMock.Setup(u => u.CheckPasswordAsync(user, "WrongPassword1")).ReturnsAsync(false);

        var svc = CreateAuthService(umMock);
        var ex1 = await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "unknown@example.com", Password = "Password1" }));
        var ex2 = await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "WrongPassword1" }));

        Assert.AreEqual(ex1.Message, ex2.Message);
    }

    [TestMethod]
    public async Task Login_Response_DoesNotContainPasswordHash()
    {
        var response = await CreateAuthService(UserManagerFor(NewUser())).LoginAsync(LoginReq);

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.IsFalse(json.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Login_TokenGenerationReceivesCorrectRole()
    {
        var umMock = UserManagerFor(new ApplicationUser("admin@example.com", "Admin User", UserRole.Admin) { Id = Guid.NewGuid() });

        string? capturedRole = null;
        var tokenMock = CreateTokenServiceMock();
        tokenMock.Setup(t => t.GenerateToken(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Guid, string, string, string>((_, _, _, r) => capturedRole = r)
            .Returns(new TokenResult("token", "Bearer", DateTimeOffset.UtcNow.AddHours(1)));

        await CreateAuthService(umMock, tokenMock).LoginAsync(LoginReq);

        Assert.AreEqual("Admin", capturedRole);
    }

    [TestMethod]
    public async Task Login_ReturnsAndPersistsHashedRefreshToken()
    {
        var repo = new FakeRefreshTokenRepository();
        var response = await CreateAuthService(UserManagerFor(NewUser()), refreshTokens: repo).LoginAsync(LoginReq);

        Assert.IsFalse(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.IsTrue(response.RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow);

        var stored = repo.Tokens.Single();
        Assert.AreEqual("hash-" + response.RefreshToken, stored.TokenHash);
        Assert.AreNotEqual(response.RefreshToken, stored.TokenHash);
    }

    [TestMethod]
    public async Task Login_EachLoginStartsNewFamily()
    {
        var repo = new FakeRefreshTokenRepository();
        var svc = CreateAuthService(UserManagerFor(NewUser()), refreshTokens: repo);

        await svc.LoginAsync(LoginReq);
        await svc.LoginAsync(LoginReq);

        Assert.HasCount(2, repo.Tokens.Select(t => t.FamilyId).Distinct().ToList());
    }

    // --- Refresh ---

    [TestMethod]
    public async Task Refresh_ValidToken_RotatesWithinSameFamily()
    {
        var repo = new FakeRefreshTokenRepository();
        var svc = CreateAuthService(UserManagerFor(NewUser()), refreshTokens: repo);
        var login = await svc.LoginAsync(LoginReq);

        var refreshed = await svc.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.AreNotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.HasCount(2, repo.Tokens);
        var old = repo.Tokens[0];
        var replacement = repo.Tokens[1];
        Assert.IsTrue(old.IsRevoked);
        Assert.AreEqual(replacement.Id, old.ReplacedByTokenId);
        Assert.AreEqual(old.FamilyId, replacement.FamilyId);
        Assert.IsFalse(replacement.IsRevoked);
    }

    [TestMethod]
    public async Task Refresh_UnknownToken_ThrowsInvalidRefreshToken()
    {
        var svc = CreateAuthService(UserManagerFor(NewUser()));
        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = "does-not-exist" }));
    }

    [TestMethod]
    public async Task Refresh_ReusedToken_RevokesWholeFamily()
    {
        var repo = new FakeRefreshTokenRepository();
        var svc = CreateAuthService(UserManagerFor(NewUser()), refreshTokens: repo);
        var login = await svc.LoginAsync(LoginReq);
        var refreshed = await svc.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });

        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken }));
        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = refreshed.RefreshToken }));
    }

    [TestMethod]
    public async Task Refresh_ReuseInOneFamily_DoesNotAffectOtherFamilies()
    {
        var repo = new FakeRefreshTokenRepository();
        var svc = CreateAuthService(UserManagerFor(NewUser()), refreshTokens: repo);
        var deviceA = await svc.LoginAsync(LoginReq);
        var deviceB = await svc.LoginAsync(LoginReq);

        await svc.RefreshAsync(new RefreshRequest { RefreshToken = deviceA.RefreshToken });
        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = deviceA.RefreshToken }));

        var stillWorks = await svc.RefreshAsync(new RefreshRequest { RefreshToken = deviceB.RefreshToken });
        Assert.IsFalse(string.IsNullOrWhiteSpace(stillWorks.AccessToken));
    }

    [TestMethod]
    public async Task Refresh_ExpiredToken_ThrowsInvalidRefreshToken()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var svc = CreateAuthService(UserManagerFor(NewUser()), timeProvider: clock);
        var login = await svc.LoginAsync(LoginReq);

        clock.Now = login.RefreshTokenExpiresAtUtc.AddSeconds(1);

        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken }));
    }

    [TestMethod]
    public async Task Refresh_DeletedUser_ThrowsInvalidRefreshToken()
    {
        var user = NewUser();
        var umMock = UserManagerFor(user);
        var svc = CreateAuthService(umMock);
        var login = await svc.LoginAsync(LoginReq);

        umMock.Setup(u => u.FindByIdAsync(user.Id.ToString())).ReturnsAsync((ApplicationUser?)null);

        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken }));
    }

    [TestMethod]
    public async Task Refresh_ReReadsUser_SoCurrentRoleIsUsed()
    {
        var user = NewUser();
        var umMock = UserManagerFor(user);
        var svc = CreateAuthService(umMock);
        var login = await svc.LoginAsync(LoginReq);

        user.Role = UserRole.Admin;

        var refreshed = await svc.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });
        Assert.AreEqual("Admin", refreshed.User.Role);
    }

    [TestMethod]
    public async Task Refresh_LostConcurrentRace_RevokesFamily()
    {
        var user = NewUser();
        var umMock = UserManagerFor(user);
        var family = Guid.NewGuid();
        var stored = new RefreshToken(user.Id, "hash-raw", family, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        var repo = new Mock<IRefreshTokenRepository>();
        repo.Setup(r => r.GetByHashAsync("hash-raw", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        repo.Setup(r => r.TryRevokeAsync(stored.Id, It.IsAny<DateTimeOffset>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var svc = CreateAuthService(umMock, refreshTokens: repo.Object);

        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = "raw" }));
        repo.Verify(r => r.RevokeFamilyAsync(family, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Logout ---

    [TestMethod]
    public async Task Logout_RevokesFamily_SoRefreshFails()
    {
        var svc = CreateAuthService(UserManagerFor(NewUser()));
        var login = await svc.LoginAsync(LoginReq);

        await svc.LogoutAsync(new LogoutRequest { RefreshToken = login.RefreshToken });

        await Assert.ThrowsExactlyAsync<InvalidRefreshTokenException>(() =>
            svc.RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken }));
    }

    [TestMethod]
    public async Task Logout_UnknownToken_IsSilentNoOp()
    {
        await CreateAuthService(UserManagerFor(NewUser())).LogoutAsync(new LogoutRequest { RefreshToken = "unknown" });
    }

    [TestMethod]
    public async Task Logout_Twice_IsIdempotent()
    {
        var svc = CreateAuthService(UserManagerFor(NewUser()));
        var login = await svc.LoginAsync(LoginReq);

        await svc.LogoutAsync(new LogoutRequest { RefreshToken = login.RefreshToken });
        await svc.LogoutAsync(new LogoutRequest { RefreshToken = login.RefreshToken });
    }

    // --- GetCurrentUser ---

    [TestMethod]
    public async Task GetCurrentUser_KnownId_ReturnsCurrentUserResponse()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser("me@example.com", "Me User", UserRole.Customer);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByIdAsync(userId.ToString())).ReturnsAsync(user);

        var response = await CreateAuthService(umMock).GetCurrentUserAsync(userId);

        Assert.AreEqual(user.Email, response.Email);
        Assert.AreEqual(user.Name, response.Name);
        Assert.AreEqual("Customer", response.Role);
    }

    [TestMethod]
    public async Task GetCurrentUser_UnknownId_ThrowsNotFoundException()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            CreateAuthService(umMock).GetCurrentUserAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetCurrentUser_Response_DoesNotContainPasswordHash()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser("me@example.com", "Me User", UserRole.Customer);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByIdAsync(userId.ToString())).ReturnsAsync(user);

        var response = await CreateAuthService(umMock).GetCurrentUserAsync(userId);

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.IsFalse(json.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase));
    }
}
