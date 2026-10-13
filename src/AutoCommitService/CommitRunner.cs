using System.Diagnostics;
using System.Text;

namespace AutoCommitService;

public sealed record GitResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// One commit cycle: pull, stamp the readme, commit, push.
/// </summary>
public sealed class CommitRunner
{
    public const string LogStartMarker = "<!-- AUTO-COMMIT-LOG:START -->";
    public const string LogEndMarker = "<!-- AUTO-COMMIT-LOG:END -->";
    public const int MaxLogLines = 100;

    private readonly ILogger<CommitRunner> _logger;

    public CommitRunner(ILogger<CommitRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>Runs one full cycle. Returns a process exit code (0 = ok).</summary>
    public async Task<int> RunOnceAsync(AutoCommitOptions options, bool dryRun, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.RepoPath) || !Directory.Exists(options.RepoPath))
        {
            _logger.LogError("RepoPath '{RepoPath}' does not exist. Set AutoCommit:RepoPath to your local git clone.", options.RepoPath);
            return 2;
        }

        if (!Directory.Exists(Path.Combine(options.RepoPath, ".git")))
        {
            _logger.LogError("'{RepoPath}' is not a git repository (no .git folder).", options.RepoPath);
            return 2;
        }

        var stamp = DateTimeOffset.UtcNow;
        var timestamp = stamp.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var branch = await ResolveBranchAsync(options, ct);
        if (branch is null)
        {
            return 2;
        }

        await EnsureIdentityAsync(options, ct);

        if (options.PullBeforeCommit && !dryRun)
        {
            var pull = await RunGitAsync(options, "pull --rebase --autostash", ct);
            if (!pull.Success)
            {
                // Common on a fresh clone with no upstream yet; push -u below fixes that.
                _logger.LogWarning("git pull failed, continuing anyway: {Error}", pull.StdErr.Trim());
            }
        }

        var readmeFullPath = Path.Combine(options.RepoPath, options.ReadmePath);
        var line = BuildLogLine(stamp);
        var existing = File.Exists(readmeFullPath) ? await File.ReadAllTextAsync(readmeFullPath, ct) : "";
        await File.WriteAllTextAsync(readmeFullPath, InsertLogLine(existing, line), ct);
        _logger.LogInformation("Stamped {File} with '{Line}'", options.ReadmePath, line);

        if (dryRun)
        {
            await File.WriteAllTextAsync(readmeFullPath, existing, ct); // leave the tree untouched
            _logger.LogInformation("Dry run: readme restored, nothing committed.");
            return 0;
        }

        var add = await RunGitAsync(options, $"add -- \"{options.ReadmePath}\"", ct);
        if (!add.Success)
        {
            _logger.LogError("git add failed: {Error}", add.StdErr.Trim());
            return 1;
        }

        var status = await RunGitAsync(options, "status --porcelain", ct);
        if (status.Success && string.IsNullOrWhiteSpace(status.StdOut))
        {
            _logger.LogInformation("Working tree clean, nothing to commit.");
            return 0;
        }

        var message = FormatCommitMessage(options.CommitMessage, timestamp);
        var commit = await RunGitAsync(options, $"commit -m \"{EscapeArg(message)}\"", ct);
        if (!commit.Success)
        {
            _logger.LogError("git commit failed: {Error}", commit.StdErr.Trim());
            return 1;
        }

        _logger.LogInformation("Committed on branch '{Branch}': {Message}", branch, message);

        if (options.PushAfterCommit)
        {
            var push = await RunGitAsync(options, "push", ct);
            if (!push.Success)
            {
                _logger.LogWarning("git push failed, retrying with upstream: {Error}", push.StdErr.Trim());
                push = await RunGitAsync(options, $"push -u origin \"{EscapeArg(branch)}\"", ct);
            }

            if (!push.Success)
            {
                _logger.LogError("git push failed: {Error}", push.StdErr.Trim());
                return 1;
            }

            _logger.LogInformation("Pushed to origin.");
        }

        return 0;
    }

    private async Task<string?> ResolveBranchAsync(AutoCommitOptions options, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.Branch))
        {
            return options.Branch;
        }

        var current = await RunGitAsync(options, "rev-parse --abbrev-ref HEAD", ct);
        if (!current.Success)
        {
            _logger.LogError("Could not determine current branch: {Error}", current.StdErr.Trim());
            return null;
        }

        return current.StdOut.Trim();
    }

    private async Task EnsureIdentityAsync(AutoCommitOptions options, CancellationToken ct)
    {
        var email = await RunGitAsync(options, "config user.email", ct);
        if (!string.IsNullOrWhiteSpace(email.StdOut))
        {
            return; // identity already configured
        }

        if (!string.IsNullOrWhiteSpace(options.GitName))
        {
            await RunGitAsync(options, $"config user.name \"{EscapeArg(options.GitName)}\"", ct);
        }

        if (!string.IsNullOrWhiteSpace(options.GitEmail))
        {
            await RunGitAsync(options, $"config user.email \"{EscapeArg(options.GitEmail)}\"", ct);
            _logger.LogInformation("Set repo-local git identity to {Email}", options.GitEmail);
        }
        else
        {
            _logger.LogWarning("No git identity configured and AutoCommit:GitEmail is empty. Commit may fail.");
        }
    }

    private async Task<GitResult> RunGitAsync(AutoCommitOptions options, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(options.GitPath, $"-C \"{options.RepoPath}\" {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // Never block waiting for credentials on a non-interactive desktop.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = Process.Start(psi);
        if (process is null)
        {
            return new GitResult(1, "", $"could not start {options.GitPath}");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return new GitResult(process.ExitCode, stdout, stderr);
    }

    private static string EscapeArg(string value) => value.Replace("\"", "'");

    public static string BuildLogLine(DateTimeOffset timestamp)
        => $"- {timestamp:yyyy-MM-dd HH:mm} UTC - automated activity commit";

    public static string FormatCommitMessage(string template, string timestamp)
        => template.Replace("{timestamp}", timestamp, StringComparison.Ordinal);

    /// <summary>
    /// Appends a stamp line. When the log markers exist, the line goes inside
    /// them and the section is trimmed to the newest <paramref name="maxLines"/> entries.
    /// </summary>
    public static string InsertLogLine(string content, string line, int maxLines = MaxLogLines)
    {
        var start = content.IndexOf(LogStartMarker, StringComparison.Ordinal);
        var end = content.IndexOf(LogEndMarker, StringComparison.Ordinal);

        if (start < 0 || end < 0 || end < start)
        {
            var sep = content.Length > 0 && !content.EndsWith('\n') ? "\n" : "";
            return $"{content}{sep}{line}\n";
        }

        var before = content[..(start + LogStartMarker.Length)];
        var section = content[(start + LogStartMarker.Length)..end];
        var after = content[end..];

        var lines = section.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Trim().Length > 0)
            .Append(line)
            .TakeLast(maxLines);

        return $"{before.TrimEnd()}\n{string.Join('\n', lines)}\n{after.TrimStart()}";
    }
}
