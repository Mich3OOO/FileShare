using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace FileShare.Api.Services;

public record StoredFile(string FileName, long Size, string Path);

public interface IFileStorage
{
    Task<StoredFile?> SaveUploadAsync(HttpRequest request, CancellationToken cancellationToken);
    void Delete(string path);
}

public class FileStorage : IFileStorage
{
    private readonly string _root;

    public FileStorage(string rootPath)
    {
        _root = rootPath;
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile?> SaveUploadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType) ||
            !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
        if (string.IsNullOrEmpty(boundary))
        {
            return null;
        }

        var reader = new MultipartReader(boundary, request.Body)
        {
            HeadersLengthLimit = 64 * 1024,
        };

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(cancellationToken)) is not null)
        {
            if (section.ContentDisposition is null ||
                !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) ||
                !disposition.FileName.HasValue)
            {
                continue;
            }

            var fileName = SanitizeFileName(disposition.FileName.Value ?? "file");
            var path = Path.Combine(_root, Guid.NewGuid().ToString("N"));
            long size;

            try
            {
                await using (var stream = new FileStream(
                    path, FileMode.CreateNew, FileAccess.Write, System.IO.FileShare.None,
                    bufferSize: 128 * 1024, useAsync: true))
                    {
                    await section.Body.CopyToAsync(stream, cancellationToken);
                    size = stream.Length;
                }
            }
            catch
            {
                TryDelete(path);
                throw;
            }

            return new StoredFile(fileName, size, path);
        }

        return null;
    }

    public void Delete(string path)
    {
        TryDelete(path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort: orphaned blobs are acceptable here.
        }
    }

    private static string SanitizeFileName(string name)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        fileName = fileName.Trim();
        if (fileName.Length == 0)
        {
            fileName = "file";
        }

        return fileName.Length > 255 ? fileName[..255] : fileName;
    }
}