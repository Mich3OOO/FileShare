using System.Text.Json;
using FileShare.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FileShare.Api.Endpoints;

public record LoginPayload(string? Username, string? Password);

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (HttpContext context, AuthService auth) =>
        {
            string username;
            string password;

            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                username = form["username"].ToString();
                password = form["password"].ToString();
            }
            else if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                LoginPayload? payload;
                try
                {
                    payload = await JsonSerializer.DeserializeAsync<LoginPayload>(
                        context.Request.Body, JsonOptions.Instance, context.RequestAborted);
                }
                catch (JsonException)
                {
                    return EndpointHelpers.Error("Invalid request body.");
                }

                username = payload?.Username ?? string.Empty;
                password = payload?.Password ?? string.Empty;
            }
            else
            {
                return EndpointHelpers.Error("Send credentials as form data or JSON.");
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return EndpointHelpers.Error("Username and password are required.");
            }

            var user = await auth.ValidateAsync(username, password);
            if (user is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400), context.RequestAborted);
                return EndpointHelpers.Error("Invalid username or password.", StatusCodes.Status401Unauthorized);
            }

            await auth.SignInAsync(context, user);
            return Results.Ok(new { username = user.Username });
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        app.MapPost("/api/auth/logout", async (HttpContext context, AuthService auth) =>
        {
            await auth.SignOutAsync(context);
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/auth/me", (HttpContext context) =>
            Results.Ok(new { username = context.User.Identity?.Name }))
        .RequireAuthorization();

        app.MapPost("/api/auth/password", async (HttpContext context, AuthService auth, ChangePasswordRequest request) =>
        {
            var username = context.User.Identity?.Name;
            if (username is null)
            {
                return EndpointHelpers.Error("Not authenticated.", StatusCodes.Status401Unauthorized);
            }

            var (ok, error) = await auth.ChangePasswordAsync(username, request.CurrentPassword, request.NewPassword);
            return ok ? Results.Ok(new { ok = true }) : EndpointHelpers.Error(error!);
        })
        .RequireRateLimiting("auth")
        .RequireAuthorization();

        return app;
    }

    public static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.LoginPath = "/login.html";
        options.Cookie.Name = "fileshare_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
        };
    }
}