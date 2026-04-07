# SonarQube Maintainability Issues Plan

Total: 633 issues as of 2026-04-04.

---

## Group A — Already handled / suppress

- **S3267** (22): Already added to `sonar-project.properties`, will clear after next scan.
- **S2187** (1, `TestApp/DebugTests.cs`): Empty test class that's intentionally a debug placeholder. Suppress or add a dummy test.

---

## Group B — Trivial mechanical fixes (no logic change)

Each item is a 1-line change per occurrence, safe to do in bulk.

| Rule | Count | What |
|------|------:|------|
| CA1822 / S2325 | 24 | Make methods `static` (don't access instance data) |
| CA1847 / CA1866 / S6610 | 17 | Use `char` overloads: `Contains('x')`, `IndexOf('x')`, `StartsWith('x')` |
| S6562 | 14 | Specify `DateTimeKind` when constructing `DateTime` |
| CA1861 | 14 | Move inline constant arrays to `static readonly` fields |
| S3260 | 8 | Seal private classes that aren't derived |
| S1481 | 5 | Remove unused local variables |
| S1192 | 5 | Extract repeated string literals to constants |
| CA1510 | 4 | Use `ArgumentNullException.ThrowIfNull` |
| CA1834 | 3 | `StringBuilder.Append(char)` instead of `Append(string)` |
| S3878 | 3 | Don't wrap values in arrays for `params` parameters |
| CA1825 | 2 | Use `Array.Empty<T>()` |
| CA1868 | 2 | Remove redundant `Contains` check before `HashSet.Add` |
| S1643 | 2 | Use `StringBuilder` instead of `+` concatenation in loops |
| CA1854 | 1 | Use `TryGetValue` instead of `ContainsKey` + indexer |
| S3247 | 1 | Use pattern matching instead of `is`-check + cast |
| S125 / Web | 2 | Remove commented-out code |
| CA1845 | 1 | `AsSpan` instead of `Substring` |
| CA1862 | 1 | Pass `StringComparison` to `Equals` |

**~111 issues**, spread across production and test code.

Files affected:
- `Gehtsoft.EF.Bson/EntityToBsonController.cs`
- `Gehtsoft.EF.Db.MssqlDb/MssqlDbLanguageSpecifics.cs`
- `Gehtsoft.EF.Db.MssqlDb/MssqlInsertSelectQueryBuilder.cs`
- `Gehtsoft.EF.Db.MysqlDb/MysqlInsertQueryBuilder.cs`
- `Gehtsoft.EF.Db.OracleDb/OracleDbLanguageSpecifics.cs`
- `Gehtsoft.EF.Db.PostgresDb/PostgresLanguageSpecifics.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/EdmModelBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/ODataProcessor.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/ODataToQuery.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/XmlSerializableDictionary.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/DeclareStatement.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/InsertRunner.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/SelectRunner.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/SqlAstVisitor.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/SqlCodeDomBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/StatementRunners.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/CreateEntity/CreateEntityController.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/DynamicEntity/DynamicEntity.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/AllEntities.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/Linq/ExpressionCompiler.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/InsertQueryBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/InsertSelectQueryBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/SelectQueryBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/TableDescriptor.cs`
- `Gehtsoft.EF.Db.SqlDb/SqlDbConnection.cs`
- `Gehtsoft.EF.Db.SqlDb/UpdateQueryToTypeBinder.cs`
- `Gehtsoft.EF.Db.SqliteDb/SqliteConnection.cs`
- `Gehtsoft.EF.Db.SqliteDb/SqliteLanguageSpecifics.cs`
- `Gehtsoft.EF.FTS/FtsConnection.cs`
- `Gehtsoft.EF.MongoDb/Context/EntityContext.cs`
- `Gehtsoft.EF.MongoDb/MongoConnectionFactory.cs`
- `Gehtsoft.EF.MongoDb/Queries/MongoSelectQueryBase.cs`
- `Gehtsoft.EF.MongoDb/Queries/PathTranslator.cs`
- `Gehtsoft.EF.Northwind/Snapshot.cs`
- `Gehtsoft.EF.Utils/EntityPathAccessor.cs`
- `Gehtsoft.EF.Utils/TypeConverter.cs`
- `Gehtsoft.EF.Test/Entity/DynamicEntity.cs`
- `Gehtsoft.EF.Test/Entity/Linq/LinqExtension.cs`
- `Gehtsoft.EF.Test/Entity/Query/ConditionBuilder.cs`
- `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_AdvancedSelect.cs`
- `Gehtsoft.EF.Test/SqlDb/DefaultValueInTableColumnTest.cs`
- `Gehtsoft.EF.Test/SqlDb/FtsTest.cs`
- `Gehtsoft.EF.Test/SqlDb/SqlQueryBuilder/ConditionBuilder.cs`
- `Gehtsoft.EF.Test/SqlDb/SqlQueryBuilder/UpdateQueries.cs`
- `Gehtsoft.EF.Test/Utils/SqlConnectionSources.cs`
- `Gehtsoft.EF.Test/Utils/SqlParser/AstNodeExtensions.cs`
- `Gehtsoft.EF.Test/Utils/TestValue.cs`
- `TestApp/AggregatesTest.cs`
- `TestApp/NorthwindTest.cs`
- `TestApp/PatchTest.cs`
- `TestApp/TestCreateAndDrop.cs`
- `TestApp/TestEntityResolver.cs`
- `TestWebApp/Startup.cs`
- `TestWebApp/Views/Shared/_Layout.cshtml`

---

## Group C — Moderate fixes (need context, small risk)

| Rule | Count | What |
|------|------:|------|
| S1133 | 11 | Remove deprecated API members — confirm no external consumers first |
| CA1859 | 14 | Narrow return types to concrete types — safe for internal/private, risky for public API |
| S1944 | 1 | Potentially invalid cast in `SelectQueryResultBinder.cs` — needs inspection |
| S1694 | 1 | `SqlTableSpecification` is abstract with no abstract members — make concrete or convert to interface |
| S1871 | 2 | Identical code in separate `if`/`switch` branches — extract or merge |
| CS0618 | 1 | Usage of deprecated API in `ODataProcessor.cs` — fix the call site |
| SYSLIB1045 | 2 | Convert `new Regex(...)` literals to `[GeneratedRegex]` — .NET 7+ only, `AstNodeExtensions.cs` |
| S107 | 2 | Too many parameters — requires API design decision (`GenericEntityAccessorWithAggregates`, `FtsConnection`) |
| S1479 | 1 | Too many `switch` cases in `SqlExpressionParser.cs` — likely needs a dispatch table |

**~35 issues**.

Files affected:
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityQuery/ConditionEntityQueryBaseBackwardCompatibility.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityGenericAccessor/GenericEntityAccessorWithAggregates.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/QueryWithWhereBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/SelectQueryBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/SelectQueryResultBinder.cs`
- `Gehtsoft.EF.Db.SqlDb/SelectQueryToTypeBinder.cs`
- `Gehtsoft.EF.Db.SqlDb/SqlDbConnection.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/ODataProcessor.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlExpressionParser.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlTableSpecification.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/StatementRunners.cs`
- `Gehtsoft.EF.FTS/FtsConnection.cs`
- `Gehtsoft.EF.MongoDb/BsonFilterExpressionBuilder.cs`
- `Gehtsoft.EF.Utils/MD5HashCreator.cs`
- `Gehtsoft.EF.Test/Utils/SqlParser/AstNodeExtensions.cs`
- `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_Infrastructure.cs`
- `Gehtsoft.EF.Test/Mongo/MongoQueries_ViaContext.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql.Test/` (all *Run.cs files — CA1859)

---

## Group D — S3776 Cognitive Complexity (92 issues, 70 files)

The largest effort. Methods exceed SonarQube's cognitive complexity threshold (max 15).
Same approach used for `AllEntitiesExtension.cs`: extract semantically meaningful private helpers.
Each method needs individual analysis before refactoring.

Worst offenders (complexity score):
- `ODataProcessor.SelectDataCore` — 120
- `ODataToQuery` (3 methods) — 101, 81, 53
- `StatementRunners` — multiple methods
- `SqlCodeDomBuilder`, `SelectQueryBuilder`, `ExpressionCompiler`

Full file list:
- `Gehtsoft.EF.Bson/EntityToBsonController.cs`
- `Gehtsoft.EF.Db.MssqlDb/MssqlConnection.cs`
- `Gehtsoft.EF.Db.MssqlDb/MssqlSelectQueryBuilder.cs`
- `Gehtsoft.EF.Db.MysqlDb/MysqlDbLanguageSpecifics.cs`
- `Gehtsoft.EF.Db.OracleDb/OracleConnection.cs`
- `Gehtsoft.EF.Db.OracleDb/OracleDbLanguageSpecifics.cs`
- `Gehtsoft.EF.Db.PostgresDb/PostgresConnection.cs`
- `Gehtsoft.EF.Db.PostgresDb/PostgresLanguageSpecifics.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/EdmModelBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/ODataProcessor.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/ODataToQuery.cs`
- `Gehtsoft.EF.Db.SqlDb.OData/XmlSerializableDictionary.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/IfStatement.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlBinaryExpression.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlExpressionParser.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlField.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlInExpression.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlInsertStatement.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlSelectStatement.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlUnaryExpression.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/Statement.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SwitchStatement.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/InsertRunner.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/SelectRunner.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/SqlCodeDomBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb.Sql/StatementRunners.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityGenericAccessor/GenericEntityAccessor.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityGenericAccessor/GenericEntityAccessorFilter.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityGenericAccessor/GenericEntityAccessorWithAggregates.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/CreateEntity/CreateEntityController.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/CreateEntity/Patch/EfPatchProcessor.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityConnection.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/ColumnDiscoverer.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/DynamicEntityDiscoverer.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityQuery/EntityQueryConditionBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityQueryBuilder/SelectEntityQueryBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityQueryBuilder/SelectEntityQueryBuilderBase.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/Linq/ExpressionCompiler.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/Linq/QueryableEntityProvider.cs`
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/Linq/SelectExpressionCompiler.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/ConditionBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/InsertSelectQueryBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/QueryWithWhereBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/QueryBuilder/SelectQueryBuilder.cs`
- `Gehtsoft.EF.Db.SqlDb/SelectQueryResultBinder.cs`
- `Gehtsoft.EF.Db.SqlDb/SelectQueryToTypeBinder.cs`
- `Gehtsoft.EF.Db.SqlDb/SqlLanguageSpecifics.cs`
- `Gehtsoft.EF.Db.SqlDb/SqlQuery.cs`
- `Gehtsoft.EF.Db.SqlDb/UpdateQueryToTypeBinder.cs`
- `Gehtsoft.EF.Db.SqliteDb/SqliteConnection.cs`
- `Gehtsoft.EF.Db.SqliteDb/SqliteLanguageSpecifics.cs`
- `Gehtsoft.EF.Entities/EntityFinder.cs`
- `Gehtsoft.EF.Entities/NamingPolicy/EntityNameConvertor.cs`
- `Gehtsoft.EF.Entities/Tools/EntityComparerHelper.cs`
- `Gehtsoft.EF.FTS/FtsConnection.cs`
- `Gehtsoft.EF.MongoDb/BsonFilterExpressionBuilder.cs`
- `Gehtsoft.EF.MongoDb/Queries/BsonValueExtension.cs`
- `Gehtsoft.EF.MongoDb/Queries/MongoUpdateEntityQuery.cs`
- `Gehtsoft.EF.MongoDb/Queries/PathTranslator.cs`
- `Gehtsoft.EF.Northwind/Factory/CsvReader.cs`
- `Gehtsoft.EF.Northwind/Snapshot.cs`
- `Gehtsoft.EF.Utils/TypeConverter.cs`

---

## Group E — xUnit1042 (241 issues, test-only)

All `[MemberData]` methods returning `IEnumerable<object[]>` should return typed
`TheoryData<string>` (or `TheoryData<string, ...>`). Pure test infrastructure change,
no production impact. Mechanical but large-scale: 19 test files, ~241 occurrences.

Files affected:
- `Gehtsoft.EF.Test/Entity/Linq/LinqOnDB_CUD.cs`
- `Gehtsoft.EF.Test/Entity/Linq/LinqOnDB_Select.cs`
- `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_AdvancedSelect.cs`
- `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_Create.cs`
- `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_GenericAccessor.cs`
- `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_Infrastructure.cs`
- `Gehtsoft.EF.Test/Entity/Query/QueryiesOnDb_UpdateAndBasicSelect.cs`
- `Gehtsoft.EF.Test/Mongo/MongoQueries.cs`
- `Gehtsoft.EF.Test/Mongo/MongoQueries_ViaContext.cs`
- `Gehtsoft.EF.Test/Northwind/TestNorthwind.cs`
- `Gehtsoft.EF.Test/SqlDb/AdvancedSelectTest.cs`
- `Gehtsoft.EF.Test/SqlDb/BasicQueryTests.cs`
- `Gehtsoft.EF.Test/SqlDb/DefaultValueInTableColumnTest.cs`
- `Gehtsoft.EF.Test/SqlDb/DropCreateTest.cs`
- `Gehtsoft.EF.Test/SqlDb/Factory/UnversalConfigurationFactory.cs`
- `Gehtsoft.EF.Test/SqlDb/FtsTest.cs`
- `Gehtsoft.EF.Test/SqlDb/HierarchicalSelectTest.cs`
- `Gehtsoft.EF.Test/SqlDb/QueryAsyncMethodsTest.cs`
- `Gehtsoft.EF.Test/SqlDb/SchemaTest.cs`

---

## Suggested execution order

1. **Group B** — quick wins, clears ~111 issues with minimal risk
2. **Group A** — suppress S2187
3. **Group C** — moderate items, file by file
4. **Group E** — xUnit1042 in test project (large but mechanical)
5. **Group D** — S3776 refactoring, heaviest effort, one file at a time
