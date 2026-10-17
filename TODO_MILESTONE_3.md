# Milestone 3: Advanced Testing Features — Task Checklist

**Status: COMPLETE** — see [MILESTONE_3_SUMMARY.md](MILESTONE_3_SUMMARY.md).

## Goals
- Implement snapshot testing with Verify
- Add visual regression testing baseline
- Integrate property-based testing (FsCheck)
- Set up mutation testing configuration (Stryker.NET)
- Create reporting pipeline (Allure)

## Tasks
1. [x] Add Verify dependencies for snapshot testing
2. [x] Create snapshot testing helpers and extensions
3. [x] Add FsCheck dependencies for property-based testing
4. [x] Create property-based testing generators and extensions
5. [x] Add Allure reporting dependencies
6. [x] Create Allure reporting pipeline and integrations
7. [x] Configure Stryker.NET for mutation testing (as dotnet tool)
8. [x] Add mutation testing configuration and documentation
9. [x] Enhance ScreenshotHelper for visual regression testing
10. [x] Add visual regression testing attributes and utilities
11. [x] Create sample tests demonstrating advanced features
12. [x] Update documentation and README

## Dependencies
| Package | Version | Note |
|---|---|---|
| Verify.Xunit | 31.12.5 | Snapshot testing |
| FsCheck / FsCheck.Xunit | 3.4.0 | Property-based testing; requires `using FsCheck.Fluent` |
| Allure.Xunit | 2.15.0 | Reporting adapter (patches xUnit **v2**) |
| Allure.Net.Commons | 2.15.0 | Replaces the legacy `Allure.Commons` package |
| Reqnroll.Tools.MsBuild.Generation | 3.3.4 | Required for `.feature` code-behind |
| Reqnroll.xUnit | 3.3.4 | Required to surface scenarios in xUnit |
| xunit.runner.visualstudio | 2.8.2 | **Must stay on 2.8.x** for xUnit 2.9.3 + Allure |
| dotnet-stryker | 5.0.0 | Local tool via `.config/dotnet-tools.json` |

## Results
- Tests: **35 passed, 5 skipped, 0 failed (40 total)**
- Mutation score: **72.43%**

## Notes
- Verify's `SettingsTask` is lazily evaluated — always `await` it or the assertion is skipped.
- Approved snapshots live in `Snapshots/`; promote `*.received.*` to `*.verified.*` to accept.
- Visual regression baselines live in `Baselines/`, diffs in `TestResults/VisualDiffs/`.
- Windows-only tests use `[RequiresWindowsFact]` / `[RequiresWindowsTheory]` and skip elsewhere.