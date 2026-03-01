using System.Reflection.Metadata;
using ResticLogsMonitor;

string[] requiredDirectories = [
    Reader.HostsConfigurationDirectory,
    Reader.SnapshotLogsDirectory,
    Reader.TargetsConfigurationDirectory
];

foreach (var dir in requiredDirectories)
{
    if (Directory.Exists(dir) is false)
    {
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (System.Exception)
        {
            System.Console.WriteLine($"ERROR: directory {dir} does not exist and couldnt be created");
            return 1;
        }
    }
}



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.UseHttpsRedirection();

app.MapGet("/health", async () =>
{
    Reader reader = new();

    BackupProfile[] profiles = await reader.GetProfiles();
    List<BackupReport> reports = [];

    foreach (var profile in profiles)
    {
        SnapshotLog[]? logs = await reader.GetSnapshotLogs(profile);

        if (logs is null || logs.Length == 0)
        {
            reports.Add(new BackupReport(profile.HostName, profile.ServiceName, "No logs found", null));
            continue;
        }

        SnapshotLog lastBackup = logs.Last();
        TimeSpan timeSinceLastBackup = DateTime.Now - lastBackup.time;

        if (timeSinceLastBackup.TotalDays > 1)
        {
            reports.Add(new BackupReport(profile.HostName, profile.ServiceName, $"Backup is more then 1 day old {timeSinceLastBackup}", lastBackup.time));
            continue;
        }

        string[] logPaths = [.. lastBackup.paths.Select(Path.TrimEndingDirectorySeparator).Order()];
        string[] profilePaths = [..profile.Paths.Select(Path.TrimEndingDirectorySeparator).Order()];
        if (logPaths.Length < profile.Paths.Length)
        {
            reports.Add(new BackupReport(profile.HostName, profile.ServiceName, "Paths missing from log", lastBackup.time));
            continue;
        }

        for (int i = 0; i < logPaths.Length; i++)
        {
            string logPath = logPaths[i];
            string profilePath = profilePaths[i];
            if (logPath != profilePath)
            {
                reports.Add(new BackupReport(profile.HostName, profile.ServiceName, "Paths do not match", lastBackup.time));
                break;
            }
        }


    }

    if (reports.Count == 0)
    {
        return Results.Ok(Array.Empty<BackupProfile>());
    }

    return Results.BadRequest(reports);
})
.WithName("health check");

app.Run();
return 0;

public record BackupReport(string BackupHost, string ServiceName, string reason, DateTime? LastBackupDate);