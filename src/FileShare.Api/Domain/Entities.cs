namespace FileShare.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

public class AccessCode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsSingleUse { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

public class FileItem
{
    public Guid Id { get; set; }
    public string OriginalName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? AccessCodeId { get; set; }
    public AccessCode? AccessCode { get; set; }
}