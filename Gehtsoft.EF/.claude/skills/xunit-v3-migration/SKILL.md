---
name: xunit-v3-migration
description: Migrate .NET test projects from xUnit v2 to xUnit v3 (xunit.v3 packages). Use this skill whenever the user asks to migrate, upgrade, or convert tests to xUnit v3 / xunit.v3, mentions the xUnit v2-to-v3 migration, or asks to modernize the xUnit test stack — even if they only say "upgrade xunit" or "move to the new xunit". Also use it to assess how much work an xUnit v3 migration would be for a solution.
---

# xUnit v2 → v3 Migration

Migrate test projects from xUnit v2 (`xunit` 2.x) to xUnit v3 (`xunit.v3`). Based on the official guide: https://xunit.net/docs/getting-started/v3/migration

The big architectural shift: **v3 test projects are stand-alone executables**, not libraries loaded by a runner. This drives most of the mechanical changes (OutputType, package names, removal of `xunit.abstractions`). `dotnet test` keeps working through `xunit.runner.visualstudio` 3.x.

## Workflow

Work one test project at a time. For each project: inventory → baseline → packages/csproj → code fixes → verify. Don't move to the next project until the current one builds and its test count matches the baseline.

### Step 1 — Inventory

Find every project referencing the `xunit` package and scan it for the patterns that need work. The grep below covers all of them:

```bash
# test projects
grep -rl 'PackageReference Include="xunit"' --include='*.csproj' .
# patterns needing code changes, per project:
grep -rn 'using Xunit.Abstractions\|IAsyncLifetime\|async void\|Xunit.Sdk\|ITestOutputHelper\|TestCaseOrderer\|CollectionBehavior\|TestCollectionOrderer\|TestFramework(' --include='*.cs' <test-project-dir>
ls <test-project-dir>/xunit.runner.json 2>/dev/null
```

Also check `Directory.Packages.props` / `Directory.Build.props` for centrally managed versions, and CI scripts (`*.bat`, `*.sh`, `*.yml`) for `dotnet test` / coverage invocations that may need attention.

Report the inventory to the user before changing anything if the migration looks non-trivial (custom extensibility, many projects).

### Step 2 — Baseline

Capture the current test count so you can prove nothing was lost:

```bash
dotnet test <project.csproj> --list-tests 2>/dev/null | grep -c .   # or run the suite and record passed/skipped/failed
```

A full pre-migration test run is better when feasible — some failures may pre-exist and shouldn't be blamed on the migration.

### Step 3 — Packages and csproj

**Prerequisites:** SDK-style project, target framework **net8.0+** or **net472+**. Projects on net6.0/net7.0 must be retargeted first — confirm with the user before changing TFMs.

Package mapping (versions: use latest stable; check with `dotnet package search xunit.v3 --take 1` or nuget.org):

| v2 package | v3 replacement |
|---|---|
| `xunit` | `xunit.v3` |
| `xunit.abstractions` | **remove** |
| `xunit.core` | `xunit.v3.core` |
| `xunit.assert` / `xunit.assert.source` | `xunit.v3.assert` / `xunit.v3.assert.source` |
| `xunit.extensibility.core` + `xunit.extensibility.execution` | `xunit.v3.extensibility.core` (merged) |
| `xunit.runner.utility` + `xunit.runner.reporters` | `xunit.v3.runner.utility` (merged) |
| `xunit.runner.console` | `xunit.v3.runner.console` |
| `xunit.runner.msbuild` | `xunit.v3.runner.msbuild` |
| `xunit.runner.visualstudio` | keep, but must be **3.x** |
| `xunit.analyzers` | keep (same name); update to latest |
| `Microsoft.NET.Test.Sdk` | keep for `dotnet test`/VSTest; update to latest |

csproj changes:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>   <!-- v3 test projects are executables -->
</PropertyGroup>
```

Keep `IsPackable=false` if present. Third-party test libraries fall into three groups — check them **locally, no web search needed**:

1. **xUnit-independent** (Moq, NSubstitute, coverlet, loggers like LiquidTestReports): unaffected, keep as is.
2. **Compile-time xUnit dependents** (`*.xunit`-flavored helper/reporter packages): their nuspec declares the xunit dependency. Check it:
   ```bash
   curl -s https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.nuspec | grep -i xunit
   ```
   A dependency on `xunit.core`/`xunit.extensibility.*` 2.x means v2-only — look for a release whose nuspec references `xunit.v3.*` (list versions: `curl -s https://api.nuget.org/v3-flatcontainer/<id>/index.json`).
3. **Runtime-binding assertion libraries** (FluentAssertions, AwesomeAssertions, Shouldly): they detect the test framework at runtime to throw `XunitException`, so nothing shows in nuspec or at compile time. Check the shipped assembly for v3 awareness:
   ```bash
   grep -lc 'xunit\.v3\|Xunit3' ~/.nuget/packages/<id>/<version>/lib/*/*.dll
   ```
   A match means the library knows how to detect xUnit v3. Either way, the **definitive check is the deliberate-failure test in Step 5** — an incompatible library surfaces as "test framework not detected" (or wrong exception type) only when an assertion actually fails, which a fully green run never exercises.

### Step 4 — Code fixes

Apply in this order; each is mechanical. Full details and signatures: read `references/api-changes.md` when you hit one of these patterns.

1. **`using Xunit.Abstractions;`** — delete the using. `ITestOutputHelper` now lives in `Xunit`; runner/message types moved to `Xunit.Sdk`, `Xunit.v3`, or `Xunit.Runner.Common`.
2. **`async void` tests** — change to `async Task` (or `ValueTask`). v3 fast-fails them at runtime, so they'd show up as failures, not silently pass.
3. **`IAsyncLifetime`** — now inherits `IAsyncDisposable`, not `IDisposable`. Signatures become `ValueTask InitializeAsync()` and `ValueTask DisposeAsync()`. If a fixture implemented both `Dispose` and `DisposeAsync`, only `DisposeAsync` is called in v3 — merge cleanup logic into `DisposeAsync`.
4. **String-based type references in attributes** — `[TestCaseOrderer("Ns.Type", "Asm")]`, `[TestCollectionOrderer(...)]`, `[CollectionBehavior(...)]`, `[TestFramework(...)]` now take `typeof(...)`.
5. **Custom extensibility** (`Xunit.Sdk` usages: orderers, data attributes, `BeforeAfterTestAttribute`, custom test cases/frameworks, `IXunitSerializable`) — read `references/extensibility.md`. This is where real porting work lives.
6. **`XunitException`** still exists in `Xunit.Sdk` (package `xunit.v3.assert`) — custom assertion helpers that throw it usually compile unchanged.
7. **Theory data** — `TheoryData<>` and `[MemberData]`/`[InlineData]`/`[ClassData]` usage in tests is source-compatible in the common cases. Exception: the named argument in `[MemberData(nameof(X), parameters: ...)]` is now `arguments:` — `grep -rn 'parameters:' --include='*.cs'` finds these. Only custom `DataAttribute` subclasses and code touching `MemberDataAttribute` internals need deeper changes (see extensibility reference). v3 additionally requires `MemberData` sources to be accessible and theory data to be serializable for Test Explorer listing — analyzer warnings will point these out.
8. **`xunit.runner.json`** — update `$schema` to `https://xunit.net/schema/v3.0/xunit.runner.schema.json`; most v2 keys carry over.

After the build is clean, the `xunit.analyzers` package surfaces v3-specific issues (xUnit1xxx/2xxx/3xxx) — fix warnings rather than suppressing them.

### Step 5 — Verify

1. `dotnet build` — zero errors, review new analyzer warnings.
2. `dotnet test` — must still work via `xunit.runner.visualstudio` 3.x + `Microsoft.NET.Test.Sdk`.
3. Compare test count against the Step 2 baseline. **A silently shrinking test count is the #1 migration failure mode** (discovery changes, serialization issues with theory data). Investigate any delta.
   If tests fail and the suite depends on external services (databases, queues), don't blame the migration yet: re-run with `--logger trx`, then classify every failure by error type (connection/timeout vs. assertion) and probe the service ports (`timeout 3 bash -c 'echo > /dev/tcp/<host>/<port>'`). All-connection-errors confined to unreachable services means environmental, not migration breakage.
4. **Deliberate-failure check** (required if the project uses a fluent assertion library): temporarily break one assertion, run that single test (`dotnet test --filter ...`), and confirm it reports as a normal test failure with the expected message — not "test framework could not be detected" or a raw `InvalidOperationException`. Revert the change **and rebuild** — a later run of the stale binary will otherwise show a phantom failure. A green suite never exercises the assertion library's failure path, so this is the only way to prove runtime compatibility.
5. v3 bonus check: the project now runs directly — `dotnet run --project <test.csproj>` (use `-- -?` for runner switches). Useful for diagnosing discovery problems because it prints them to the console.
6. Re-run any CI/coverage scripts found in Step 1 (`dotnet test --collect ...` keeps working in VSTest mode).

## Gotchas

- **Don't mix v2 and v3 packages** in one project — `xunit` and `xunit.v3` together produce duplicate-attribute compile errors.
- A solution can contain v2 and v3 *projects* side by side during incremental migration; `dotnet test` at solution level handles both as long as each project's own packages are consistent.
- Shared test-utility *library* projects referenced by test projects: if they use xUnit types, they must reference `xunit.v3.extensibility.core` (netstandard2.0-compatible) and be migrated together with the test projects.
- New assertion overloads in v3 can make previously unambiguous calls ambiguous with custom/3rd-party assertion extensions — fix by casting or removing the redundant custom overload.
- v3 caches attribute instances; attributes with mutable state that relied on per-use instantiation will misbehave.
- Custom runner reporters changed completely (linked into the test assembly, chosen via `-reporter`); third-party reporters may not have v3 support yet.
