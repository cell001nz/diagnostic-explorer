[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot

npm --prefix diag-web run build
if ($LASTEXITCODE -ne 0) {
    throw "npm run build failed with exit code $LASTEXITCODE."
}

npm --prefix diag-web run build:self-host
if ($LASTEXITCODE -ne 0) {
    throw "npm run build:self-host failed with exit code $LASTEXITCODE."
}

npm --prefix diag-web run build:self-host-net48
if ($LASTEXITCODE -ne 0) {
    throw "npm run build:self-host-net48 failed with exit code $LASTEXITCODE."
}

Remove-Item -Recurse -Force DiagnosticExplorer.SelfHost\wwwroot\core\*
Copy-Item -Recurse -Force diag-web\dist\self-host\browser\* DiagnosticExplorer.SelfHost\wwwroot\core

Remove-Item -Recurse -Force DiagnosticExplorer.SelfHost\wwwroot\net48\*
Copy-Item -Recurse -Force diag-web\dist\self-host-net48\browser\* DiagnosticExplorer.SelfHost\wwwroot\net48

dotnet restore DiagnosticExplorer.slnx
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

dotnet build DiagnosticExplorer.slnx --no-restore --configuration $Configuration -p:GeneratePackageOnBuild=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}