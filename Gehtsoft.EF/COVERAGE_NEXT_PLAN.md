# Plan: High-Level Integration Tests for Gehtsoft.EF.Db.SqlDb.Sql.dll (P2)

## Context

Coverage of `Gehtsoft.EF.Db.SqlDb.Sql.dll` is 61.1% (2032 uncovered lines). The existing `Gehtsoft.EF.Db.SqlDb.Sql.Test` project has 47 tests across 13 files but leaves large gaps in constant expression evaluation, binary type coercion, built-in functions in SET context, and several SQL query features (DISTINCT, LEFT JOIN, AVG/SUM, NOTLIKE, unary expressions, subqueries in expressions).

The approach: parse SQL DSL text -> execute against in-memory SQLite with Northwind data -> verify results. One new test file, following existing conventions exactly.

## Bugs Discovered During Analysis

These pre-existing bugs should be documented in test comments but are **out of scope** for fixing:

1. **TOINT vs TOINTEGER naming mismatch** (`sql.gram` keyword is `TOINT`, but `StatementRunners.cs:205,696` and `SqlExpressionParser.cs:209` check `"TOINTEGER"`) -- the function is dead code, never matches.
2. **STARTSWITH/ENDSWITH/CONTAINS negated in CalculateExpression** (`StatementRunners.cs:263,267,271`) -- returns `!result` in constant-eval context, producing inverted values vs SQL execution path.
3. **IN-list CalculateExpression bug** (`StatementRunners.cs:293`) -- evaluates `inExpression.LeftOperand` instead of `expr` (the list item), so IN with list always matches.
4. **IN-SELECT CalculateExpression cast bug** (`StatementRunners.cs:309`) -- `recordObj as Dictionary<string, object>` returns null because SelectRunner.BindRecord returns ExpandoObject (implements `IDictionary`, not `Dictionary`).

## File to Create

`Gehtsoft.EF.Db.SqlDb.Sql.Test/SqlDslExecutionTests.cs`

Scaffold: same pattern as `SelectRun.cs` -- sealed class, IDisposable, SqlDbUniversalConnectionFactory with SQLite `:memory:`, Northwind Snapshot, SqlCodeDomBuilder built with "northwind" entities. Each `[Fact]` creates a fresh `SqlCodeDomEnvironment`.

## Test Groups

### Group 1: Constant Expression Evaluation via SET (8 tests)

Targets: `CalculateExpression` binary path (lines 40-48), `TryGetConstant` (lines 88-328), `SqlUnaryExpression.TryGetConstant`

Key insight: constant folding happens at parse time when both operands are literals. To exercise the *runtime* `CalculateExpression` binary path AND `TryGetConstant`, we need two-step SET operations: first SET stores a constant, second SET uses that variable in a binary expression (GlobalParameter + literal -> runtime evaluation).

| # | Test | SQL DSL (abbreviated) | Asserts | Lines Covered |
|---|------|----------------------|---------|---------------|
| 1 | `SetIntegerArithmetic` | `SET a=5; SET b=?a+3, c=?a-2, d=?a*4, e=?a/2; EXIT WITH ?b` (and verify c,d,e) | b=8, c=3, d=20, e=2 | TryGetConstant Int +/-/*/div (115-131) + CalculateExpression binary (40-48) |
| 2 | `SetIntegerComparisons` | `SET a=10; SET eq=?a=10, neq=?a<>5, gt=?a>5, ge=?a>=10, ls=?a<20, le=?a<=10; IF all THEN EXIT WITH 1` | 1 | TryGetConstant Int Eq/Neq/Gt/Ge/Ls/Le (90-113) |
| 3 | `SetDoubleArithmeticAndComparisons` | `SET a=3.14; SET b=?a+1.0, c=?a-1.0, d=?a*2.0, e=?a/2.0; SET eq=?a=3.14...` | b=4.14, all comparisons true | TryGetConstant Double all ops (134-181) |
| 4 | `SetMixedIntDoubleArithmetic` | `SET a=5; SET b=2.0; SET c=?a+?b, d=?a-?b, e=?a*?b, f=?a/?b; comparisons...` | c=7.0, all comparisons correct | TryGetConstant mixed Int/Double (270-327) |
| 5 | `SetBooleanOperations` | `SET a=TRUE, b=FALSE; SET eq=?a=TRUE, neq=?a<>?b, andOp=?a AND ?b, orOp=?a OR ?b` | eq=T, neq=T, andOp=F, orOp=T | TryGetConstant Boolean (186-203) |
| 6 | `SetStringOperationsAndConcat` | `SET a='hello', b='world'; SET c=?a \|\| ' ' \|\| ?b; SET eq=?a='hello', gt=?b>?a...` | c="hello world", all comps true | TryGetConstant String (234-266) |
| 7 | `SetDateTimeComparisons` | `SET a=DATETIME '2023-01-15 10:00:00', b=DATETIME '2023-06-20 10:00:00'; SET eq=?a=DATETIME '2023-01-15 10:00:00'...` | all 6 comparisons correct | TryGetConstant DateTime (205-232) |
| 8 | `SetUnaryExpressions` | `SET a=5; SET b=-?a, c=+?a; SET d=3.14; SET e=-?d; SET g=TRUE; SET h=NOT ?g` | b=-5, c=5, e=-3.14, h=FALSE | CalculateExpression unary (149-157) |

### Group 2: Built-in Functions in SET Context (9 tests)

Targets: `CalculateExpression` SqlCallFuncExpression (lines 158-279), `TryParseDateTime` (lines 337-357), `UnixTimeStampUTC` (lines 359-364)

| # | Test | SQL DSL | Asserts | Lines Covered |
|---|------|---------|---------|---------------|
| 9 | `SetTrimFunctions` | `SET a='  hello  '; SET b=TRIM(?a), c=LTRIM(?a), d=RTRIM(?a); EXIT WITH ?b\|\|'\|'\|\|?c\|\|'\|'\|\|?d` | "hello\|hello  \|  hello" | CalcExpr TRIM/LTRIM/RTRIM (181-189) |
| 10 | `SetUpperLower` | `SET a='Hello World'; SET b=UPPER(?a), c=LOWER(?a)` | "HELLO WORLD", "hello world" | CalcExpr UPPER/LOWER (193-199) |
| 11 | `SetToString` | `SET a=42; SET b=TOSTRING(?a)` | "42" | CalcExpr TOSTRING (201-203) |
| 12 | `SetTodouble` | `SET a='3.14'; SET b=TODOUBLE(?a); SET c=?b+1.0; EXIT WITH ?c` | 4.14 | CalcExpr TODOUBLE (213-219) |
| 13 | `SetTodate` | `SET a='2023-06-15'; SET b=TODATE(?a); SET c=DATETIME '2023-06-15 00:00:00'; SET eq=?b=?c` | eq=true | CalcExpr TODATE (221-228) + TryParseDateTime (337-357) |
| 14 | `SetTotimestamp` | `SET a='2023-06-15 12:00:00'; SET b=TOTIMESTAMP(?a); IF ?b > 0 THEN EXIT WITH ?b` | result > 0 | CalcExpr TOTIMESTAMP (229-236) + UnixTimeStampUTC (359-364) |
| 15 | `SetAbs` | `SET a=-42; SET b=ABS(?a); SET c=-3.14; SET d=ABS(?c)` | b=42, d=3.14 | CalcExpr ABS int+double (237-252) |
| 16 | `SetLikeAndNotlike` | `SET a='hello world'; SET b=?a LIKE 'hello%'; SET c=?a NOT LIKE 'foo%'` | b=true, c=true | CalcExpr LIKE/NOTLIKE (253-260) |
| 17 | `SetStartsWithEndsWithContains` | `SET a='hello world'; SET sw=STARTSWITH(?a,'hello')...` | All return **inverted** results (document bug #2) | CalcExpr STARTSWITH/ENDSWITH/CONTAINS (261-272) |

### Group 3: Advanced SELECT Features (12 tests)

Targets: `GetStrExpression` in `SqlStatementRunner<T>`, `SelectRunner.ProcessSelect`, `DiveTableSpecification`

| # | Test | SQL DSL | Asserts | Lines Covered |
|---|------|---------|---------|---------------|
| 18 | `SelectDistinct` | `SELECT DISTINCT Country FROM Customer` | Count < 91 (total customers), all unique | SelectRunner DISTINCT (103-104) |
| 19 | `SelectLeftJoin` | `SELECT Order.OrderID, Customer.CompanyName FROM Order LEFT JOIN Customer ON Order.Customer = Customer.CustomerID LIMIT 20` | Has rows, OrderID > 0 | DiveTableSpecification LEFT (266-267) |
| 20 | `SelectAvgSum` | `SELECT AVG(Freight) AS Avg, SUM(Freight) AS Total, COUNT(*) AS Cnt FROM Order` | All > 0, Total/Cnt ~ Avg | GetStrExpr AVG/SUM (474-478,483) |
| 21 | `SelectLtrimRtrimInQuery` | `SELECT LTRIM(' ' \|\| CompanyName) AS LT, RTRIM(CompanyName \|\| ' ') AS RT FROM Customer LIMIT 1` | LT and RT are trimmed | GetStrExpr LTRIM/RTRIM (681-684) |
| 22 | `SelectNotlikeInWhere` | Multi-statement: count all, count LIKE 'A%', count NOT LIKE 'A%', verify sum | all = like + notLike | GetStrExpr NOTLIKE+isNot (714-717,777-781) |
| 23 | `SelectUnaryNotInWhere` | `SELECT COUNT(*) AS Total FROM Customer WHERE NOT (Country = 'USA')` | Total = allCustomers - usaCustomers | GetStrExpr Not (643-644) |
| 24 | `SelectUnaryMinusInExpression` | `SELECT COUNT(*) AS Total FROM OrderDetail WHERE -Quantity < -100.0` | Same count as Quantity > 100 | GetStrExpr Minus (637-638) |
| 25 | `SelectUnaryPlusInExpression` | `SELECT +Freight AS PosFreight FROM Order LIMIT 1` | Has rows | GetStrExpr Plus (640-641) |
| 26 | `SelectTostringInQuery` | `SELECT TOSTRING(CategoryID) AS IdStr FROM Category LIMIT 1` | Result is string | GetStrExpr TOSTRING (693-694) |
| 27 | `SelectSubqueryInExpression` | `SELECT CategoryName, (SELECT COUNT(*) FROM Product WHERE Product.Category = Category.CategoryID) AS ProductCount FROM Category LIMIT 5` | Has CategoryName and ProductCount | GetStrExpr SqlSelectExpression (831-837) |
| 28 | `SelectWithMultipleParameters` | `SELECT * FROM Customer WHERE Country = ?country AND City LIKE ?pattern` with dict params | Filtered results match | GetStrExpr GlobalParameter dict (581-598) |
| 29 | `SelectOrderByAggregate` | `SELECT Country, SUM(Freight) AS TotalFreight FROM Order GROUP BY Country ORDER BY SUM(Freight) DESC LIMIT 5` | Results ordered descending | GetStrExpr aggregate in ORDER BY context |

### Group 4: Advanced DML (3 tests)

Targets: `UpdateRunner.ProcessUpdate` expression/subquery paths, `DeleteRunner` WHERE processing

| # | Test | SQL DSL | Asserts | Lines Covered |
|---|------|---------|---------|---------------|
| 30 | `UpdateWithFieldExpression` | INSERT test row, then `UPDATE Supplier SET ContactTitle = UPPER(ContactTitle), CompanyName = CompanyName \|\| ' Inc.' WHERE CompanyName = 'TestCo'` | Updated=1, verify values via SELECT | GetStrExpr SqlField alias (428-451), UpdateRunner expression (116-118) |
| 31 | `UpdateWithSubqueryInWhere` | INSERT test row, then `UPDATE Supplier SET ContactTitle = 'Director' WHERE SupplierID IN (SELECT SupplierID FROM Supplier WHERE CompanyName = 'TestUpd')` | Updated=1 | GetStrExpr IN+SELECT (813-819) in update context |
| 32 | `DeleteWithFieldExpressionWhere` | INSERT test row, then `DELETE FROM Supplier WHERE CompanyName LIKE 'TestDel%' AND Country = 'TestCountry'` | Deleted >= 1 | DeleteRunner complex WHERE |

### Group 5: Error Paths and Edge Cases (3 tests)

Targets: `CalculateExpression` error branches for GET_ROW/GET_FIELD

| # | Test | SQL DSL | Asserts | Lines Covered |
|---|------|---------|---------|---------------|
| 33 | `GetRowIndexOutOfRange` | `SELECT * FROM Category LIMIT 1; SET row = GET_ROW(LAST_RESULT(), -1)` | Throws SqlParserException "Index out of range" | CalcExpr (85-88) |
| 34 | `GetFieldTypeMismatch` | `SELECT CategoryName FROM Category LIMIT 1; SET row = GET_ROW(LAST_RESULT(), 0); SET val = GET_FIELD(?row, 'CategoryName', INTEGER)` | Throws SqlParserException "is not of type" | CalcExpr (115-118) |
| 35 | `GetFieldMissingField` | `SELECT CategoryName FROM Category LIMIT 1; SET row = GET_ROW(LAST_RESULT(), 0); SET val = GET_FIELD(?row, 'NonExistent', STRING)` | Throws SqlParserException "doesn't contain field" | CalcExpr (110-113) |

## Estimated Coverage Impact

| Area | Current Uncovered | Estimated Covered by Tests | Remaining |
|------|-------------------|---------------------------|-----------|
| TryGetConstant (all type ops) | 127 | ~115 | ~12 |
| CalculateExpression (functions, unary, binary) | 140 | ~90 | ~50 |
| GetStrExpression (queries) | 100 | ~50 | ~50 |
| SqlParser.Visitor (161 empty virtuals) | 161 | 0 (not coverable) | 161 |
| SqlParser.VisitASTNode | 166 | 0 (not coverable) | 166 |
| SqlExpressionParser.ParseExpression | 99 | ~30 (indirect) | ~69 |
| SqlAutoJoinedTable/SqlSelectStatement/SqlFunctionExpression ctors | 224 | ~30 (indirect) | ~194 |
| Runner GetQueryBuilder (factory path) | 63 | 0 (needs factory, not direct conn) | 63 |
| Other | ~952 | ~60 | ~892 |
| **Total** | **2032** | **~375-425** | **~1607-1657** |

Expected coverage: **61% -> ~68-70%**. The 161+166 lines of Visitor/VisitASTNode are dead infrastructure (virtual empty methods in generated parser) and are inherently uncoverable via high-level tests.

## Implementation Sequence

1. Create `SqlDslExecutionTests.cs` with class scaffold (constructor, Dispose, usings)
2. Implement Group 1 (tests 1-8) -- run and verify
3. Implement Group 2 (tests 9-17) -- run and verify
4. Implement Group 3 (tests 18-29) -- run and verify
5. Implement Group 4 (tests 30-32) -- run and verify
6. Implement Group 5 (tests 33-35) -- run and verify
7. Full test run + re-collect coverage to measure delta

## Verification

```bash
dotnet test Gehtsoft.EF.Db.SqlDb.Sql.Test/
dotnet-coverage collect "dotnet test Gehtsoft.EF.sln" -f xml -o coverage.xml
python3 .claude/skills/dotnet-coverage/scripts/coverage_report.py coverage.xml --mode summary --module SqlDb.Sql --namespace Gehtsoft.EF
```

## Key Files

| File | Role |
|------|------|
| `Gehtsoft.EF.Db.SqlDb.Sql.Test/SqlDslExecutionTests.cs` | **NEW** -- all 35 tests |
| `Gehtsoft.EF.Db.SqlDb.Sql.Test/SelectRun.cs` | Reference for test scaffold pattern |
| `Gehtsoft.EF.Db.SqlDb.Sql/StatementRunners.cs` | Primary coverage target (CalculateExpression + GetStrExpression) |
| `Gehtsoft.EF.Db.SqlDb.Sql/CodeDom/SqlBinaryExpression.cs` | Coverage target (TryGetConstant) |
| `Gehtsoft.EF.Db.SqlDb.Sql/SelectRunner.cs` | Coverage target (DISTINCT, JOIN types) |
| `Gehtsoft.EF.Db.SqlDb.Sql/UpdateRunner.cs` | Coverage target (expression/subquery updates) |
| `Gehtsoft.EF.Db.SqlDb.Sql/DeleteRunner.cs` | Coverage target (complex WHERE) |
