# Plan: Improve Test Coverage — Driver Type Conversions & SQL DSL

## Context

Coverage analysis after full test run (2551 tests) revealed:
- Driver type conversion methods have significant gaps for nullable types, Guid, decimal, bool?, Int16, byte, TimeSpan
- SqlDb.Sql module (SQL DSL parser) at 62% — best covered via high-level query compilation tests
- Core SqlDb.dll at 89.8% — smaller public API gaps

## Priority 1: Driver Type Conversions — DONE

### What was done

**Part A — Entity round-trip tests:** Extended `TestDataTypeArgs` in `QueryiesOnDb_Infrastructure.cs` with 14 new cases:
- Non-null nullable values: `bool?` (true/false), `Guid?`, `Date?`, `DateTime?`, `int?`, `long?`, `double?`, `decimal?`
- New type `short` (Int16): 5 cases (basic, MaxValue, MinValue, null, non-null nullable)
- Total round-trip tests: 242 (all pass across all configured DBs)

**Part B — Direct LanguageSpecifics unit tests:** Created `Gehtsoft.EF.Test/SqlDb/LanguageSpecificsTypeTests.cs` (44 tests):
- Base class (Sql92): `byte`→DbType.Byte, `TimeSpan`→DbType.Time, `char`→DbType.String, `short`→TypeToDb, `object`→TypeToDb, nullable unwrap, null handling
- Per-driver ToDbValue spot checks: SQLite (bool?, Guid?, decimal?, DateTime? including zero-ticks), Oracle (bool?, Guid?, int?), MySQL (bool?→Int16, Guid?), MSSQL (bool? true/false/null), Postgres (base fallthrough)
- Per-driver TranslateValue spot checks: SQLite (bool?, Guid?, Guid parse failures, DateTime?, DateTime null), Oracle (bool?, Guid?, Guid parse failures), MySQL (bool?, Guid?, Guid parse failure)

**Bug found and fixed:** SQLite `DateTime?` `ToDbValue` ignored `StoreDateAsString` — always stored as Double even when column is TEXT. Fixed in `SqliteLanguageSpecifics.cs` to mirror the non-nullable `DateTime` branch.

### Types not coverable via entity round-trip

`byte` (DbType.Byte), `TimeSpan` (DbType.Time), `char` — no driver `TypeName` handles these DbTypes, so table creation throws. Covered via direct `ToDbValue` calls in Part B instead.

### TypeName size/precision variants — not yet covered

These branches in driver `TypeName` methods require specific size/precision combos at table creation time. Not addressed in P1:
- `Int16` case: Oracle, MSSQL, MySQL, Postgres
- `Double` with size/precision: Oracle, MSSQL, MySQL
- `Decimal` with size/precision: Oracle, MSSQL, MySQL, Postgres
- `Int32`/`Int64` non-autoincrement: Postgres

These could be covered by direct `TypeName` unit tests in `LanguageSpecificsTypeTests.cs` if desired.

## Priority 2: SqlDb.Sql — High-level Query Tests (~2033 uncovered lines, 62%)

Test the SQL DSL parser via real queries, not unit-testing individual CodeDom nodes.

**Biggest gaps:**
- `SqlParser.cs` (654 uncov) — AST visiting
- `StatementRunners.cs` (292 uncov) — expression evaluation
- `SqlBinaryExpression.cs` (138 uncov) — binary expression type coercion
- INSERT/UPDATE/DELETE runners (~130 uncov combined)
- Cursor operations, IF/SWITCH, SET statements

### Approach

Extend existing tests under `Gehtsoft.EF.Test/Utils/SqlParser/` or create `Gehtsoft.EF.Test/SqlDb/SqlDslQueryTests.cs`.

Use `SqlCodeDomBuilder` to parse and execute SQL DSL statements against in-memory SQLite. Group tests by statement type:
1. **SELECT** — joins, subqueries, aggregates, HAVING, expressions in resultset (already partially covered)
2. **INSERT** — with VALUES, with SELECT, with expressions
3. **UPDATE** — SET with expressions, WHERE conditions
4. **DELETE** — with WHERE, with subquery conditions
5. **Cursor** — OPEN/FETCH/CLOSE cursor operations
6. **Flow control** — IF/ELSE, SWITCH/CASE, SET variable
7. **Expressions** — binary operations with type coercion, IN expressions, NULL handling

Each test: parse SQL text → execute against SQLite → verify results.

**File to create:** `Gehtsoft.EF.Test/SqlDb/SqlDslExecutionTests.cs`

## Priority 3: Core SqlDb.dll Public API Gaps (lower priority)

- `SelectEntitiesQueryBase.AddToResultset(Type, int, string[])` — untested overload
- `SelectEntityQueryReader<T>.Scan(Func<T, bool>)` — untested utility
- `DynamicEntityPropertyCollection.Remove/IndexOf` — collection operations
- LINQ `QueryableEntityProvider.FindIEnumerable` — internal reflection helper

**File to create:** `Gehtsoft.EF.Test/SqlDb/CoreApiCoverageTests.cs`

## Files

| File | Priority | Status |
|---|---|---|
| `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_Infrastructure.cs` | P1 | DONE — 14 cases added to TestDataTypeArgs |
| `Gehtsoft.EF.Test/SqlDb/LanguageSpecificsTypeTests.cs` | P1 | DONE — 44 direct ToDbValue/TranslateValue tests |
| `Gehtsoft.EF.Db.SqliteDb/SqliteLanguageSpecifics.cs` | P1 | DONE — DateTime? StoreDateAsString bug fixed |
| `Gehtsoft.EF.Test/SqlDb/SqlDslExecutionTests.cs` | P2 | TODO — SQL DSL parser + runners |
| `Gehtsoft.EF.Test/SqlDb/CoreApiCoverageTests.cs` | P3 | TODO — Remaining public API gaps |

## Verification

1. `dotnet build Gehtsoft.EF.Test/`
2. `dotnet test Gehtsoft.EF.Test/` — all pass
3. Re-run coverage, verify:
   - Driver modules: 90%+ each (from current 84-95%)
   - SqlDb.Sql.dll: 70%+ (from 62%)
   - SqlDb.dll: 92%+ (from 89.8%)
