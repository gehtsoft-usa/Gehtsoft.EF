# xUnit v3 — Migrating custom extensibility (reference)

Source: https://xunit.net/docs/getting-started/v3/migration-extensibility
(Upstream note: runner extensibility docs are still incomplete; when signatures matter, verify against the installed `xunit.v3.extensibility.core` package with `dotnet build` errors or by decompiling — don't guess.)

Packages: `xunit.extensibility.core` + `xunit.extensibility.execution` → single `xunit.v3.extensibility.core` (netstandard2.0). Reflection abstractions (`ITypeInfo`, `IMethodInfo`, `IAttributeInfo`, ...) are gone — v3 uses plain .NET reflection (`Type`, `MethodInfo`), which makes most custom code *simpler* after porting.

## Custom test case orderer (`ITestCaseOrderer`)

Common pattern: an orderer reading a `[TestOrder(n)]`-style attribute.

```csharp
// v2 (Xunit.Sdk + Xunit.Abstractions)
public class OrderAttributeOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases.OrderBy(tc => GetOrder(tc));

    private static int GetOrder(ITestCase tc)
        => tc.TestMethod.Method
             .GetCustomAttributes(typeof(TestOrderAttribute).AssemblyQualifiedName)
             .FirstOrDefault()?.GetNamedArgument<int>("Order") ?? 0;
}

// v3 (Xunit.v3 namespace; IReadOnlyCollection; real reflection)
public class OrderAttributeOrderer : ITestCaseOrderer
{
    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
        => testCases.OrderBy(GetOrder).ToList();

    private static int GetOrder(ITestCase tc)
        => (tc as IXunitTestCase)?.TestMethod.Method
             .GetCustomAttribute<TestOrderAttribute>()?.Order ?? 0;
}
```

Key points:
- Interface moved to `Xunit.v3`, takes/returns `IReadOnlyCollection<>` (prevents double enumeration) — return a materialized list.
- Attribute lookup is ordinary `GetCustomAttribute<T>()` on `MethodInfo` via `IXunitTestCase.TestMethod.Method`.
- The `[TestCaseOrderer]` attribute on test classes/assembly now takes `typeof(OrderAttributeOrderer)` instead of type-name + assembly-name strings.
- Same shape applies to `ITestCollectionOrderer` (`OrderTestCollections` returns `IReadOnlyCollection<TTestCollection>`).

## Custom data attributes

`IDataDiscoverer` is gone; data generation lives on the attribute itself, and it's async-capable:

```csharp
// v3 base
public abstract class DataAttribute : Attribute, IDataAttribute
{
    public abstract ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod, DisposalTracker disposalTracker);
    ...
}
```

- Return `ITheoryDataRow` items (`TheoryDataRow(params object?[])` or `TheoryDataRow<T1,...>`) instead of `object[]`.
- Register disposables created during data generation with the `DisposalTracker` instead of relying on GC.
- `MemberDataAttribute` internals renamed: `Parameters` → `Arguments`, `ConvertDataItem` → `ConvertDataRow` (returns `ITheoryDataRow`). Plain `[MemberData(nameof(...))]` usage in tests is unaffected.
- Theory data rows must be serializable (implement `IXunitSerializable` from `Xunit.Sdk` for custom types) for individual rows to appear in Test Explorer; otherwise rows collapse into one test case — functional but worse UX.

## BeforeAfterTestAttribute

Still exists; `Before`/`After` overrides now also receive the running `IXunitTest` instance. Update override signatures to match the v3 base class (compile errors will show the exact shape).

## Custom FactAttribute / TheoryAttribute subclasses

Simple subclasses (e.g. a `SkippableFactAttribute` setting `Skip`) usually port directly. v3 additionally allows *implementing* `IFactAttribute` instead of inheriting `FactAttribute`. Note v3 caches attribute instances — don't keep mutable per-test state in attributes.

New in v3 that may *replace* custom code (prefer deleting custom extensions over porting them):
- `Skip`, `SkipUnless`/`SkipWhen` (conditional skip), `Explicit`, `Timeout` on facts/theories
- Assembly-level `[Trait]`
- `ITestContextAccessor` / `TestContext.Current` for test state, output, and cancellation

## Custom test framework / discoverer / test cases

This is the deep end — read https://xunit.net/docs/getting-started/v3/migration-extensibility directly when porting one. Highlights:
- `ITestFramework`, `ITestFrameworkDiscoverer`, `ITestFrameworkExecutor` live in `Xunit.v3`; source-based discovery is removed.
- `ITestCase` metadata moved to `ITestCaseMetadata` (`TestCaseDisplayName`, `SkipReason`, `Timeout`, ...).
- `IXunitTestCase`: self-execution removed; implement `CreateTests()` returning `IXunitTest` instances; new `PreInvoke()`/`PostInvoke()` hooks.
- Runner classes (`XunitTestAssemblyRunner` et al.) are singletons; per-run state goes into context objects passed through `RunAsync`.
- `[TestFramework]` takes `typeof(...)` now.
