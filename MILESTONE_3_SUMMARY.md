# Milestone 3: Advanced Testing Features — Summary

**Status: COMPLETE**

## What was delivered

| Task | Status | Where |
|---|---|---|
| Verify dependencies | ✅ | `DesktopAutomationTest.Tests.csproj` |
| Snapshot helpers | ✅ | `Helpers/SnapshotHelper.cs`, `ModuleInit.cs`, `Snapshots/` |
| FsCheck dependencies | ✅ | `DesktopAutomationTest.Tests.csproj` |
| Property-based generators | ✅ | `Helpers/PropertyBasedHelper.cs` |
| Allure dependencies | ✅ | `Allure.Xunit` + `Allure.Net.Commons` |
| Allure reporting pipeline | ✅ | `Helpers/AllurePipeline.cs`, `allureConfig.json` |
| Stryker.NET configuration | ✅ | `stryker-config.json`, `.config/dotnet-tools.json` |
| Mutation testing docs | ✅ | `README.md` |
| ScreenshotHelper visual regression | ✅ | `Helpers/VisualRegressionHelper.cs` |
| Visual regression attributes | ✅ | `Helpers/PlatformAttributes.cs` |
| Sample tests | ✅ | `AdvancedFeaturesTests.cs` |
| Documentation / README | ✅ | `README.md` |

**Test results: 35 passed, 5 skipped, 0 failed (40 total).**
Mutation score: **72.43%** (thresholds: high 80 / low 60).

## Problems found and fixed

### 1. FsCheck was never actually integrated
The previous milestone reported "FsCheck needs fixing — types `Arb`, `Prop`, `Arbitrary`,
`Property` cannot be resolved" and shipped a stubbed helper that threw
`NotImplementedException`.

**Root cause:** FsCheck 3.x moved `Arb`, `Prop`, `Gen` and the combinators into the
**`FsCheck.Fluent`** namespace. The old code only had `using FsCheck;`.

Consequences discovered while fixing:
- There is no `Arb.Create<T>()`; the default arbitrary is `FsCheck.FSharp.ArbMap.defaults.ArbFor<T>()`.
- `Gen.Bind` takes an `FSharpFunc`; from C# use `Gen.SelectMany`.
- `Gen.Map` takes curried `FSharpFunc`s, so `Arb.Zip`/`Gen.Zip` are easier to compose.
- FsCheck.Xunit 3.4 **removed** `[Property(ArbitrarySources = ...)]` and `MemberType`, so custom
  arbitraries are bound explicitly with `Prop.ForAll(arb, …)`.

The old tests were also vacuous — they generated raw FsCheck values rather than the project's
arbitraries and asserted only `!= null`. They now sample each arbitrary and assert real
invariants, plus genuine property tests over `Guard` and `UserBuilder`.

### 2. Snapshot assertions were silently no-ops
`SnapshotHelper` called `Verifier.Verify(...)` and discarded the returned `SettingsTask`.
Verify evaluates **lazily**, so no comparison ever ran and the tests passed unconditionally.

Fixed by returning `Task` from every helper and awaiting it in `async Task` tests. Verified the
fix catches real regressions by temporarily editing a baseline (test failed as expected).

### 3. Allure was installed but produced nothing
`allure-results/` was created but always empty, and every `AllureApi` call was a silent no-op.

Two independent causes:
- `Allure.Commons 3.5.0.73` was the **legacy Java-era** package (exposes `AllureLifecycle` /
  `AllureStorage`). The real .NET SDK is `Allure.Net.Commons`, pulled in transitively by
  `Allure.Xunit`. Replaced.
- `xunit.runner.visualstudio` was **3.1.4**, the **xUnit v3** runner, while the project uses
  xUnit **2.9.3**. Allure.Xunit 2.x patches the v2 runner via Harmony, so with the v3 runner it
  never attached (`AllureLifecycle.Instance.Context.Test` stayed `null`). Pinned to 2.8.2 and
  documented the constraint in the csproj.

Allure output is now verified: nested steps, attachments, and severity/feature/story labels all
appear in the generated result JSON.

### 4. BDD scenarios were never generated
Reqnroll was referenced but produced **zero** tests. `.feature` files alone are not enough —
Reqnroll 3.x also needs `Reqnroll.Tools.MsBuild.Generation` (code-behind) and
`Reqnroll.xUnit` (xUnit bridge). Added both; the two calculator scenarios now execute.

`SampleSteps` also injected an unused `IDriver`, which Reqnroll's container cannot resolve, so
every scenario failed with `ObjectContainerException`. Since the scenario is pure arithmetic, the
unused dependency was removed and the requirement to register the driver is documented.

### 5. Stryker 5.x config schema
- The document must be wrapped in a single `stryker-config` root object.
- Options are `disable-bail` / `break-on-initial-test-failure` / `additional-timeout`
  (not `disable-bail-out` / `ignore-initial-failures` / `timeout-ms`), and there is no
  `ignore-tests` or `ignore-mutations-in` option.
- The Tests project was missing from `DesktopAutomationTest.slnx`, so Stryker could not locate
  the test project. Added.

### 6. Windows-only dependencies
FlaUI.UIA3 (COM UI Automation) and `System.Drawing.Common` (GDI+) both throw on Linux. Instead of
leaving a permanently failing test or hiding it behind `--filter`, these are marked with
`[RequiresWindowsFact]` / `[RequiresWindowsTheory]` and report as **skipped**.

## Remaining follow-ups (not Milestone 3 scope)

- Reqnroll steps that drive the UI still need an `IDriver` binding
  (a `[ScenarioContext]` method calling `AddScoped`) before injecting the driver.
- Visual regression baselines for real application screens will be recorded in Milestone 5.
- Mutation score is 72.43%, below the 80 "high" threshold; raising it needs additional tests.