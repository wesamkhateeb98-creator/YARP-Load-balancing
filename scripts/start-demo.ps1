<#
.SYNOPSIS
    Builds the solution, then starts five Products instances (5000-5004) and the YARP gateway (8000).

.DESCRIPTION
    Each process is launched detached with its stdout/stderr redirected to .run/*.log.
    Process ids are recorded in .run/pids.json so that stop-demo.ps1 can shut everything down.

.EXAMPLE
    ./scripts/start-demo.ps1
    ./scripts/start-demo.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [int[]]  $Ports       = @(5000, 5001, 5002, 5003, 5004),
    [int]    $GatewayPort = 8000,
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

$root   = Split-Path -Parent $PSScriptRoot
$runDir = Join-Path $root '.run'
New-Item -ItemType Directory -Force -Path $runDir | Out-Null

if (-not $SkipBuild) {
    Write-Host 'Building solution...' -ForegroundColor Cyan
    dotnet build (Join-Path $root 'YarpLoadBalancing.slnx') -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

function Start-DemoProcess([string] $Name, [string[]] $Arguments) {
    $process = Start-Process -FilePath 'dotnet' `
        -ArgumentList $Arguments `
        -RedirectStandardOutput (Join-Path $runDir "$Name.out.log") `
        -RedirectStandardError  (Join-Path $runDir "$Name.err.log") `
        -WindowStyle Hidden -PassThru
    [pscustomobject]@{ Name = $Name; Pid = $process.Id }
}

$started = @()

foreach ($port in $Ports) {
    $name = "products-$port"
    $started += Start-DemoProcess $name @(
        'run', '--project', (Join-Path $root 'src/Products/Products.csproj'),
        '-c', 'Release', '--no-build', '--',
        '--urls', "http://localhost:$port",
        '--Instance:Name', $name
    )
    Write-Host "  started $name" -ForegroundColor DarkGray
}

$started += Start-DemoProcess 'gateway' @(
    'run', '--project', (Join-Path $root 'src/YarpGateway/YarpGateway.csproj'),
    '-c', 'Release', '--no-build', '--',
    '--urls', "http://localhost:$GatewayPort"
)
Write-Host "  started gateway" -ForegroundColor DarkGray

$started | ConvertTo-Json | Set-Content -Path (Join-Path $runDir 'pids.json') -Encoding utf8

Write-Host 'Waiting for the gateway to accept traffic...' -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds(60)
do {
    Start-Sleep -Milliseconds 500
    try {
        $probe = Invoke-WebRequest "http://localhost:$GatewayPort/products" -UseBasicParsing -TimeoutSec 3
    } catch {
        $probe = $null
    }
} while (-not $probe -and (Get-Date) -lt $deadline)

if (-not $probe) { throw "Gateway did not become ready. Inspect $runDir for logs." }

Write-Host ''
Write-Host "Demo is up. Gateway: http://localhost:$GatewayPort/products" -ForegroundColor Green
Write-Host "Cluster state:  http://localhost:$GatewayPort/gateway/clusters" -ForegroundColor Green
Write-Host "Stop with:      ./scripts/stop-demo.ps1" -ForegroundColor Green
