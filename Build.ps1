$ErrorActionPreference = 'Stop'

npm --prefix diag-web install
npm --prefix diag-web run build

$selfHostBuildJobs = @(
	Start-Job -Name 'self-host' -ScriptBlock {
		param($workingDirectory)
		Set-Location $workingDirectory
		npm --prefix diag-web run build:self-host
		if ($LASTEXITCODE -ne 0) {
			throw "npm run build:self-host failed with exit code $LASTEXITCODE."
		}
	} -ArgumentList $PSScriptRoot
	Start-Job -Name 'self-host-net48' -ScriptBlock {
		param($workingDirectory)
		Set-Location $workingDirectory
		npm --prefix diag-web run build:self-host-net48
		if ($LASTEXITCODE -ne 0) {
			throw "npm run build:self-host-net48 failed with exit code $LASTEXITCODE."
		}
	} -ArgumentList $PSScriptRoot
)

try {
	$selfHostBuildJobs | Wait-Job | Out-Null
	$selfHostBuildJobs | Receive-Job

	$failedBuildJobs = $selfHostBuildJobs | Where-Object State -ne 'Completed'
	if ($failedBuildJobs) {
		throw "One or more self-host builds failed: $($failedBuildJobs.Name -join ', ')."
	}
}
finally {
	$selfHostBuildJobs | Remove-Job -Force
}

Remove-Item -Recurse -Force DiagnosticExplorer.SelfHost\wwwroot\core\*
Copy-Item -Recurse -Force diag-web\dist\self-host\browser\* DiagnosticExplorer.SelfHost\wwwroot\core

Remove-Item -Recurse -Force DiagnosticExplorer.SelfHost\wwwroot\net48\*
Copy-Item -Recurse -Force diag-web\dist\self-host-net48\browser\* DiagnosticExplorer.SelfHost\wwwroot\net48

dotnet restore DiagnosticExplorer.slnx
dotnet build DiagnosticExplorer.slnx --no-restore