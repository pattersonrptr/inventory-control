namespace InventoryControl.Infrastructure.Backup;

public interface IDatabaseBackupService
{
    /// <summary>
    /// Creates a backup of the database and returns a readable stream plus a suggested file name.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    Task<(Stream Stream, string FileName)> CreateBackupAsync(CancellationToken cancellationToken = default);
}
