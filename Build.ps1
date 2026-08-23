param(
	[string] $PackageVersion
)

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

& "$PSScriptRoot\BuildDev.ps1"
if ($LASTEXITCODE -ne 0) {
	throw "BuildDev.ps1 failed with exit code $LASTEXITCODE."
}

dotnet restore DiagnosticExplorer.slnx
if ($LASTEXITCODE -ne 0) {
	throw "dotnet restore failed with exit code $LASTEXITCODE."
}

$buildArguments = @(
	'build',
	'DiagnosticExplorer.slnx',
	'--no-restore',
	'--configuration', 'Release',
	'-p:GeneratePackageOnBuild=false'
)
if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
	$assemblyVersion = (($PackageVersion -replace '-.*$', '') + '.0')
	$buildArguments += "-p:DiagnosticExplorerPackageVersion=$PackageVersion"
	$buildArguments += "-p:DiagnosticExplorerAssemblyVersion=$assemblyVersion"
	$buildArguments += "-p:DiagnosticExplorerFileVersion=$assemblyVersion"
}

dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
	throw "dotnet build failed with exit code $LASTEXITCODE."
}

Remove-Item -Recurse -Force $packageOutput -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $packageOutput | Out-Null

foreach ($packageProject in $packageProjects) {
	$packArguments = @(
		'pack',
		$packageProject,
		'--no-build',
		'--no-restore',
		'--configuration', 'Release',
		'--output', $packageOutput
	)
	if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
		$packArguments += "-p:DiagnosticExplorerPackageVersion=$PackageVersion"
	}
	dotnet @packArguments
	if ($LASTEXITCODE -ne 0) {
		throw "dotnet pack $packageProject failed with exit code $LASTEXITCODE."
	}
}

& "$PSScriptRoot\Build-ServiceInstaller.ps1" -PackageVersion $PackageVersion -SkipAngularBuild