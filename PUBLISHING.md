# Publishing NuGet Packages

NuGet packages are published by the GitHub Actions workflow when a version tag is pushed.

## One-time setup

Create a NuGet.org trusted-publishing policy for:

- Repository owner: `cell001nz`
- Repository: `diagnostic-explorer`
- Workflow file: `publish-nuget-packages.yml`
- NuGet.org user: `cell001uk`

## Publish a release

Start from a clean worktree on the commit to release, then run:

```powershell
./New-NuGetRelease.ps1
```

The script fetches the latest published `DiagnosticExplorer` version from NuGet.org, displays it, suggests the next patch version, and prompts for a new SemVer version. It verifies the worktree, branch, and tag before asking for confirmation, then creates and pushes an annotated `v` tag.

The same script is available as the VS Code task **Create NuGet Release Tag**. For non-interactive use, pass `-NewVersion` and `-Force`; use `-WhatIf` to preview the tag operation.

The pushed tag triggers the NuGet workflow and produces packages with the version after `v`.

Monitor the run in the repository's GitHub Actions page. The workflow can also be run manually; provide the package version when prompted.
