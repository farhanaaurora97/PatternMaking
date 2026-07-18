namespace PatternPro.Desktop.Services;

internal static class DesktopFileSave
{
    public static async Task<string> SaveToDownloadsAsync(byte[] bytes, string fileName, CancellationToken cancellationToken = default)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(downloads);

        var safeName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = Path.Combine(downloads, safeName);
        if (File.Exists(path))
        {
            var stem = Path.GetFileNameWithoutExtension(safeName);
            var ext = Path.GetExtension(safeName);
            path = Path.Combine(downloads, $"{stem}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
        }

        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }
}
