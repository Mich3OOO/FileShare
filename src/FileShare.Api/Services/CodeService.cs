using FileShare.Api.Data;
using FileShare.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FileShare.Api.Services;

public class CodeService
{
    private readonly AppDbContext _db;

    public CodeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AccessCode> CreateAsync(bool isSingleUse)
    {
        string code;
        do
        {
            code = RandomTokens.Generate(10);
        } while (await _db.AccessCodes.AnyAsync(c => c.Code == code));

        var entity = new AccessCode
        {
            Id = Guid.NewGuid(),
            Code = code,
            IsSingleUse = isSingleUse,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.AccessCodes.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public Task<List<AccessCode>> GetActiveAsync()
    {
        return _db.AccessCodes
            .Where(c => c.UsedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _db.AccessCodes.FindAsync(id);
        if (entity is null)
        {
            return false;
        }

        _db.AccessCodes.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<AccessCode?> FindActiveAsync(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _db.AccessCodes.FirstOrDefaultAsync(c => c.Code == normalized && c.UsedAt == null);
    }

    public async Task<bool> TryConsumeAsync(Guid id)
    {
        return await _db.AccessCodes
            .Where(c => c.Id == id && c.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UsedAt, DateTimeOffset.UtcNow)) == 1;
    }

    public async Task ReleaseAsync(Guid id)
    {
        await _db.AccessCodes
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.UsedAt, (DateTimeOffset?)null));
    }
}