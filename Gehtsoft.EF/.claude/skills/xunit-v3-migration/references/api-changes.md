# xUnit v3 — Namespace and API changes (reference)

Source: https://xunit.net/docs/getting-started/v3/migration

## Where types went after `xunit.abstractions` was removed

The `Xunit.Abstractions` namespace/package is gone (it existed for cross-AppDomain marshalling, which v3 no longer needs).

| Type(s) | New home |
|---|---|
| `ITestOutputHelper` | `Xunit` namespace (`xunit.v3.extensibility.core`) |
| `ISourceInformationProvider` | `Xunit.Runner.Common` |
| Message interfaces (`IMessageSink`, `IMessageSinkMessage`, `IErrorMessage`, `ITest*Starting/Finished`, `IDiagnosticMessage`, ...) | `Xunit.Sdk` (`xunit.v3.common`) |
| `ITest`, `ITestAssembly`, `ITestCase`, `ITestClass`, `ITestCollection`, `ITestMethod` | `Xunit.Sdk` |
| `IXunitSerializable`, `IXunitSerializationInfo` | `Xunit.Sdk` |
| `ITestFrameworkDiscoveryOptions`, `ITestFrameworkExecutionOptions` | `Xunit.Sdk` |
| `ITestFramework`, `ITestFrameworkDiscoverer`, `ITestFrameworkExecutor` | `Xunit.v3` |

Renames while moving:
- `IDiscoveryCompleteMessage` → `IDiscoveryComplete`
- `IFailureInformation` → `IErrorMetadata`
- `IFinishedMessage` → `IExecutionSummaryMetadata`

Removed entirely (use plain .NET reflection instead):
- `IAssemblyInfo`, `ITypeInfo`, `IMethodInfo`, `IParameterInfo`, `IAttributeInfo` and all `IReflectionXxxInfo` variants, plus `Reflector` and `ReflectionXyz` classes.
- `ISourceInformation` → `SourceInformation` struct.

Removed core types:
- `AsyncTestSyncContext` (no more `async void` tests)
- `AssemblyTraitAttribute` → put `[Trait]` directly on the assembly
- `PropertyDataAttribute` → `[MemberData]`
- `LongLivedMarshalByRefObject`, `PlatformSpecificAssemblyAttribute`
- `TestClassException` → `TestPipelineException`

## IAsyncLifetime

```csharp
// v2
public interface IAsyncLifetime : IDisposable
{
    Task InitializeAsync();
    Task DisposeAsync();
}

// v3
public interface IAsyncLifetime : IAsyncDisposable
{
    ValueTask InitializeAsync();
    // DisposeAsync() comes from IAsyncDisposable, returns ValueTask
}
```

Behavior change: in v2 both `DisposeAsync` and `Dispose` were called if both existed; in v3 only `DisposeAsync` runs. Merge any `Dispose` logic into `DisposeAsync`.

Migration template:

```csharp
// v2
public class DbFixture : IAsyncLifetime
{
    public Task InitializeAsync() { ...; return Task.CompletedTask; }
    public Task DisposeAsync() { ...; return Task.CompletedTask; }
}

// v3
public class DbFixture : IAsyncLifetime
{
    public ValueTask InitializeAsync() { ...; return ValueTask.CompletedTask; }
    public ValueTask DisposeAsync() { ...; return ValueTask.CompletedTask; }
}
```

## Attributes: string type names → typeof

```csharp
// v2
[assembly: CollectionBehavior("MyNs.MyFactory", "MyAssembly")]
[TestCaseOrderer("MyNs.MyOrderer", "MyAssembly")]

// v3
[assembly: CollectionBehavior(typeof(MyFactory))]
[TestCaseOrderer(typeof(MyOrderer))]
```

Applies to `CollectionBehaviorAttribute`, `TestCaseOrdererAttribute`, `TestCollectionOrdererAttribute`, `TestFrameworkAttribute`.

## Assertions

Largely backward compatible with v2.9. Watch for:
- New overloads creating ambiguity with custom assertion extensions.
- `Record.ExceptionAsync` now returns `ValueTask` and accepts `ValueTask`-returning lambdas — `await Record.ExceptionAsync(...)` call sites compile unchanged.
- `XunitException` remains in `Xunit.Sdk`; throwing it from custom helpers is still fine.

## Runner-utility layer (only if the solution hosts its own runner)

- `xunit.runner.reporters` + `xunit.runner.utility` merged into `xunit.v3.runner.utility`; common types in new `xunit.v3.runner.common` (`Xunit.Runner.Common` namespace).
- `IFrontController` is now `IAsyncDisposable`, exposes `FindAndRun`/`Run`; discovery-only via new `IFrontControllerDiscoverer`.
- Version-specific namespaces: `Xunit.Runner.v1`, `Xunit.Runner.v2`, `Xunit.Runner.v3`.
- `TestExecutionSummary` → `TestExecutionSummaries` (multi-assembly).
- Visitor classes (`XmlTestExecutionVisitor`, `TestMessageVisitor`, delegating sinks) consolidated into `ExecutionSink` + `ExecutionSinkOptions`.
- Concrete runner classes (`XunitTestAssemblyRunner` etc.) are singletons now; state moved from constructors into context objects passed to `RunAsync`.
- `ExceptionAggregator` is a struct in `Xunit.v3`; `RunAsync` returns `ValueTask`.

## xunit.runner.json

Update the schema reference:

```json
{
  "$schema": "https://xunit.net/schema/v3.0/xunit.runner.schema.json"
}
```

Most v2 settings carry over. Ensure the file is copied to output (`CopyToOutputDirectory=PreserveNewest`) — same requirement as v2.
