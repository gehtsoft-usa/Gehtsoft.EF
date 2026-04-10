# Known Bugs — Gehtsoft.EF.Db.SqlDb.Sql

## Open

### 1. SqlConstant does not override Equals/GetHashCode

**File:** `CodeDom/SqlConstant.cs`

`SqlConstant` uses default reference equality. Any code calling `.Equals()` on two `SqlConstant` instances with the same value will get `false`. The IN-list comparison in `StatementRunners.cs` was fixed to compare `.Value` directly, but other call sites may be affected.

## Fixed

### TODATE/TOTIMESTAMP in SQLite query context

Three issues fixed: (1) `SqliteLanguageSpecifics.GetSqlFunction` threw `FeatureNotSupported` — replaced with `(JULIANDAY(arg) - 2415018.5)` for TODATE (OLE double, respects StoreDateAsString) and `CAST((JULIANDAY(arg) - 2440587.5) * 86400 AS INTEGER)` for TOTIMESTAMP (unix seconds via Julian Day math, avoids STRFTIME '%s' string literal that triggers injection protection). (2) `SqlExpressionParser` declared TOTIMESTAMP as DateTime — fixed to Integer to match CalculateExpression behavior.

### NOT LIKE in WHERE clause generated invalid SQL

`GetStrExpression` NOTLIKE/isNot branch used `GetLogOp(LogOp.Not)` which returns `" NOT ("` (with opening paren) but never called `CloseLogOp` to add the closing `)`. Generated SQL had unbalanced parentheses. Fixed by adding `CloseLogOp(LogOp.Not)` call.

### Scalar subquery in CalculateExpression casts ExpandoObject to Dictionary

`StatementRunners.cs`, `CalculateExpression`, `SqlSelectExpression` branch — same pattern as the previously fixed IN-SELECT bug: `recordObj as Dictionary<string, object>` returns null because `SelectRunner.BindRecord` returns `ExpandoObject`. Fixed to `IDictionary<string, object>`.

### BREAK inside SWITCH exits enclosing loop

`BreakStatement.ToLinqWxpression()` and its constructor searched the compile-time block stack from bottom to top (`array.Length-1` downto `0`), but `Stack.ToArray()` returns top-first. This caused BREAK to find the outermost Loop instead of the innermost Switch, jumping to the Loop's EndLabel and exiting both. Same reversed iteration in `ContinueStatement`. Fixed by iterating from index 0 (top/innermost) in both `BreakStatement` and `ContinueStatement`.

### NOT IN not handled in CalculateExpression

`CalculateExpression` returned `inExpressionResult` without checking `inExpression.Operation`. `NOT IN` in SET/IF context produced the same result as `IN`. Fixed by negating the result when operation is `NotIn`.

### EXIT WITH inside IF block causes Stack empty

`ExitStatement.ToLinqWxpression()` used `BlockDescriptors.ToArray()[0]` which in a Stack gets the top (innermost block), not the root. EXIT WITH inside IF/WHILE jumped to the wrong end label, causing the root block descriptor to be popped prematurely. Fixed by using `array[array.Length - 1]` to target the root block.

### Unary minus before IN/LIKE expression causes parse failure

`IN_PREDICATE` and `LIKE_EXPR` grammar rules used `CONCAT_EXPR` as the left operand, which only accepts `SIMPLE_EXPR` — excluding arithmetic and unary expressions. `-999 IN (...)` failed to parse. Fixed in `sql.gram` by broadening the left operand: `IN_PREDICATE -> EXPR IN_OP^ ...` and `LIKE_EXPR -> COM_EXPR LIKE_OP^ ...`.

### IsCalculable for SqlCallFuncExpression uses OR instead of AND

`IsCalculable` returned `true` if *any* function parameter was calculable. Fixed to require *all* parameters to be calculable.

### TOINT grammar token matched against TOINTEGER

Three `case "TOINTEGER":` branches never matched because the grammar produces `"TOINT"`. Fixed to `case "TOINT":`.

### STARTSWITH/ENDSWITH/CONTAINS negated in CalculateExpression

Returned `!result` in the constant-eval path, producing inverted boolean values. Fixed by removing the negation.

### IN-list evaluates wrong operand + SqlConstant reference equality

Loop evaluated `inExpression.LeftOperand` instead of the loop variable `expr`, and used `SqlConstant.Equals` (reference equality). Fixed both: evaluate `expr` and compare `.Value`.

### IN-SELECT casts ExpandoObject to Dictionary

Cast `recordObj as Dictionary<string, object>` always returned null because `SelectRunner.BindRecord` returns `ExpandoObject`. Fixed to `IDictionary<string, object>`.

### IsCalculable missing SqlInExpression support

`IsCalculable` didn't handle `SqlInExpression`, making IN expressions unusable in SET/IF context despite `CalculateExpression` supporting them. Added handler.
