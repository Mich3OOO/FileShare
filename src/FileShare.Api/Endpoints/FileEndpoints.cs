using FileShare.Api.Data;
using FileShare.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FileShare.Api.Endpoints;

public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files", async (HttpContext context, AppDbContext db) =>
        {
            var now = DateTimeOffset.UtcNow;
            var files = await db.Files
                .Where(f => f.ExpiresAt > now)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return Results.Ok(files.Select(f => EndpointHelpers.ToDto(context, f)));
        })
        .RequireAuthorization();

        app.MapPost("/api/files/upload", async (HttpContext context, AppDbContext db, IFileStorage storage) =>
        {
            var durationDays = EndpointHelpers.ParseDurationDays(context.Request.Query["days"].ToString());
            if (durationDays is null)
            {
                return EndpointHelpers.Error("Duration must be 1, 3 or 7 days.");
            }

            var item = await EndpointHelpers.SaveFileAsync(context, db, storage, durationDays.Value, accessCodeId: null);
            return item is null
                ? EndpointHelpers.Error("No file was received.")
                : Results.Ok(EndpointHelpers.ToDto(context, item));
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        app.MapDelete("/api/files/{id:guid}", async (Guid id, AppDbContext db, IFileStorage storage) =>
        {
            var item = await db.Files.FindAsync(id);
            if (item is null)
            {
                return EndpointHelpers.Error("File not found.", StatusCodes.Status404NotFound);
            }

            storage.Delete(item.StoragePath);
            db.Files.Remove(item);
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        })
        .RequireAuthorization();

        return app;
    }
}