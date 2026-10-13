using Microsoft.Extensions.Options;

namespace AutoCommitService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly CommitRunner _runner;
    private readonly AutoCommitOptions _options;

    public Worker(ILogger<Worker> logger, CommitRunner runner, IOptions<AutoCommitOptions> options)
    {
        _logger = logger;
        _runner = runner;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IntervalHours <= 0)
        {
            _logger.LogError("AutoCommit:IntervalHours must be positive, got {Hours}.", _options.IntervalHours);
            return;
        }

        // Run once at startup so a fresh install proves itself immediately,
        // then settle into the steady every-X-hours rhythm.
        _logger.LogInformation(
            "AutoCommit started: every {Hours}h, repo '{Repo}', file '{File}'.",
            _options.IntervalHours, _options.RepoPath, _options.ReadmePath);

        await _runner.RunOnceAsync(_options, dryRun: false, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.IntervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _runner.RunOnceAsync(_options, dryRun: false, stoppingToken);
        }
    }
}
