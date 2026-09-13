<#
.SYNOPSIS
    Stops every process started by start-demo.ps1.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root     = Split-Path -Parent $PSScriptRoot
$pidsFile = Join-Path $root '.run/pids.json'

if (-not (Test-Path $pidsFile)) {
    Write-Host 'Nothing to stop: .run/pids.json not found.' -ForegroundColor Yellow
    return
}

foreach ($entry in (Get-Content $pidsFile -Raw | ConvertFrom-Json)) {
    try {
        Stop-Process -Id $entry.Pid -Force -ErrorAction Stop
        Write-Host "  stopped $($entry.Name) (pid $($entry.Pid))" -ForegroundColor DarkGray
    } catch {
        Write-Host "  $($entry.Name) (pid $($entry.Pid)) was not running" -ForegroundColor DarkGray
    }
}

Remove-Item $pidsFile -Force
Write-Host 'Demo stopped.' -ForegroundColor Green
