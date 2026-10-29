namespace ECom.Auth.UnitTests;

using ECom.Auth.Api.DTOs;
using ECom.Auth.Api.Entities;
using ECom.Auth.Api.Services;
using ECom.Shared.Exceptions;
using Microsoft.AspNetCore.Identity;
using Moq;

[TestClass]
public sealed class AuthServiceTests
{
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
        return mock;
    }

    // --- Registration ---

    [TestMethod]
    public async Task Register_ValidRequest_CreatesCustomerUser()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        var response = await svc.RegisterAsync(new RegisterRequest
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
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        ApplicationUser? capturedUser = null;
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, _) => capturedUser = u)
            .ReturnsAsync(IdentityResult.Success);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        await svc.RegisterAsync(new RegisterRequest
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
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        await svc.RegisterAsync(new RegisterRequest
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

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            svc.RegisterAsync(new RegisterRequest
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
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        var response = await svc.RegisterAsync(new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Password1",
            Name = "New User",
        });

        var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.IsFalse(responseJson.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(responseJson.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Register_IdentityFailure_ThrowsDomainException()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak." }));

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        await Assert.ThrowsExactlyAsync<DomainException>(() =>
            svc.RegisterAsync(new RegisterRequest
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
        var user = new ApplicationUser("user@example.com", "Test User", UserRole.Customer);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        umMock.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        var response = await svc.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "Password1" });

        Assert.IsFalse(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.AreEqual("Bearer", response.TokenType);
        Assert.IsTrue(response.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [TestMethod]
    public async Task Login_UnknownEmail_ThrowsInvalidCredentialsException()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "unknown@example.com", Password = "Password1" }));
    }

    [TestMethod]
    public async Task Login_WrongPassword_ThrowsInvalidCredentialsException()
    {
        var user = new ApplicationUser("user@example.com", "Test User", UserRole.Customer);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        umMock.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "WrongPassword1" }));
    }

    [TestMethod]
    public async Task Login_UnknownEmail_SameExceptionTypeAsWrongPassword()
    {
        var user = new ApplicationUser("user@example.com", "Test User", UserRole.Customer);
        var umMock = CreateUserManagerMock();

        umMock.Setup(u => u.FindByEmailAsync("unknown@example.com")).ReturnsAsync((ApplicationUser?)null);
        umMock.Setup(u => u.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        umMock.Setup(u => u.CheckPasswordAsync(user, "WrongPassword1")).ReturnsAsync(false);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);

        var ex1 = await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "unknown@example.com", Password = "Password1" }));
        var ex2 = await Assert.ThrowsExactlyAsync<InvalidCredentialsException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "WrongPassword1" }));

        Assert.AreEqual(ex1.Message, ex2.Message);
    }

    [TestMethod]
    public async Task Login_Response_DoesNotContainPasswordHash()
    {
        var user = new ApplicationUser("user@example.com", "Test User", UserRole.Customer);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        umMock.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        var response = await svc.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "Password1" });

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.IsFalse(json.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Login_TokenGenerationReceivesCorrectRole()
    {
        var user = new ApplicationUser("admin@example.com", "Admin User", UserRole.Admin);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        umMock.Setup(u => u.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(true);

        string? capturedRole = null;
        var tokenMock = new Mock<ITokenService>();
        tokenMock.Setup(t => t.GenerateToken(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Guid, string, string, string>((_, _, _, r) => capturedRole = r)
            .Returns(new TokenResult("token", "Bearer", DateTimeOffset.UtcNow.AddHours(1)));

        var svc = new AuthService(umMock.Object, tokenMock.Object);
        await svc.LoginAsync(new LoginRequest { Email = "admin@example.com", Password = "Password1" });

        Assert.AreEqual("Admin", capturedRole);
    }

    // --- GetCurrentUser ---

    [TestMethod]
    public async Task GetCurrentUser_KnownId_ReturnsCurrentUserResponse()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser("me@example.com", "Me User", UserRole.Customer);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByIdAsync(userId.ToString())).ReturnsAsync(user);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        var response = await svc.GetCurrentUserAsync(userId);

        Assert.AreEqual(user.Email, response.Email);
        Assert.AreEqual(user.Name, response.Name);
        Assert.AreEqual("Customer", response.Role);
    }

    [TestMethod]
    public async Task GetCurrentUser_UnknownId_ThrowsNotFoundException()
    {
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            svc.GetCurrentUserAsync(Guid.NewGuid()));
    }

    [TestMethod]
    public async Task GetCurrentUser_Response_DoesNotContainPasswordHash()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser("me@example.com", "Me User", UserRole.Customer);
        var umMock = CreateUserManagerMock();
        umMock.Setup(u => u.FindByIdAsync(userId.ToString())).ReturnsAsync(user);

        var svc = new AuthService(umMock.Object, CreateTokenServiceMock().Object);
        var response = await svc.GetCurrentUserAsync(userId);

        var json = System.Text.Json.JsonSerializer.Serialize(response);
        Assert.IsFalse(json.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("SecurityStamp", StringComparison.OrdinalIgnoreCase));
    }
}
