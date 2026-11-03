using FileShare.Api.Data;
using FileShare.Api.Domain;
using FileShare.Api.Services;

namespace FileShare.Api.Endpoints;

public record FileDto(Guid Id, string Name, long Size, string? Code, string? CodeNote, string Url, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
public record CodeDto(Guid Id, string Code, bool IsSingleUse, string? Note, DateTimeOffset CreatedAt);
public record CreateCodeRequest(bool IsSingleUse, string? Note);
public record UpdateCodeRequest(string? Note);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public static class EndpointHelpers
{
    public static IResult Error(string message, int statusCode = 400)
        => Results.Json(new { error = message }, statusCode: statusCode);

    public static string BuildDownloadUrl(HttpContext context, string token)
        => $"{context.Request.Scheme}://{context.Request.Host}/d/{token}";

    public static FileDto ToDto(HttpContext context, FileItem item)
        => new(item.Id, item.OriginalName, item.Size, item.AccessCode?.Code, item.AccessCode?.Note,
            BuildDownloadUrl(context, item.Token), item.CreatedAt, item.ExpiresAt);

    public static int? ParseDurationDays(string raw)
        => int.TryParse(raw, out var days) && days is 1 or 3 or 7 ? days : null;

    public static async Task<FileItem?> SaveFileAsync(
        HttpContext context,
        AppDbContext db,
        IFileStorage storage,
        int durationDays,
        Guid? accessCodeId)
    {
        var stored = await storage.SaveUploadAsync(context.Request, context.RequestAborted);
        if (stored is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var item = new FileItem
        {
            Id = Guid.NewGuid(),
            OriginalName = stored.FileName,
            Size = stored.Size,
            StoragePath = stored.Path,
            Token = RandomTokens.Generate(32),
            DurationDays = durationDays,
            CreatedAt = now,
            ExpiresAt = now.AddDays(durationDays),
            AccessCodeId = accessCodeId,
        };

        db.Files.Add(item);
        await db.SaveChangesAsync();
        return item;
    }
}