using Microsoft.Data.SqlClient;

namespace DailyPilot.Services;

public class BackupOptions
{
    /// <summary>Filesystem directory where .bak files are written. Empty = backups disabled.</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>How many daily backup files to keep before pruning the oldest.</summary>
    public int RetentionDays { get; set; } = 14;
}

public interface IBackupService
{
    bool IsEnabled { get; }
    Task BackupAsync();
}

/// <summary>
/// PRD §9 Daily Backups. Runs a SQL Server BACKUP DATABASE to a configured folder
/// and prunes old files. No-ops (with a log) when no directory is configured.
/// </summary>
public class SqlBackupService : IBackupService
{
    private readonly string _connectionString;
    private readonly BackupOptions _options;
    private readonly ILogger<SqlBackupService> _logger;

    public SqlBackupService(IConfiguration config, Microsoft.Extensions.Options.IOptions<BackupOptions> options, ILogger<SqlBackupService> logger)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")!;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_options.Directory);

    public async Task BackupAsync()
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("Database backup skipped: no 'Backup:Directory' configured.");
            return;
        }

        var dbName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(dbName))
        {
            _logger.LogWarning("Database backup skipped: could not determine database name.");
            return;
        }

        Directory.CreateDirectory(_options.Directory);
        var fileName = $"{dbName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak";
        var fullPath = Path.Combine(_options.Directory, fileName);

        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;
            // Parameterise the path; the DB name is quoted via QUOTENAME to avoid injection.
            cmd.CommandText =
                "DECLARE @db sysname = DB_NAME(); " +
                "DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@db) + " +
                "N' TO DISK = @p WITH INIT, FORMAT, NAME = N''DayPilot daily backup'';'; " +
                "EXEC sp_executesql @sql, N'@p nvarchar(4000)', @p = @path;";
            cmd.Parameters.AddWithValue("@path", fullPath);
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogInformation("Database backup written to {Path}.", fullPath);
        Prune();
    }

    private void Prune()
    {
        try
        {
            var files = new DirectoryInfo(_options.Directory)
                .GetFiles("*.bak")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(Math.Max(1, _options.RetentionDays))
                .ToList();
            foreach (var f in files) f.Delete();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup pruning failed.");
        }
    }
}
