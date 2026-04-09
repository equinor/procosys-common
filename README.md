# procosys-common

Libraries with common code for ProCoSys solutions:

- **Equinor.ProCoSys.Auth** — Library for getting users permissions in ProCoSys. ProCoSys permissions are made as claims in logged on users ClaimsPrincipal
- **Equinor.ProCoSys.BlobStorage** — Library for interact with Azure Storage in ProCoSys
- **Equinor.ProCoSys.Common** — Library with common code in ProCoSys

## CI/CD

| Workflow | Trigger | Purpose |
|---|---|---|
| 🤖 Build & run tests | Pull request | Builds solution and runs all unit tests |
| ✏️ Verify formatting | Pull request | Checks `dotnet format` compliance for all projects |
| 🗃️ Verify PR Title | Pull request | Enforces [Conventional Commits](https://www.conventionalcommits.org/) titles |
| 📦 Publish Auth | Push to `main` (path-filtered) | Packs and publishes `Equinor.ProCoSys.Auth` to GitHub Packages |
| 📦 Publish BlobStorage | Push to `main` (path-filtered) | Packs and publishes `Equinor.ProCoSys.BlobStorage` to GitHub Packages |
| 📦 Publish Common | Push to `main` (path-filtered) | Packs and publishes `Equinor.ProCoSys.Common` to GitHub Packages |

> **Note:** NuGet packages are automatically published when changes are merged to `main`. Each package has its own path-filtered workflow, so only changed packages are republished.

## PR & Publish Flow

1. Create a feature branch and open a pull request
2. CI checks run automatically (build, tests, formatting, PR title)
3. On merge to `main`, affected packages are published to GitHub Packages

