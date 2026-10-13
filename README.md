# auto-commit

A local Windows service that commits a timestamp to a readme every X hours,
so your GitHub contribution graph stays green.

How it works: every cycle pulls with rebase, appends a stamp line to the readme,
commits, and pushes. The service also runs once at startup, so a fresh install
proves itself immediately.

## First-time setup (do this once)

1. Create the GitHub repo that will collect the green squares (this repo itself works fine).
2. Clone it and push once **manually** from the account the service will run as.
   That manual push is what caches your git credentials in Windows Credential
   Manager — the service reuses them and never asks for a password.
3. Make sure `git` and `dotnet` are on your PATH (`dotnet --list-sdks` should show 8.x).

## Install as a Windows service (recommended)

Run PowerShell **as Administrator** from this folder:

```powershell
.\install-service.ps1 -IntervalHours 6 -GitEmail "you@example.com"
```

Omit `-GitEmail` if the repo already has `git config user.email` set.
Point at a different repo with `-RepoPath "C:\path\to\greens"`.

Useful commands:

```powershell
Get-Service AutoCommitService        # status
Restart-Service AutoCommitService    # after editing config
& "C:\Services\AutoCommitService\AutoCommitService.exe" --once      # manual run
& "C:\Services\AutoCommitService\AutoCommitService.exe" --dry-run   # stamps nothing
.\uninstall-service.ps1
```

Logs live in Event Viewer under Windows Logs > Application (source `AutoCommitService`).
Config lives at `C:\Services\AutoCommitService\appsettings.json` — restart the
service after editing it.

## Lighter alternative: scheduled task (no admin)

If you'd rather not run a service, this registers a Task Scheduler entry that
fires every X hours while you're logged in:

```powershell
.\install-task.ps1 -IntervalHours 6 -GitEmail "you@example.com"
.\install-task.ps1 -Uninstall   # to remove it again
```

## Config reference (`AutoCommit` section)

| Key | Default | Meaning |
| --- | --- | --- |
| `RepoPath` | (set by installer) | Local git clone receiving the commits |
| `IntervalHours` | `6` | Hours between commits (restart to apply) |
| `ReadmePath` | `README.md` | File to stamp, relative to repo root |
| `Branch` | current branch | Override when you must pin a branch |
| `CommitMessage` | `chore: automated activity commit ({timestamp})` | `{timestamp}` is replaced |
| `GitName` / `GitEmail` | existing git config | Applied only when git identity is missing |
| `PullBeforeCommit` / `PushAfterCommit` | `true` | Toggle the pull/push steps |
| `GitPath` | `git` | Full path if git isn't on the service PATH |

Stamps land between the markers at the bottom of this file (last 100 kept).
Without markers, the line is simply appended.

## No green squares? Check these

- **Email mismatch** is the #1 cause: the commit author email must match an
  email on your GitHub account exactly. Check with `git log -1 --format:%ae`
  after a run.
- Commits must land on the repo's **default branch** (or `gh-pages`).
- A repo with no remote, or pushes failing auth, silently piles up local commits —
  check the Application event log after install.
- For private repos, enable "Private contributions" in your GitHub profile settings.

Fair warning: a filler bot is visible to anyone who opens the commit history,
and some employers/recruiters discount obviously automated activity. Tinker away,
just know what the graph is (and isn't) saying.

## Develop

```powershell
dotnet build AutoCommit.sln
dotnet test AutoCommit.sln
```

## Activity log

<!-- AUTO-COMMIT-LOG:START -->
<!-- AUTO-COMMIT-LOG:END -->
