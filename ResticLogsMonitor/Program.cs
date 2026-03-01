using ResticLogsMonitor;

Directory.CreateDirectory(Reader.HostsConfigurationDirectory);
Directory.CreateDirectory(Reader.SnapshotLogsDirectory);
Directory.CreateDirectory(Reader.TargetsConfigurationDirectory);

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
    Reader reader = new Reader();

    BackupProfile[] profiles = await reader.GetProfiles();
    List<BackupProfile> backupsWithIssues = [];

    foreach (var profile in profiles)
    {
        SnapshotLog[]? logs = await reader.GetSnapshotLogs(profile);

        if (logs is null || logs.Length == 0)
        {
            backupsWithIssues.Add(profile);
            continue;
        }

        SnapshotLog lastBackup = logs.Last();
        TimeSpan timeSinceLastBackup = DateTime.Now - lastBackup.time;

        if (timeSinceLastBackup.TotalDays > 1)
        {
            backupsWithIssues.Add(profile);
            continue;
        }

        bool pathsMatch = lastBackup.paths.Order().SequenceEqual(profile.Paths.Order());
        if (pathsMatch is false)
        {
            backupsWithIssues.Add(profile);
            continue;
        }
    }

    if (backupsWithIssues.Count == 0)
    {
        return Results.Ok(Array.Empty<BackupProfile>());
    }

    return Results.BadRequest(backupsWithIssues);

})
.WithName("health check");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
