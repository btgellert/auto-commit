<#
.SYNOPSIS
  Lightweight alternative to the Windows service: runs one commit every X hours
  via Task Scheduler. No admin rights needed, but the PC must be on and you
  must be logged in when it fires.

.EXAMPLE
  .\install-task.ps1 -IntervalHours 6 -GitEmail "you@example.com"

.EXAMPLE
  .\install-task.ps1 -Uninstall
#>
param(
  [string]$RepoPath = "",
  [double]$IntervalHours = 6,
  [string]$GitName = "",
  [string]$GitEmail = "",
  [string]$InstallDir = "",
  [string]$TaskName = "AutoCommit",
  [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

if ($Uninstall) {
  Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
  Write-Host "Scheduled task '$TaskName' removed." -ForegroundColor Green
  return
}

if ([string]::IsNullOrWhiteSpace($RepoPath)) {
  $RepoPath = $PSScriptRoot # commit to this repo itself by default
}
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
  $InstallDir = Join-Path $env:LOCALAPPDATA "AutoCommitService"
}
if (-not (Test-Path (Join-Path $RepoPath ".git"))) {
  throw "RepoPath '$RepoPath' is not a git repository. Clone one first, then re-run with -RepoPath."
}
if ($IntervalHours -le 0) { throw "IntervalHours must be positive." }
foreach ($cmd in @("dotnet", "git")) {
  if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { throw "Required command not found on PATH: $cmd" }
}

Write-Host "Publishing to $InstallDir ..."
dotnet publish "$PSScriptRoot\src\AutoCommitService" -c Release -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$configPath = Join-Path $InstallDir "appsettings.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$config.AutoCommit.RepoPath = $RepoPath
$config.AutoCommit.IntervalHours = $IntervalHours
if (-not [string]::IsNullOrWhiteSpace($GitName)) { $config.AutoCommit.GitName = $GitName }
if (-not [string]::IsNullOrWhiteSpace($GitEmail)) { $config.AutoCommit.GitEmail = $GitEmail }
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8

$exe = Join-Path $InstallDir "AutoCommitService.exe"
$action = New-ScheduledTaskAction -Execute $exe -Argument "--once" -WorkingDirectory $InstallDir
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
  -RepetitionInterval (New-TimeSpan -Hours $IntervalHours)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable `
  -RunOnlyIfNetworkAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
  -Settings $settings -Description "Commits a timestamp to a readme every $IntervalHours hour(s)." -Force | Out-Null

Write-Host ""
Write-Host "Task '$TaskName' registered: every $IntervalHours hour(s), starting in ~1 minute." -ForegroundColor Green
Write-Host "  First run is imminent, so check your repo shortly to confirm it works."
Write-Host "  Manage it in Task Scheduler (taskschd.msc) or re-run with -Uninstall."
Write-Host "  Quick test anytime: & '$exe' --once"
