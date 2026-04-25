using System.Diagnostics;
using InventoryControl.Services.Interfaces;

namespace InventoryControl.Services;

/// <summary>
/// Creates a database backup on demand.
/// - PostgreSQL: runs pg_dump and streams the result as plain SQL.
/// - SQLite: copies the database file.
/// Used by BackupController to provide an instant download to Admin users.
/// </summary>
public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly string _connectionString;
    private readonly bool _isPostgres;

    public DatabaseBackupService(IConfiguration configuration, ILogger<DatabaseBackupService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        _isPostgres = _connectionString.StartsWith("Host=", StringComparison.OrdinalIgnoreCase)
                   || _connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                   || _connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<(Stream Stream, string FileName)> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DatabaseBackupService: starting {Provider} backup.", _isPostgres ? "PostgreSQL" : "SQLite");

        return _isPostgres
            ? await CreatePostgresBackupAsync(cancellationToken)
            : await CreateSqliteBackupAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // PostgreSQL — pg_dump piped to MemoryStream
    // -------------------------------------------------------------------------

    private async Task<(Stream, string)> CreatePostgresBackupAsync(CancellationToken cancellationToken)
    {
        // Parse key=value pairs from the ADO.NET connection string
        var parts = _connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

        var host     = parts.GetValueOrDefault("Host",     "localhost");
        var port     = parts.GetValueOrDefault("Port",     "5432");
        var database = parts.GetValueOrDefault("Database", "postgres");
        var username = parts.GetValueOrDefault("Username", "postgres");
        var fileName = $"backup-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.sql";

        var psi = new ProcessStartInfo("pg_dump")
        {
            Arguments = $"-h {host} -p {port} -U {username} -d {database} --no-password",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
        // Pass password via environment variable — never via command-line args
        psi.Environment["PGPASSWORD"] = parts.GetValueOrDefault("Password", "");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start pg_dump.");

        // Clear PGPASSWORD immediately after the process has started
        psi.Environment.Remove("PGPASSWORD");

        var ms     = new MemoryStream();
        var copyTask  = process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(copyTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask;
            _logger.LogError("pg_dump exited with code {Code}: {Stderr}", process.ExitCode, stderr);
            throw new InvalidOperationException(
                $"pg_dump failed (exit {process.ExitCode}): {stderr}");
        }

        ms.Position = 0;
        _logger.LogInformation(
            "DatabaseBackupService: PostgreSQL backup complete. Size={Bytes} bytes, FileName={FileName}.",
            ms.Length, fileName);

        return (ms, fileName);
    }

    // -------------------------------------------------------------------------
    // SQLite — copy the database file into a MemoryStream
    // -------------------------------------------------------------------------

    private Task<(Stream, string)> CreateSqliteBackupAsync(CancellationToken cancellationToken)
    {
        var parts = _connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

        var dbPath = parts.GetValueOrDefault("Data Source", "app.db");

        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"SQLite database file not found: {dbPath}");

        var ms = new MemoryStream();
        using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            fs.CopyTo(ms);

        ms.Position = 0;

        var fileName = $"backup-{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.db";

        _logger.LogInformation(
            "DatabaseBackupService: SQLite backup complete. Size={Bytes} bytes, FileName={FileName}.",
            ms.Length, fileName);

        return Task.FromResult<(Stream, string)>((ms, fileName));
    }
}
