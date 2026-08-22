$ErrorActionPreference = 'Stop'
$packageOutput = Join-Path $PSScriptRoot 'artifacts\packages'
$packageProjects = @(
	'DiagnosticExplorer\DiagnosticExplorer.csproj'
	'DiagnosticExplorer.Log4Net\DiagnosticExplorer.Log4Net.csproj'
	'DiagnosticExplorer.Hosting\DiagnosticExplorer.Hosting.csproj'
	'DiagnosticExplorer.SelfHost\DiagnosticExplorer.SelfHost.csproj'
	'DiagnosticExplorer.Extensions.Logging\DiagnosticExplorer.Extensions.Logging.csproj'
	'DiagnosticExplorer.Serilog\DiagnosticExplorer.Serilog.csproj'
	'DiagnosticExplorer.NLog\DiagnosticExplorer.NLog.csproj'
)

Set-Location $PSScriptRoot

& "$PSScriptRoot\BuildDev.ps1" -Configuration Release

Remove-Item -Recurse -Force $packageOutput -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $packageOutput | Out-Null

foreach ($packageProject in $packageProjects) {
	dotnet pack $packageProject --no-build --no-restore --configuration Release --output $packageOutput
	if ($LASTEXITCODE -ne 0) {
		throw "dotnet pack $packageProject failed with exit code $LASTEXITCODE."
	}
}