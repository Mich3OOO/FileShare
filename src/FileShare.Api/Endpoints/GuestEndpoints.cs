using FileShare.Api.Data;
using FileShare.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FileShare.Api.Endpoints;

public static class GuestEndpoints
{
    public static IEndpointRouteBuilder MapGuestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/guest/upload", async (
            HttpContext context,
            AppDbContext db,
            CodeService codes,
            IFileStorage storage) =>
        {
            var codeValue = context.Request.Query["code"].ToString();
            if (string.IsNullOrWhiteSpace(codeValue))
            {
                return EndpointHelpers.Error("An access code is required.");
            }

            var durationDays = EndpointHelpers.ParseDurationDays(context.Request.Query["days"].ToString());
            if (durationDays is null)
            {
                return EndpointHelpers.Error("Duration must be 1, 3 or 7 days.");
            }

            var code = await codes.FindActiveAsync(codeValue);
            if (code is null)
            {
                return EndpointHelpers.Error("This access code is invalid or has already been used.", StatusCodes.Status403Forbidden);
            }

            if (code.IsSingleUse && !await codes.TryConsumeAsync(code.Id))
            {
                return EndpointHelpers.Error("This access code has already been used.", StatusCodes.Status409Conflict);
            }

            var item = await EndpointHelpers.SaveFileAsync(context, db, storage, durationDays.Value, code.Id);
            if (item is null)
            {
                if (code.IsSingleUse)
                {
                    await codes.ReleaseAsync(code.Id);
                }

                return EndpointHelpers.Error("No file was received.");
            }

            return Results.Ok(new
            {
                name = item.OriginalName,
                size = item.Size,
                url = EndpointHelpers.BuildDownloadUrl(context, item.Token),
                expiresAt = item.ExpiresAt,
            });
        })
        .RequireRateLimiting("uploads")
        .AllowAnonymous()
        .DisableAntiforgery();

        return app;
    }
}

public static class DownloadEndpoints
{
    public static IEndpointRouteBuilder MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/d/{token}", async (string token, AppDbContext db, HttpContext context, CancellationToken cancellationToken) =>
        {
            var item = await db.Files.FirstOrDefaultAsync(f => f.Token == token, cancellationToken);
            if (item is null)
            {
                return Results.Text("This link is not valid.", statusCode: StatusCodes.Status404NotFound);
            }

            if (item.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return Results.Text("This link has expired.", statusCode: StatusCodes.Status410Gone);
            }

            // Every download extends the link by the same duration chosen at upload time.
            item.ExpiresAt = item.ExpiresAt.AddDays(item.DurationDays);
            await db.SaveChangesAsync(cancellationToken);

            if (!File.Exists(item.StoragePath))
            {
                return Results.Text("The file is no longer available.", statusCode: StatusCodes.Status404NotFound);
            }

            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(item.OriginalName, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return Results.File(item.StoragePath, contentType, item.OriginalName, enableRangeProcessing: true);
        })
        .AllowAnonymous();

        return app;
    }
}