namespace DailyPilot.Services;

/// <summary>Stores task attachment files outside the web root, served via an authorised action.</summary>
public interface IFileStorageService
{
    long MaxFileSizeBytes { get; }
    bool IsAllowed(string fileName, string contentType);
    Task<string> SaveAsync(string userId, IFormFile file);
    Stream? OpenRead(string userId, string storedFileName);
    void Delete(string userId, string storedFileName);
}

public class FileStorageService : IFileStorageService
{
    private readonly string _root;

    public FileStorageService(IWebHostEnvironment env)
    {
        // Stored under the content root (NOT wwwroot) so files aren't publicly served.
        _root = Path.Combine(env.ContentRootPath, "Storage", "attachments");
        Directory.CreateDirectory(_root);
    }

    public long MaxFileSizeBytes => 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".txt", ".csv", ".md",
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
        ".zip"
    };

    public bool IsAllowed(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
    }

    public async Task<string> SaveAsync(string userId, IFormFile file)
    {
        var dir = Path.Combine(_root, userId);
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(file.FileName);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, storedName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return storedName;
    }

    public Stream? OpenRead(string userId, string storedFileName)
    {
        var path = ResolvePath(userId, storedFileName);
        return path is not null && File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string userId, string storedFileName)
    {
        var path = ResolvePath(userId, storedFileName);
        if (path is not null && File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Resolve and guard against path traversal — the file must stay within the user's folder.</summary>
    private string? ResolvePath(string userId, string storedFileName)
    {
        var dir = Path.GetFullPath(Path.Combine(_root, userId));
        var candidate = Path.GetFullPath(Path.Combine(dir, storedFileName));
        return candidate.StartsWith(dir, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
