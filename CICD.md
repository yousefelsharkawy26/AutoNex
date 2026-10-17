# CI/CD Pipeline for Desktop Automation Test Platform

## Overview

This document describes the CI/CD pipeline implemented for the Desktop Automation Test Platform using GitHub Actions.

## Workflow: windows-ci.yml

The workflow is defined in `.github/workflows/windows-ci.yml` and runs on Windows machines to execute UI tests that require Windows-specific dependencies.

### Triggers

The workflow runs on:
- Push to `main` and `develop` branches
- Pull requests targeting `main` and `develop` branches

### Environment Variables

- `SOLUTION_FILE`: Path to the solution file (`DesktopAutomationTest.slnx`)
- `TEST_PROJECT`: Path to the test project (`DesktopAutomationTest.Tests/DesktopAutomationTest.Tests.csproj`)
- `CONFIGURATION`: Build configuration (`Release`)

### Steps

1. **Checkout**: Uses `actions/checkout@v4` to get the repository code
2. **Setup .NET**: Installs .NET 10.0 SDK using `actions/setup-dotnet@v4`
3. **Restore dependencies**: Runs `dotnet restore` on the solution
4. **Build**: Compiles the solution in Release mode
5. **Test with retry logic**: 
   - Executes tests with normal verbosity
   - If initial run fails, waits 10 seconds and retries only failed tests
   - This helps mitigate flaky UI tests
6. **Collect test results**: Uploads TRX test result files as artifacts (retained 7 days)
7. **Collect failure artifacts**: On failure, uploads test results, snapshots, baselines, and visual diffs (retained 14 days)
8. **Report test results**: Uses `dorny/test-reporter@v1` to annotate pull requests with test results

### Windows-Specific Considerations

The workflow runs on `windows-latest` runners to ensure availability of:
- FlaUI.UIA3 (COM UI Automation)
- System.Drawing.Common (GDI+ for image comparison)

Tests requiring these dependencies are marked with `[RequiresWindowsFact]` or `[RequiresWindowsTheory]` attributes and will be skipped on non-Windows platforms.

### Artifacts

- **test-results-{run_id}**: Contains TRX files from test runs
- **failure-artifacts-{run_id}**: Contains failure diagnostics (only when workflow fails)

### Test Result Reporting

The workflow uses the test-reporter action to:
- Parse TRX files
- Annotate pull requests with test failures
- Provide inline error messages for failed assertions

## Local Execution

To run the same tests locally:

```bash
# Build and test
dotnet test DesktopAutomationTest.Tests/DesktopAutomationTest.Tests.csproj --configuration Release

# With verbose output
dotnet test DesktopAutomationTest.Tests/DesktopAutomationTest.Tests.csproj --configuration Release --verbosity detailed
```

## Flaky Test Handling

The workflow implements a simple retry mechanism for flaky tests:
1. Initial test run
2. If failures detected, wait 10 seconds
3. Retry only previously failed tests
4. Report combined results

This approach helps distinguish between consistent failures and intermittent issues in UI automation.