namespace AutoCommitService;

public sealed class AutoCommitOptions
{
    public const string SectionName = "AutoCommit";

    /// <summary>Absolute path to the local git clone that receives the commits.</summary>
    public string RepoPath { get; set; } = "";

    /// <summary>Hours between commits. Changing this requires a service restart.</summary>
    public double IntervalHours { get; set; } = 6;

    /// <summary>File to stamp, relative to the repo root.</summary>
    public string ReadmePath { get; set; } = "README.md";

    /// <summary>
    /// Branch to push to. Null/empty means "whatever branch is checked out".
    /// Commits only count toward the GitHub graph on the default branch.
    /// </summary>
    public string? Branch { get; set; }

    /// <summary>Commit message template. {timestamp} is replaced with the UTC stamp.</summary>
    public string CommitMessage { get; set; } = "chore: automated activity commit ({timestamp})";

    /// <summary>Set as the repo-local git identity when it is not already configured.</summary>
    public string? GitName { get; set; }

    /// <summary>
    /// Set as the repo-local git identity when it is not already configured.
    /// Must match an email on your GitHub account or the graph stays empty.
    /// </summary>
    public string? GitEmail { get; set; }

    public bool PullBeforeCommit { get; set; } = true;
    public bool PushAfterCommit { get; set; } = true;

    /// <summary>git executable. Full path if git is not on the service account's PATH.</summary>
    public string GitPath { get; set; } = "git";
}
