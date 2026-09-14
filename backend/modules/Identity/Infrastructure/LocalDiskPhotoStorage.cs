using Microsoft.Extensions.Configuration;
using SeniorConnect.Domain;
using SeniorConnect.Modules.Identity.Application;

namespace SeniorConnect.Modules.Identity.Infrastructure;

/// <summary>
/// Pilot-phase profile photo storage: local disk under wwwroot, served via
/// static files. One photo per user (previous files for the user are deleted
/// before the new one is written).
/// </summary>
public sealed class LocalDiskPhotoStorage : IPhotoStorage
{
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedContentTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly string _baseDirectory;

    public LocalDiskPhotoStorage(IConfiguration configuration)
    {
        _baseDirectory = configuration["Storage:PhotoUploadPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads", "photos");

        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<Result<string>> SaveProfilePhotoAsync(
        Guid userId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedContentTypes.TryGetValue(contentType, out var extension))
        {
            return Error.Validation("Unsupported image type.");
        }

        if (content.CanSeek && content.Length > MaxPhotoSizeBytes)
        {
            return Error.Validation("Photo exceeds the maximum allowed size of 5 MB.");
        }

        foreach (var existingFile in Directory.GetFiles(_baseDirectory, $"{userId:N}.*"))
        {
            File.Delete(existingFile);
        }

        var fileName = $"{userId:N}{extension}";
        var filePath = Path.Combine(_baseDirectory, fileName);

        await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return Result<string>.Success($"/uploads/photos/{fileName}");
    }
}
