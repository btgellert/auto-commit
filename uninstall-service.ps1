#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Stops and removes the AutoCommit Windows service.
#>
param(
  [string]$InstallDir = "C:\Services\AutoCommitService",
  [string]$ServiceName = "AutoCommitService"
)

$ErrorActionPreference = "Stop"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
  if ($existing.Status -eq "Running") { Stop-Service -Name $ServiceName -Force }
  sc.exe delete $ServiceName | Out-Null
  Write-Host "Service '$ServiceName' removed." -ForegroundColor Green
} else {
  Write-Host "Service '$ServiceName' is not installed."
}

if (Test-Path $InstallDir) {
  Remove-Item $InstallDir -Recurse -Force
  Write-Host "Removed $InstallDir"
}
