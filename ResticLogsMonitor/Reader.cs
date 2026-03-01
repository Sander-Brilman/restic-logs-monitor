using System.Globalization;
using System.Text.Json;

namespace ResticLogsMonitor;

public class Reader
{
    public static readonly string HostsConfigurationDirectory = Path.Combine(Directory.GetCurrentDirectory(), "backup_config", "host");
    public static readonly string TargetsConfigurationDirectory = Path.Combine(Directory.GetCurrentDirectory(), "backup_config", "target");
    public static readonly string SnapshotLogsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "backup_log");

    public async Task<BackupProfile[]> GetProfiles()
    {
        string[] hosts = Directory
                   .GetFiles(HostsConfigurationDirectory)
                   .Select(f => Path.GetFileName(f))
                   .ToArray();

        string[] services = Directory
                    .GetFiles(TargetsConfigurationDirectory)
                    .Select(f => Path.GetFileName(f))
                    .ToArray();


        List<BackupProfile> profiles = new(hosts.Length * services.Length);
        foreach (var host in hosts)
        {
            foreach (var service in services)
            {
                string[] paths = await File
                    .ReadLinesAsync(Path.Combine(TargetsConfigurationDirectory, service))
                    .Select(p => p.Trim())
                    .Where(p => string.IsNullOrEmpty(p) is false)
                    .ToArrayAsync();

                profiles.Add(new BackupProfile(host, service, paths));
            }
        }

        return [.. profiles];
    }

    public async Task<SnapshotLog[]?> GetSnapshotLogs(BackupProfile profile)
    {
        string path = Path.Combine(SnapshotLogsDirectory, profile.HostName, $"{profile.ServiceName}-snapshots.json");

        if (File.Exists(path) is false)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SnapshotLog[]>(await File.ReadAllTextAsync(path));
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}

public record BackupProfile(string HostName, string ServiceName, string[] Paths);
public record SnapshotLog(DateTime time, string id, string tree, string[] paths);
