[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $PackageId = 'DiagnosticExplorer',
    [string] $Remote = 'origin',
    [string] $NewVersion,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$semVerPattern = '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$'

function Invoke-Git {
    param([Parameter(Mandatory)][string[]] $Arguments)

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$($output -join [Environment]::NewLine)"
    }
    return $output
}

function ConvertTo-SemanticVersion {
    param(
        [Parameter(Mandatory)][string] $Value,
        [Parameter(Mandatory)][string] $Description
    )

    if ($Value -notmatch $semVerPattern) {
        throw "$Description '$Value' is not valid SemVer, such as 5.0.3 or 5.1.0-preview.1."
    }

    return [System.Management.Automation.SemanticVersion]::new($Value)
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git was not found on PATH.'
}

$packagePath = $PackageId.ToLowerInvariant()
$versionsUri = "https://api.nuget.org/v3-flatcontainer/$packagePath/index.json"
Write-Host "Fetching published versions for $PackageId..."
$response = Invoke-RestMethod -Uri $versionsUri
$publishedVersions = @(
    $response.versions |
    ForEach-Object {
        [pscustomobject]@{
            Text    = $_
            Version = ConvertTo-SemanticVersion -Value $_ -Description 'Published package version'
        }
    } |
    Sort-Object Version
)

if ($publishedVersions.Count -eq 0) {
    throw "NuGet returned no published versions for package '$PackageId'."
}

$current = $publishedVersions[-1]
$suggestedVersion = "$($current.Version.Major).$($current.Version.Minor).$($current.Version.Patch + 1)"
Write-Host "Current NuGet version: $($current.Text)"

if ([string]::IsNullOrWhiteSpace($NewVersion)) {
    $NewVersion = Read-Host "New package version [$suggestedVersion]"
    if ([string]::IsNullOrWhiteSpace($NewVersion)) {
        $NewVersion = $suggestedVersion
    }
}

$NewVersion = $NewVersion.Trim()
$newSemanticVersion = ConvertTo-SemanticVersion -Value $NewVersion -Description 'New package version'
if ($newSemanticVersion -le $current.Version) {
    throw "New package version '$NewVersion' must be greater than current NuGet version '$($current.Text)'."
}

Set-Location $PSScriptRoot
Invoke-Git -Arguments @('rev-parse', '--is-inside-work-tree') | Out-Null
$worktreeChanges = @(Invoke-Git -Arguments @('status', '--porcelain'))
if ($worktreeChanges.Count -gt 0 -and -not $Force) {
    throw 'The Git worktree is not clean. Commit or stash changes before creating a release tag.'
}

$branch = (Invoke-Git -Arguments @('branch', '--show-current')).Trim()
if ([string]::IsNullOrWhiteSpace($branch)) {
    throw 'HEAD is detached. Check out the release branch before creating a release tag.'
}

$commit = (Invoke-Git -Arguments @('rev-parse', '--short', 'HEAD')).Trim()
Invoke-Git -Arguments @('fetch', $Remote, '--tags', '--quiet') | Out-Null

$tagName = "v$NewVersion"
$existingTag = @(Invoke-Git -Arguments @('tag', '--list', $tagName))
if ($existingTag.Count -gt 0) {
    throw "Tag '$tagName' already exists."
}

Write-Host "Branch: $branch"
Write-Host "Commit: $commit"
Write-Host "Tag: $tagName"
Write-Host "Remote: $Remote"

if (-not $Force) {
    $confirmation = Read-Host "Type 'yes' to create and push $tagName"
    if ($confirmation -cne 'yes') {
        Write-Host 'Release tag creation cancelled.'
        return
    }
}

if (-not $PSCmdlet.ShouldProcess("$Remote/$tagName", 'Create annotated release tag and push it')) {
    return
}

Invoke-Git -Arguments @('tag', '-a', $tagName, '-m', "Release $NewVersion") | Out-Null
Invoke-Git -Arguments @('push', $Remote, $tagName) | Out-Null
Write-Host "Pushed $tagName. The NuGet publishing workflow should now start."
