namespace InventoryControl.Services.Interfaces;

public interface IOffsiteBackupService
{
    string RemotePath { get; }

    /// <summary>
    /// Returns true if rclone binary is available and at least one remote is configured.
    /// </summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a string listing configured remotes (for display), or null if unavailable.
    /// </summary>
    Task<string?> GetConfiguredRemotesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads the given stream to the configured remote path using rclone.
    /// </summary>
    Task<(bool Success, string Message)> UploadAsync(
        Stream stream, string fileName, CancellationToken cancellationToken = default);
}
