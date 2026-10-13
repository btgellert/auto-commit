using AutoCommitService;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "AutoCommitService");
builder.Services.Configure<AutoCommitOptions>(builder.Configuration.GetSection(AutoCommitOptions.SectionName));
builder.Services.AddSingleton<CommitRunner>();

var once = HasFlag(args, "--once");
var dryRun = HasFlag(args, "--dry-run");

if (!once && !dryRun)
{
    builder.Services.AddHostedService<Worker>();
}

var host = builder.Build();

if (once || dryRun)
{
    var options = host.Services.GetRequiredService<IOptions<AutoCommitOptions>>().Value;
    ApplyOverrides(args, options);
    var runner = host.Services.GetRequiredService<CommitRunner>();
    return await runner.RunOnceAsync(options, dryRun, CancellationToken.None);
}

host.Run();
return 0;

static bool HasFlag(string[] args, string flag) =>
    args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

static string? FlagValue(string[] args, string flag)
{
    for (var i = 0; i + 1 < args.Length; i++)
    {
        if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}

static void ApplyOverrides(string[] args, AutoCommitOptions options)
{
    var repo = FlagValue(args, "--repo");
    if (!string.IsNullOrWhiteSpace(repo))
    {
        options.RepoPath = repo;
    }

    var hours = FlagValue(args, "--interval-hours");
    if (double.TryParse(hours, out var h) && h > 0)
    {
        options.IntervalHours = h;
    }
}
