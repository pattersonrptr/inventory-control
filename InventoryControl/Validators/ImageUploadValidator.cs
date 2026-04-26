using Microsoft.AspNetCore.Http;

namespace InventoryControl.Validators;

public static class ImageUploadValidator
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    public static List<string> Validate(IFormFile[] files)
    {
        var errors = new List<string>();

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                errors.Add($"'{file.FileName}': invalid file type. Allowed: {string.Join(", ", AllowedExtensions)}.");
                continue;
            }

            if (file.Length > MaxFileSizeBytes)
                errors.Add($"'{file.FileName}': exceeds the 10 MB size limit.");
        }

        return errors;
    }
}
