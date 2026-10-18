# Desktop Automation Test Platform

A reusable automation-testing platform for .NET desktop applications (WPF, WinForms, WinUI/MAUI) designed to catch both UI bugs (layout, state, binding, accessibility, visual regressions) and logical bugs (business rules, calculations, data flow, edge cases).

## Project Structure

```
DesktopAutomationTest.slnx          Solution (Core + Tests)
DesktopAutomationTest.Core/         Shared test library
DesktopAutomationTest.Tests/        Test project demonstrating framework integration
stryker-config.json                 Mutation testing configuration
.config/dotnet-tools.json           Local tool manifest (dotnet-stryker)
```

## Milestones

### ✅ Milestone 1: Core Library Foundation
- Shared test library project with base test fixtures
- Driver abstraction interface (`IDriver`) with FlaUI adapter
- Common utilities (wait helpers, screenshot comparison, guard clauses, driver factory)

### ✅ Milestone 2: Test Framework Integration
- xUnit as the primary test framework
- Reqnroll for BDD scenarios
- FluentAssertions, Moq, coverlet.collector, ReportGenerator
- Test data builder pattern (`UserBuilder`)

### ✅ Milestone 3: Advanced Testing Features
- **Snapshot testing** with Verify — approved snapshots in `Snapshots/`
- **Property-based testing** with FsCheck — reusable arbitraries in `Helpers/PropertyBasedHelper.cs`
- **Visual regression testing** — baseline recording and pixel-diff comparison
- **Allure reporting** — steps, attachments, labels and screen diffs
- **Mutation testing** with Stryker.NET

### ✅ Milestone 4: CI/CD Pipeline
- GitHub Actions workflow for Windows UI tests (`.github/workflows/windows-ci.yml`)
- Artifact collection for failures
- Flaky test retry and parallel execution limits

### ✅ Milestone 5: Sample Application Integration (WPF application, page object models, onboarding guide)
- Sample WPF application ✓
- Page object models ✓  
- Onboarding guide ✓
- Fixed Reqnroll build errors to enable cross-platform test execution ✓

## Getting Started

### Prerequisites
- .NET 10.0 SDK
- Windows OS for UI automation and image comparison (see [Platform Support](#platform-support))

### Running Tests
```bash
dotnet test DesktopAutomationTest.slnx
```

### Generating Coverage Reports
```bash
dotnet test DesktopAutomationTest.slnx \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=opencover

reportgenerator "-reports:DesktopAutomationTest.Tests/TestResults/**/coverage.opencover.xml" \
    "-targetdir:coverletreports" "-reporttypes:Html"
```

## Platform Support

Two dependencies are Windows-only:

| Dependency | Used by | Limitation |
|---|---|---|
| `FlaUI.UIA3` | `DriverFactory` / `FlaUIAdapter` | COM UI Automation |
| `System.Drawing.Common` | `ScreenshotHelper` image comparison | GDI+ |

Tests that need these are marked `[RequiresWindowsFact]` / `[RequiresWindowsTheory]` and report
as **skipped** (not failed) elsewhere, so the suite runs green on Linux and macOS.

## Advanced Testing Features

### Snapshot testing (Verify)

Approved snapshots live in `Snapshots/` next to the source file, so baselines show up in normal
code-review diffs.

```csharp
[Fact]
public async Task Snapshot_UserObject_RoundTripsThroughSnapshot()
{
    var user = new UserBuilder().WithFirstName("John").Build();
    await SnapshotHelper.VerifySnapshotAsync(user);
}
```

> **Important:** Verify returns a lazily-evaluated `SettingsTask`. Always `await` it — a helper
> that discards the task silently skips the comparison and the test passes unconditionally.

When a snapshot is intentionally changed, accept the new value by promoting the `.received.*`
file to `.verified.*`:

```bash
for f in DesktopAutomationTest.Tests/Snapshots/*.received.*; do
  mv "$f" "${f/.received./.verified.}"
done
```

### Property-based testing (FsCheck)

`Helpers/PropertyBasedHelper.cs` exposes reusable arbitraries (`UserArbitrary`, `ValidEmail`,
`ValidAge`, `AutomationIdArbitrary`, …) plus property builders:

```csharp
PropertyBasedHelper.Check(
    FsCheck.Fluent.Prop.ForAll(
        PropertyBasedHelper.ValidString,
        value => string.IsNullOrWhiteSpace(value)
            == (Record.Exception(() => Guard.AgainstNullOrEmpty(value, "v")) is ArgumentException)));
```

> **FsCheck 3.x note:** `Arb`, `Prop` and the `Gen` combinators moved to the **`FsCheck.Fluent`**
> namespace. There is no `Arb.Create<T>()`; use `PropertyBasedHelper.Default<T>()` (which wraps
> `FsCheck.FSharp.ArbMap.defaults.ArbFor<T>()`). `Gen.Bind` takes an FSharp function, so use
> `Gen.SelectMany` from C#. FsCheck.Xunit 3.4 also removed `[Property(ArbitrarySources=...)]`.

### Visual regression testing

Baselines are stored under `Baselines/`, diffs under `TestResults/VisualDiffs/`.

```csharp
VisualRegressionHelper.AssertMatches("login-screen", actualScreenshotPath, thresholdPercentage: 1.0);
```

The first run records the baseline and passes; later runs compare and, on failure, throw
`ImageComparisonException` naming the baseline, actual and diff paths. Use
`VisualRegressionHelper.AcceptBaseline(name, path)` to approve a deliberate change.

### Allure reporting

Results are written to `allure-results/` (relative to the test output directory) and can be
rendered with the Allure CLI:

```bash
allure serve DesktopAutomationTest.Tests/bin/Debug/net10.0/allure-results
```

```csharp
AllurePipeline.Label(severity: "critical", feature: "Checkout");

var total = AllurePipeline.Step("sum-prices", () => prices.Sum());

AllurePipeline.AttachScreenDiff(baselinePath, actualPath, diffPath);
AllurePipeline.RecordResult(testResult);   // forwards metadata as Allure parameters
```

> **Runner pinning matters:** this project uses **xUnit 2.9.3**, so `xunit.runner.visualstudio`
> must stay on the **2.8.x** line. Allure.Xunit 2.x patches the xUnit **v2** runner via Harmony;
> with the v3 runner it silently never attaches and every `AllureApi` call becomes a no-op
> (`AllureLifecycle.Instance.Context.Test` stays `null` and `allure-results/` stays empty).

### Mutation testing (Stryker.NET)

```bash
dotnet tool restore
dotnet stryker
```

The HTML report is written to `StrykerOutput/<timestamp>/html`. Thresholds are configured in
`stryker-config.json` (high 80 / low 60). Stryker 5.x requires the whole document to be wrapped
in a single `stryker-config` root object.

## Features

### Core Library & Agent Engine
- **Driver Abstraction**: `IDriver` with FlaUI implementation
- **Token-Efficient Agent Automation**:
  - `CompactUiTree`: filtered, semantic-only UI hierarchy dump (`[Button #Submit "Submit"]`) saving ~90% tokens
  - `AgentBatchRunner`: multi-action JSON execution pipeline in 1 tool call
  - `FuzzyLocator`: Levenshtein self-healing locators that avoid trial-and-error retry loops
  - `AgentStateTracker`: real-time UI diffing / delta tracking
  - `SessionRecorder` & `SessionReplayer`: zero-token replay of captured automation sessions
  - `CompactUiElement`: lightweight JSON/Text DTO for LLM context windows
- **Model Context Protocol (MCP) Server**: 9 tools exposing full automation to Claude, Cursor, and any MCP agent
- **AutoNex CLI**: Global `dotnet tool` for inspection, batch plans, record, and replay
- **Desktop Controls**: CheckBox, ComboBox (text/index), RadioButton automation
- **Advanced Keyboard & Mouse**: DoubleClick, RightClick, Hover, PressKey, SendKeys, Drag & Drop
- **Base Test Fixture**: shared setup/teardown
- **Wait Helpers**: condition-based waiting with timeout and polling
- **Screenshot Helpers**: image comparison and threshold validation
- **Guard Clauses**: argument validation
- **TestResult**: status, timing, errors, metadata
- **Test Data Builders**: `TestDataBuilder<T>` pattern

### Test Helpers
- `SnapshotHelper` — Verify assertions with caller-path forwarding
- `PropertyBasedHelper` — FsCheck arbitraries and property builders
- `VisualRegressionHelper` — baseline recording, comparison and diff generation
- `AllurePipeline` — Allure steps, attachments, labels and result bridging
- `PlatformAttributes` — `[RequiresWindowsFact]` / `[RequiresWindowsTheory]`

## Documentation

- [AI Agent Desktop Automation Guide (Token-Efficient)](AGENT_AUTOMATION_GUIDE.md)
- [Milestone 1 Completion Details](MILESTONE_1_COMPLETE.md)
- [Milestone 2 Progress](MILESTONE_2_PROGRESS.md)
- [Milestone 3 Summary](MILESTONE_3_SUMMARY.md)
- [CI/CD Pipeline](CICD.md)

## License

MIT