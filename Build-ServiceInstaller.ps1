[CmdletBinding()]
param(
    [string] $PackageVersion,
    [switch] $SkipAngularBuild
)

$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot

if (-not $SkipAngularBuild) {
    npm --prefix diag-web run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build failed with exit code $LASTEXITCODE."
    }
}

$spaOutput = Join-Path $PSScriptRoot 'diag-web\dist\diag-exp\browser\index.html'
if (-not (Test-Path $spaOutput)) {
    throw "The Diagnostic Service SPA build was not found at '$spaOutput'."
}

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $serviceProject = Get-Content (Join-Path $PSScriptRoot 'DiagnosticService\Diagnostic.Service.csproj') -Raw
    $assemblyVersion = [regex]::Match($serviceProject, '<AssemblyVersion>([^<]+)</AssemblyVersion>').Groups[1].Value
    $PackageVersion = (($assemblyVersion -split '\.')[0..2] -join '.')
}

$installerVersion = $PackageVersion -replace '-.*$', ''
if ($installerVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Installer version '$PackageVersion' must begin with a three-part numeric version."
}

$publishDirectory = Join-Path $PSScriptRoot 'artifacts\installer\publish'
$packageOutput = Join-Path $PSScriptRoot 'artifacts\packages'
Remove-Item -Recurse -Force $publishDirectory -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null

dotnet publish DiagnosticService\Diagnostic.Service.csproj --configuration Release --runtime win-x64 --self-contained true --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

dotnet build DiagnosticService.Installer\Diagnostic.Service.Installer.wixproj --configuration Release "-p:InstallerVersion=$installerVersion" "-p:ServicePublishDirectory=$publishDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build installer failed with exit code $LASTEXITCODE."
}

$msi = Get-ChildItem (Join-Path $PSScriptRoot 'DiagnosticService.Installer\bin\Release') -Filter '*.msi' -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $msi) {
    throw 'The WiX build completed without producing an MSI.'
}

$msiOutput = Join-Path $packageOutput "DiagnosticExplorer.Service-$installerVersion-win-x64.msi"
Copy-Item -Force $msi.FullName $msiOutput
$latestMsiOutput = Join-Path $packageOutput 'DiagnosticExplorer.Service-win-x64.msi'
Copy-Item -Force $msi.FullName $latestMsiOutput
Write-Host "Created $msiOutput"