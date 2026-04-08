# Plan: Migrate TestApp to Gehtsoft.EF.Test/Legacy

## Context

TestApp is a legacy NUnit-based test project (~9100 lines, 31 files) that tests database operations across MSSQL, MySQL, PostgreSQL, SQLite, Oracle, and MongoDB. It uses a custom `Config.ini` for connections, NUnit `[TestFixture]`/`[Test]` attributes, and a mix of AwesomeAssertions and `ClassicAssert`.

The goal is to migrate all test cases into `Gehtsoft.EF.Test/Legacy/` subfolder, converting to xUnit with AwesomeAssertions, and using `Gehtsoft.EF.Test`'s existing infrastructure (`SqlConnectionFixtureBase`, `SqlConnectionSources`, `MongoConnectionFixtureBase`, `Configuration.json`) for connection management.

## Approach

### Structure

Create `Gehtsoft.EF.Test/Legacy/` with test classes organized by concern. Entity classes used by tests go into `Gehtsoft.EF.Test/Legacy/Entities/`.

### Conversion patterns

**NUnit -> xUnit:**
- `[TestFixture]` -> remove (xUnit discovers public classes automatically)
- `[Test]` -> `[Fact]` (no parameters) or `[Theory]` (parameterized)
- `[TestCase(...)]` -> `[InlineData(...)]`
- `[OneTimeSetUp]`/`[OneTimeTearDown]` -> `IClassFixture<T>` with constructor injection
- `[Explicit]` -> `[Fact(Skip = "Explicit")]`
- `[Ignore("...")]` -> `[Fact(Skip = "...")]`
- `ClassicAssert.AreEqual(a, b)` -> `b.Should().Be(a)`
- `ClassicAssert.IsTrue(x)` -> `x.Should().BeTrue()`
- `ClassicAssert.NotNull(x)` -> `x.Should().NotBeNull()`
- `ClassicAssert.IsNull(x)` -> `x.Should().BeNull()`

**Connection management:**
- SQL tests: Use `SqlConnectionFixtureBase` + `SqlConnectionSources.SqlConnectionNames()` via `[Theory]`/`[MemberData]`
- Mongo tests: Use `MongoConnectionFixtureBase` + `SqlConnectionSources.MongoConnectionNames()`
- Per-database fixtures (MssqlTest, PostgresTest, etc.) are eliminated; their test calls become Theory methods running against all configured connections

### Files to create

#### Infrastructure
- `Gehtsoft.EF.Test/Legacy/Entities/` - shared entity classes extracted from helpers

#### From static helpers -> inlined into xUnit test classes (with Theory/MemberData for connections)
| Source helper | Target file | Notes |
|---|---|---|
| `TestCreateAndDrop.cs` | `Legacy/CreateAndDropTests.cs` | ~379 lines, CRUD + data types |
| `TestEntity1.cs` | `Legacy/EntityTests.cs` | ~1884 lines, largest file - may split into multiple classes |
| `TestEntity2.cs` | `Legacy/SqlInjectionTests.cs` | ~107 lines |
| `TestFts.cs` | `Legacy/FtsTests.cs` | ~503 lines, sync + async |
| `TestHierarchical.cs` | `Legacy/HierarchicalTests.cs` | ~209 lines |
| `TestPerformance.cs` | skip | Low value, not migrating |
| `TestDbUpdate.cs` | `Legacy/DbUpdateTests.cs` | ~312 lines, schema alteration |
| `TestEntityResolver.cs` | `Legacy/EntityResolverTests.cs` | ~2056 lines, may split |
| `EntityContextTest.cs` | `Legacy/EntityContextTests.cs` | ~175 lines |
| `AggregatesTest.cs` | `Legacy/AggregatesTests.cs` | ~235 lines |
| `TestTasks.cs` | skip | TestTasksImpl, not migrating |

#### From standalone tests -> converted to xUnit
| Source | Target file | Notes |
|---|---|---|
| `EntityNameConvertorTest.cs` | `Legacy/EntityNameConvertorTests.cs` | TestCase -> InlineData |
| `EntityReaderTest.cs` | `Legacy/EntityReaderTests.cs` | Standalone, no DB |
| `BsonSerializerTest.cs` | `Legacy/BsonSerializerTests.cs` | Standalone, no DB |
| `NorthwindTest.cs` | skip | Not migrating |
| `ODataProcessorTest.cs` | `Legacy/ODataProcessorTests.cs` | Uses in-memory SQLite |
| `ODataTest.cs` | `Legacy/ODataModelTests.cs` | Standalone, no DB |
| `PatchTest.cs` | `Legacy/PatchTests.cs` | Uses in-memory SQLite |
| `ResiliencyTest.cs` | `Legacy/ResiliencyTests.cs` | Standalone, no DB |
| `DebugTests.cs` | skip | Not migrating |
| `ConnectionFactoryTest.cs` | skip | Empty stub |

#### Mongo tests -> keep structure, use MongoConnectionFixtureBase
| Source | Target file | Notes |
|---|---|---|
| `MongoQueryTest.cs` | `Legacy/Mongo/MongoQueryTests.cs` | BSON filter tests |
| `MongoTestNoRef.cs` | `Legacy/Mongo/MongoNoRefTests.cs` | ~474 lines, entity ops |

### Database-specific fixture mapping

The 5 per-database fixtures (MssqlTest, MySqlTest, PostgresTest, SqliteDbTest, OracleTest) are NOT migrated as-is. Instead, their test method calls are absorbed into the inlined test classes above. Each inlined class gets its own `Fixture : SqlConnectionFixtureBase` and uses `[Theory]`/`[MemberData(nameof(ConnectionNames))]` to run against all configured connections.

Some tests were only run on specific databases (e.g., `TestHierarchical` not on MySQL, `NestedTransactionsTest` not on MySQL/Oracle). These filters should be preserved using the flag system: `SqlConnectionSources.SqlConnectionNames("-mysql")`.

### What NOT to migrate
- `Config.cs` / `config.ini` - replaced by `Configuration.json`
- `Program.cs` - NUnitLite runner, not needed
- `GlobalSuppressions.cs` - review, merge if needed
- `ConnectionFactoryTest.cs` - empty stub

## Execution order

1. Create `Legacy/Entities/` folder and extract shared entity classes
2. Start with small standalone tests (EntityNameConvertor, EntityReader, Resiliency) to establish the pattern
3. Move to medium helpers (TestCreateAndDrop, TestEntity2, EntityContext, Aggregates)
4. Tackle large helpers (TestEntity1, TestEntityResolver)
5. Convert FTS, Hierarchical, DbUpdate, Performance, Concurrency tests
6. Convert OData tests (ODataProcessor, ODataTest)
7. Convert Mongo tests
8. Convert Northwind, Patch, BsonSerializer tests
9. Verify build compiles
10. Run tests against available connections

## Verification

1. `dotnet build Gehtsoft.EF.Test/` - must compile
2. `dotnet test Gehtsoft.EF.Test/ --filter "FullyQualifiedName~Legacy"` - run migrated tests
3. Compare test count: ensure all non-skipped tests from TestApp have equivalents
4. Verify connection filtering flags match original per-database restrictions
