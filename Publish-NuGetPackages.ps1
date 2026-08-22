[CmdletBinding()]
param(
    [string] $Source = $(if ($env:NUGET_SOURCE) { $env:NUGET_SOURCE } else { 'https://api.nuget.org/v3/index.json' })
)

$ErrorActionPreference = 'Stop'
$packageOutput = Join-Path $PSScriptRoot 'artifacts\packages'
$apiKey = $env:NUGET_API_KEY

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw 'Set the NUGET_API_KEY environment variable before publishing packages.'
}

if (-not (Test-Path $packageOutput)) {
    throw "Package output was not found at '$packageOutput'. Run Build.ps1 first."
}

$packages = Get-ChildItem -Path $packageOutput -Filter '*.nupkg' -File |
Where-Object Name -NotLike '*.symbols.nupkg' |
Sort-Object Name

if ($packages.Count -eq 0) {
    throw "No NuGet packages were found at '$packageOutput'. Run Build.ps1 first."
}

foreach ($package in $packages) {
    Write-Host "Publishing $($package.Name) to $Source"
    dotnet nuget push $package.FullName --api-key $apiKey --source $Source --skip-duplicate --timeout 300
}