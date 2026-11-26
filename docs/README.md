# Documentation

For installation, current features and known limitations, start with the [project README](../README.md).

## Current release — v0.3.4

- [Release and downloads](https://github.com/nocharlo/helium-charlo-fork/releases/tag/v0.3.4)
- [Changelog](CHANGELOG.md)
- [Navigator UX conventions](NAVIGATOR-UX.md)
- [Development guidelines](../AGENTS.md)

## Source and testing

- `theme/` — Desktop theme and WPF integration.
- `source/HeliumNavigator/` — Navigator C# extension.
- `tests/` — automated checks.
- `scripts/` — build, packaging, OPSEC and isolated Playnite test tooling.

To build and package the current checkout on Windows with PowerShell 7 and the .NET SDK installed, run:

```powershell
pwsh -NoProfile -File .\scripts\Helium-TestHarness.ps1 -Action Build -OutputRoot "$env:TEMP\helium-build"
```

Use a separate portable Playnite installation for interactive tests. A successful build or automated test run does not establish runtime compatibility with every library and extension configuration.

The extension's internal namespace and source directory are now `HeliumNavigator`. Its shipped `HeliumCharlo.dll`, plugin ID, theme bindings and settings location remain unchanged for compatibility.

## Versioned records

Older reports are retained under `archive/` and describe their original versions; their paths, terminology and technical assumptions should not be read as current instructions.

- **v0.3.0:** [Testing and validation](archive/v0.3.0/VALIDATION-v0.3.0.md).
- **v0.2.6:** [Navigator Essentials investigation](archive/v0.2.6/NAVIGATOR-ESSENTIALS-v0.2.6.md).
- **v0.2.5:** [Architecture](archive/v0.2.5/NAVIGATOR-ARCHITECTURE.md), [infrastructure audit](archive/v0.2.5/INFRASTRUCTURE-AUDIT.md), [test harness](archive/v0.2.5/TEST-HARNESS.md) and [validation](archive/v0.2.5/VALIDATION-v0.2.5.md).
- **v0.2.4:** [Architecture](archive/v0.2.4/ARCHITECTURE.md), [technical report](archive/v0.2.4/TECHNICAL-REPORT.md), [Windows testing](archive/v0.2.4/WINDOWS-TESTING.md) and [validation data](archive/v0.2.4/VALIDATION.json).
- **Historical release maintenance:** [Release asset repair notes](archive/RELEASE-REPAIR.md).

Historical release rebuilds continue to reference the original tagged source layout. These references are intentional and do not affect current builds.
