# Publishing NuGet Packages

NuGet packages are published by the GitHub Actions workflow when a version tag is pushed.

## One-time setup

Create a NuGet.org trusted-publishing policy for:

- Repository owner: `cell001nz`
- Repository: `diagnostic-explorer`
- Workflow file: `publish-nuget-packages.yml`
- NuGet.org user: `cell001uk`

## Publish a release

Choose an unused SemVer version, then create and push a matching `v` tag:

```powershell
git checkout main
git pull --ff-only
git tag v5.0.2
git push origin v5.0.2
```

The `v5.0.2` tag triggers the NuGet workflow and produces packages with version `5.0.2`.

Monitor the run in the repository's GitHub Actions page. The workflow can also be run manually; provide the package version when prompted.
