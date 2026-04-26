using System.Diagnostics;
using InventoryControl.Services.Interfaces;

namespace InventoryControl.Infrastructure.Backup;

/// <summary>
/// Uploads database backups to an offsite remote using the rclone CLI.
/// Requires the rclone binary to be installed (added to the Docker image) and
/// a valid rclone config file at the path specified by OffsiteBackup:RcloneConfigPath.
///
/// Configure via appsettings:
///   "OffsiteBackup": {
///     "RcloneConfigPath": "/config/rclone/rclone.conf",
///     "RemotePath": "gdrive:inventory-control-backups/manual"
///   }
///
/// PERMISSIONS: the rclone.conf file must be readable by the app process user.
/// On Docker, run: chmod o+r /path/to/rclone.conf
/// </summary>
public class OffsiteBackupService : IOffsiteBackupService
{
    private readonly string _configPath;
    private readonly ILogger<OffsiteBackupService> _logger;

    public string RemotePath { get; }

    public OffsiteBackupService(IConfiguration configuration, ILogger<OffsiteBackupService> logger)
    {
        _logger = logger;
        _configPath = configuration["OffsiteBackup:RcloneConfigPath"] ?? "/config/rclone/rclone.conf";
        RemotePath  = configuration["OffsiteBackup:RemotePath"] ?? "gdrive:inventory-control-backups/manual";
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        var remotes = await GetConfiguredRemotesAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(remotes);
    }

    public async Task<string?> GetConfiguredRemotesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogDebug("OffsiteBackupService: rclone config not found at {Path}.", _configPath);
            return null;
        }

        try
        {
            var psi = BuildProcess($"listremotes --config \"{_configPath}\"");
            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)
                ? output.Trim()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OffsiteBackupService: could not run rclone listremotes.");
            return null;
        }
    }

    public async Task<(bool Success, string Message)> UploadAsync(
        Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configPath))
            return (false, $"Arquivo de configuração rclone não encontrado em '{_configPath}'. Veja as instruções abaixo.");

        var tmpFile = Path.Combine(Path.GetTempPath(), $"ic-{fileName}");
        try
        {
            // Write backup to a temp file so rclone can read it
            await using (var fs = new FileStream(tmpFile, FileMode.Create, FileAccess.Write))
                await stream.CopyToAsync(fs, cancellationToken);

            var destination = $"{RemotePath.TrimEnd('/')}/{fileName}";

            _logger.LogInformation(
                "OffsiteBackupService: uploading {FileName} to {Destination} via rclone.",
                fileName, destination);

            var psi = BuildProcess($"copyto \"{tmpFile}\" \"{destination}\" --config \"{_configPath}\" --log-level WARNING");

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Falha ao iniciar o processo rclone.");

            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "OffsiteBackupService: rclone exited with code {Code}: {Stderr}",
                    process.ExitCode, stderr);
                return (false, $"rclone falhou (código {process.ExitCode}): {stderr.Trim()}");
            }

            _logger.LogInformation(
                "OffsiteBackupService: upload successful — {FileName} → {Destination}.",
                fileName, destination);

            return (true, $"Backup '{fileName}' enviado com sucesso para {destination}.");
        }
        finally
        {
            if (File.Exists(tmpFile))
                File.Delete(tmpFile);
        }
    }

    private static ProcessStartInfo BuildProcess(string arguments) =>
        new("rclone")
        {
            Arguments             = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };
}
