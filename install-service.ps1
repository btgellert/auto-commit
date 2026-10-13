#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Builds, installs and starts the AutoCommit Windows service.

.EXAMPLE
  .\install-service.ps1 -IntervalHours 6 -GitEmail "you@example.com"

.EXAMPLE
  .\install-service.ps1 -RepoPath "C:\Users\me\source\greens" -IntervalHours 12
#>
param(
  [string]$RepoPath = "",
  [double]$IntervalHours = 6,
  [string]$GitName = "",
  [string]$GitEmail = "",
  [string]$InstallDir = "C:\Services\AutoCommitService",
  [string]$ServiceName = "AutoCommitService"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoPath)) {
  $RepoPath = $PSScriptRoot # commit to this repo itself by default
}
if (-not (Test-Path (Join-Path $RepoPath ".git"))) {
  throw "RepoPath '$RepoPath' is not a git repository. Clone one first, then re-run with -RepoPath."
}
if ($IntervalHours -le 0) { throw "IntervalHours must be positive." }
foreach ($cmd in @("dotnet", "git", "sc.exe")) {
  if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { throw "Required command not found on PATH: $cmd" }
}

Write-Host "Publishing service to $InstallDir ..."
dotnet publish "$PSScriptRoot\src\AutoCommitService" -c Release -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$configPath = Join-Path $InstallDir "appsettings.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json
$config.AutoCommit.RepoPath = $RepoPath
$config.AutoCommit.IntervalHours = $IntervalHours
if (-not [string]::IsNullOrWhiteSpace($GitName)) { $config.AutoCommit.GitName = $GitName }
if (-not [string]::IsNullOrWhiteSpace($GitEmail)) { $config.AutoCommit.GitEmail = $GitEmail }
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
Write-Host "Config: repo='$RepoPath' every=${IntervalHours}h"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
  Write-Host "Removing existing '$ServiceName' service ..."
  if ($existing.Status -eq "Running") { Stop-Service -Name $ServiceName -Force }
  sc.exe delete $ServiceName | Out-Null
  Start-Sleep -Seconds 2
}

# The service must run as YOU, not SYSTEM, so it can use your cached git credentials.
$defaultUser = "$env:USERDOMAIN\$env:USERNAME"
$cred = Get-Credential -UserName $defaultUser -Message "Windows account the service runs as (must have your git credentials)"
$plainPassword = $cred.GetNetworkCredential().Password

$exe = Join-Path $InstallDir "AutoCommitService.exe"
sc.exe create $ServiceName binPath= "`"$exe`"" start= auto obj= $cred.UserName password= $plainPassword DisplayName= "AutoCommit Service" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed." }
sc.exe description $ServiceName "Commits a timestamp to a readme every $IntervalHours hour(s)." | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
sc.exe start $ServiceName | Out-Null

Write-Host ""
Write-Host "Installed and started '$ServiceName'." -ForegroundColor Green
Write-Host "  It commits once right away, then every $IntervalHours hour(s)."
Write-Host "  Logs: Event Viewer > Windows Logs > Application, source '$ServiceName'."
Write-Host "  Config: $configPath (restart the service after editing)"
Write-Host "  Quick test anytime: & '$exe' --once"
