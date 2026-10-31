# Documentation

I've kept this directory focused on the current release. The older design notes, test reports, and development experiments are still available under `archive/`, organized by milestone.

## Current release — v0.3.0

- [Changelog](CHANGELOG.md) — what changed between versions.
- [v0.3.0 validation](VALIDATION-v0.3.0.md) — tests performed, automated coverage, and checks still pending.
- [v0.3.0 release notes](https://github.com/nocharlo/helium-charlo-fork/releases/tag/v0.3.0) — highlights and downloads.
- [Project README](../README.md) — installation, features, limitations, and credits.

## Development

The theme is in `theme/`, the Navigator extension is in `source/HeliumHybrid/`, and the tests are in `tests/`. The build and isolated Playnite tooling lives in `scripts/Helium-TestHarness.ps1`.

To compile and package the current checkout on Windows with PowerShell 7 and the .NET SDK installed, run this from the repository root:

```powershell
pwsh -NoProfile -File .\scripts\Helium-TestHarness.ps1 -Action Build -OutputRoot "$env:TEMP\helium-build"
```

The build and package checks do not replace testing interactions in Playnite Desktop. Use a separate portable Playnite installation for runtime checks, and keep personal library data and test logs outside the repository.

## Historical documentation

These files record specific older versions. Some descriptions, package names, and commands refer to the state of the project *at that time*, not to v0.3.0.

### v0.2.4 — Stability hotfix

- [Architecture and native integration](archive/v0.2.4/ARCHITECTURE.md)
- [Technical report](archive/v0.2.4/TECHNICAL-REPORT.md)
- [Windows testing checklist](archive/v0.2.4/WINDOWS-TESTING.md)
- [Validation record (JSON)](archive/v0.2.4/VALIDATION.json)

### v0.2.5 — Architecture foundation

- [Navigator architecture and extraction notes](archive/v0.2.5/NAVIGATOR-ARCHITECTURE.md)
- [Infrastructure audit](archive/v0.2.5/INFRASTRUCTURE-AUDIT.md)
- [Portable test harness notes](archive/v0.2.5/TEST-HARNESS.md)
- [Validation record](archive/v0.2.5/VALIDATION-v0.2.5.md)

### v0.2.6 — Navigator Essentials

- [Preset synchronization and ordering investigation](archive/v0.2.6/NAVIGATOR-ESSENTIALS-v0.2.6.md)

### Release maintenance

- [Historical release asset repair notes](archive/RELEASE-REPAIR.md)

The archived documents are retained for reference rather than rewritten to describe newer behavior.
