<#
.SYNOPSIS
    Fires N sequential requests at the gateway and reports the distribution across instances.

.DESCRIPTION
    Individual failures are counted rather than fatal, so the script still produces a report
    while a destination is being evicted by health checks.

.EXAMPLE
    ./scripts/test-load-balancing.ps1 -Requests 50
#>
[CmdletBinding()]
param(
    [string] $Url      = 'http://localhost:8000/products',
    [int]    $Requests = 20
)

$hits = for ($i = 1; $i -le $Requests; $i++) {
    try {
        $response = Invoke-RestMethod -Uri $Url -TimeoutSec 10
        $id = $response.servedBy.id
    } catch {
        $id = 'ERROR'
    }

    Write-Host ("{0,4}  ->  {1}" -f $i, $id)
    $id
}

Write-Host ''
Write-Host "Distribution over $Requests requests:" -ForegroundColor Cyan
$hits |
    Group-Object |
    Sort-Object Name |
    Select-Object @{ N = 'Instance'; E = { $_.Name } },
                  @{ N = 'Hits';     E = { $_.Count } },
                  @{ N = 'Share';    E = { '{0:P1}' -f ($_.Count / $Requests) } } |
    Format-Table -AutoSize
