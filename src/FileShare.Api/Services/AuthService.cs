using System.Security.Claims;
using FileShare.Api.Data;
using FileShare.Api.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace FileShare.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAdminAsync(string username, string password)
    {
        if (await _db.Users.AnyAsync())
        {
            return;
        }

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
        });
        await _db.SaveChangesAsync();
    }

    public async Task<User?> ValidateAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }

    public async Task SignInAsync(HttpContext context, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    public async Task SignOutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return (false, "The new password must be at least 8 characters long.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !PasswordHasher.Verify(currentPassword, user.PasswordHash))
        {
            return (false, "The current password is incorrect.");
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        await _db.SaveChangesAsync();
        return (true, null);
    }
}