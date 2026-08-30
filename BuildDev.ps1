param(
    [bool]$BuildAngularServer = $true,
    [bool]$BuildAngularSelfHost = $true,
    [bool]$BuildAngularSelfHost48 = $true
)

$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot

if ($BuildAngularServer) {
    npm --prefix diag-web run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build failed with exit code $LASTEXITCODE."
    }
}
if ($BuildAngularSelfHost) {

    npm --prefix diag-web run build:self-host
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build:self-host failed with exit code $LASTEXITCODE."
    }
    Remove-Item -Recurse -Force DiagnosticExplorer.Hosting\wwwroot\core\*
    Copy-Item -Recurse -Force diag-web\dist\self-host\browser\* DiagnosticExplorer.Hosting\wwwroot\core
}

if ($BuildAngularSelfHost48) {
    npm --prefix diag-web run build:self-host-net48
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build:self-host-net48 failed with exit code $LASTEXITCODE."
    }
    Remove-Item -Recurse -Force DiagnosticExplorer.Hosting\wwwroot\net48\*
    Copy-Item -Recurse -Force diag-web\dist\self-host-net48\browser\* DiagnosticExplorer.Hosting\wwwroot\net48
}


dotnet build DiagnosticExplorer.slnx --configuration Debug -p:GeneratePackageOnBuild=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}
