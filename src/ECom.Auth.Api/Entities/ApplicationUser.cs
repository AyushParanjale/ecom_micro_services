namespace ECom.Auth.Api.Entities;

using Microsoft.AspNetCore.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Customer;

    private ApplicationUser() { }

    public ApplicationUser(string email, string name, UserRole role = UserRole.Customer)
    {
        Email = email;
        UserName = email;
        Name = name;
        Role = role;
    }
}
