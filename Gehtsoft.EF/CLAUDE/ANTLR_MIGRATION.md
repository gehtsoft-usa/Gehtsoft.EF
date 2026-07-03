# Hime → ANTLR4 Migration Plan — Gehtsoft.EF SQL DSL

> Status: **planned, not started.** This document is intentionally detailed so execution can begin next session without re-analyzing the codebase. Line/method references were accurate as of the analysis (git `6c4bfa2`); re-verify a reference if the surrounding code has since moved.

## Context

Two projects in this solution generate their parsers with **Hime** (`Hime.SDK` 3.5.1 + `Hime.Build.Task` 0.1.6), compiling a `.gram` grammar into `*Lexer.cs/.bin` + `*Parser.cs/.bin` at build time:

- **`Gehtsoft.EF.Db.SqlDb.Sql`** — the shipped SQL-scripting DSL (SELECT/INSERT/UPDATE/DELETE + control flow SET/DECLARE/IMPORT/EXIT/IF/WHILE/FOR/SWITCH/cursors). ~7,400 lines of tree-walking code build a LINQ-expression Code DOM from the Hime AST.
- **`Gehtsoft.EF.Test`** — has its **own separate** Hime grammar (`SqlTest`, DML + DDL + `UNION` + a `DEBUG` helper) used only to assert the *structure* of generated SQL in ~184 test call-sites.

Goal: **remove Hime from the entire solution**, replacing it with **ANTLR4** (`Antlr4.Runtime.Standard` + `Antlr4BuildTasks` build-time codegen). Motivation: consolidate on a mainstream, actively maintained parser toolchain and drop the niche Hime dependency.

### Decisions (locked with the user)

1. **Codegen:** `Antlr4BuildTasks` (auto-regenerate from `.g4` on build). ⚠️ **New build prerequisite: a Java runtime on every build machine and CI agent** (the ANTLR tool is a jar). Flag this for CI before starting.
2. **Scope:** solution-wide — migrate **both** grammars so Hime can be fully removed.
3. **Approach — differs per project:**
   - **`.Sql` package → native ANTLR rewrite.** Consumers read ANTLR typed contexts directly; delete the Hime `ASTNode` index-navigation. Rationale: sheds the fragile `disp`/`Children[i]` arithmetic; the 137 unit tests + golden corpus are the safety net.
   - **`EF.Test` → wrapper (shim) over the existing `IAstNode` seam.** This project already abstracts the tree behind `IAstNode`; only 2 files are truly Hime-coupled. Reproduce the tree shape so the ~184 call-sites and assertion helpers stay unchanged. Native rewrite here would be pure churn.

---

## Core technical challenge

Hime grammars use **tree-rewriting operators** that ANTLR has no equivalent for:
- `!` — **drop** the token from the tree (keywords/punctuation never appear as children).
- `^` — **promote** the child up, replacing its parent's symbol.

This produces a *compact* AST. Two consequences the rewrite must handle:

1. **Operator-rooted expression tree.** Rules like `ADD_EXPR -> ADD_EXPR PLUS_OP^ MUL_EXPR` promote the **operator**, so a binary node's `Symbol.ID` *is* the operator (`VariablePlusOp`) with operands at `Children[0]`/`Children[1]`. ANTLR instead yields left-recursive **labeled alternatives** (`# AddExpr`) whose context holds `expr op expr`. Dispatch must move from "operator symbol" → "context alternative type + operator-token check".

2. **Index arithmetic depends on dropped/optional nodes.** Positional `Children[i]` reads assume keywords are gone and optional elements are absent. In ANTLR every token is present; use **typed accessors** (`ctx.IDENTIFIER()`, `ctx.expr()`, `ctx.joinType()`) instead of indices.

Hime naming convention (for mapping): rule `FOO_BAR` → `SqlParser.ID.VariableFooBar`; terminal `INT` → `SqlLexer.ID.TerminalInt`.

---

## Current architecture (entry points)

- **`SqlCodeDomBuilder.ParseToRawTree(name, TextReader)`** — the only parse entry: `new SqlParser(new SqlLexer(source)).Parse()` → `ParseResult`; on `!IsSuccess` → `SqlErrorCollection.ToSqlErrors`; returns `result.Root` (a `ROOT` node). Also called recursively via `ParseNodeToLinq` for IF/WHILE/FOR/SWITCH bodies.
- **`SqlErrorCollection.ToSqlErrors(source, ParseResult)`** — maps `r.Errors` (`e.Position.Line/.Column`, `e.Message`) → `SqlError`.
- **`SqlAstVisitor`** — statement dispatch → Code DOM classes.
- **`CodeDom/*.cs`** (~50) — one class per grammar construct; constructors take `ASTNode` and walk it.
- **Runners** (`SelectRunner`, `InsertRunner`, `UpdateRunner`, `DeleteRunner`, `StatementRunners`) — consume **Code DOM objects, not the AST**. ✅ **No AST changes needed** (they do re-dispatch on captured function-name strings — see token contract).

---

## Token-text contract (the ANTLR lexer MUST reproduce these exactly)

The walker dispatches mostly on `Symbol.ID`, but a specific set of nodes is branched on by **token text** (`.Value`), case-sensitively, with **no normalization**. The ANTLR lexer must emit identical text.

**Function names** (captured via `Children[0].Value`, later re-switched in `StatementRunners.cs:179` & `:678`):
- Math: `ABS`
- Cast: `TOSTRING`, `TOINT`, `TODOUBLE`, `TODATE`, `TOTIMESTAMP`
- String: `TRIM`, `LTRIM`, `RTRIM`, `UPPER`, `LOWER`
- Bool-string: `CONTAINS`, `ENDSWITH`, `STARTSWITH`
- Aggregates: `COUNT`, `MAX`, `MIN`, `AVG`, `SUM`; compound `COUNT(*)` → name `COUNT`
- (`LIKE`/`NOTLIKE`, `TRIM`/`LTRIM`/`RTRIM` refinement injected by ID, not text.)

**Type names** (`Statement.cs:291-311 GetResultTypeByName`): `STRING`, `INTEGER`, `DOUBLE`, `BOOLEAN`, `DATETIME`, `ROW`, `ROWSET` (else → Unknown). Callers: `DeclareStatement`, `ImportStatement`, `GlobalParameter`, `GetField`.

**Keywords compared as text:** `DESC` (`SqlSortSpecification.cs:19`, else Asc); `DISTINCT` (`SelectRunner.cs:103`, else incl. ALL ignored); join types `INNER`/`LEFT`/`RIGHT`/`FULL` (`SelectRunner.cs:261`); sentinel `NULL`; booleans consumed by ID but text `TRUE`/`FALSE`.

**Operators:** matched by `Symbol.ID` only — text not string-compared. They just need to map to the right ANTLR alternative/token.

**Token text-shape requirements:**
- `INT`: digits only → `int.Parse`.
- `REAL`: dot decimal. ⚠️ Parsed with **default/current culture** (`SqlExpressionParser.cs:45`, `SqlBinaryExpression.cs:277`) — token must use `.` decimal; preserve behavior (do not "fix" to InvariantCulture without discussion).
- `STRINGDQ`/`STRINGSQ`: **token text includes both delimiter quotes**; inner body delivered **raw** — the walker only does `Substring(1, len-2)` and performs **NO backslash unescaping** anywhere. The ANTLR lexer must NOT unescape `\n`, `\\`, etc.; deliver the full matched lexeme verbatim.
- `DATE`/`DATETIME` constant bodies: quoted string parsed InvariantCulture with `yyyy-MM-dd[ HH[:mm[:ss]]]`.
- `GLOBAL_PARAMETER_NAME`: **keeps leading `?`** (stripped later at `StatementRunners.cs:591`).

`.Symbol.Name` (24 uses) appears **only in error-message interpolation** — never drives logic. ANTLR need only match it for readable diagnostics, not correctness.

---

## Part A — `.Sql` package rewrite

### A1. `Sql.g4` grammar
Author idiomatically (consumers are rewritten, so tree shape need not mimic Hime):
- **Lexer:** `INT [0-9]+`; `REAL` (`[0-9]+ '.' [0-9]*` | `'.' [0-9]+`); `STRINGDQ`/`STRINGSQ` matching the same escape *classes* but with **no lexer unescaping** and quotes included in the token; `IDENTIFIER [a-zA-Z_][a-zA-Z0-9_]*`; `GLOBAL_PARAMETER_NAME '?' IDENTIFIER`; `ASSIGN ':='`.
- **Keyword tokens declared before `IDENTIFIER`** (ANTLR longest-match, ties by declaration order). Special compound/space-containing tokens: `ORDER BY`, `GROUP BY` (embedded space → explicit rule `ORDER_BY: 'ORDER' [ \t]+ 'BY';`), and `COUNT(*)` → single `AGGR_COUNT_ALL` token.
- Whitespace + `--` line comments → `-> skip`.
- **Expressions:** single left-recursive `expr` rule with **labeled alternatives ordered by precedence** (unary ± / NOT, `*` `/`, `+` `-` `||`, relational, LIKE/IN/IS NULL, AND, OR) and labeled operator tokens. Mirror the current precedence layering (`UNARY→MUL→ADD→REL→...→OR`). Note `CONCAT_EXPR` currently restricts operands to `SIMPLE_EXPR` — preserve equivalent behavior.
- Keep statement rules with labeled optional sub-elements so the walker uses `ctx.setQuantifier()`, `ctx.limitOffset()`, `ctx.orderBy()`, `ctx.groupBy()`, `ctx.joinType()` instead of index math.
- Grammar case-sensitive (all keywords uppercase, as today). Resolve any ANTLR ambiguity warnings; verify associativity/precedence against the Phase-0 golden trees.

### A2. Parse entry + errors
- Rewrite `SqlCodeDomBuilder.ParseToRawTree`: build `SqlLexer(CharStreams.fromString/…)` → `CommonTokenStream` → `SqlParser`; `parser.RemoveErrorListeners()`; attach new **`SqlErrorListener : BaseErrorListener`** collecting into `SqlErrorCollection`; return the root context. Change the return type from `Hime.Redist.ASTNode` to the ANTLR root context type (ripples into `ParseNodeToLinq` / `SqlAstVisitor`).
- Rewrite `SqlErrorCollection.ToSqlErrors` to consume ANTLR error data (`line`, `charPositionInLine`, message). Hime returned multiple errors via RNGLR recovery — keep ANTLR's default recovery (collect multiple) rather than `BailErrorStrategy`.

### A3. Consumer rewrite — per-file inventory
Each class currently takes `ASTNode` and navigates by index/`Symbol.ID`. Rewrite each to take its **typed context** and use accessors. `.Position.Line/.Column` → `ctx.Start.Line`/`ctx.Start.Column`; `.Value` → `ctx.GetText()` or a specific terminal's text; `.Symbol.Name` → rule name (error text only).

**Dispatch:**
- `SqlAstVisitor.VisitStatement` — currently switches `Children[0].Symbol.ID` over 21 statement kinds (Select/Insert/Update/Delete/Set/Declare/Import/Exit/Ifthen/Continue/Break/Whiledo/Fordo/Switch/AddFieldTo/AddRowTo/DeclareCursor/OpenCursor/CloseCursor/AssignExpr/Nop). Re-express as dispatch on statement context type. `Nop` → null. (Child-count guards `<2`/`<3` become "context has the required sub-nodes".)
- `SqlExpressionParser.ParseExpression` — **the hardest file.** Recursive dispatch currently keyed on operator/leaf `Symbol.ID` with operands at `Children[0/1/2]`. Re-express against labeled `expr` alternative contexts + operator-token checks. Notable cases: **MINUS_OP/PLUS_OP binary-vs-unary** (`Children.Count>1`) → separate unary/binary labeled alternatives; **TRIM_CALL** optional `TRIM_SPECIFICATION` shifts param index (0 vs 1) → typed `trimSpecification()` accessor; func-name capture (`Children[0].Value`) → keyword token text; string/date/datetime leaf parsing per the token contract; post-switch tail builds binary/unary/in expressions from operand contexts.

**Statement classes** (`CodeDom/`, ctor `(builder, ASTNode, source)` unless noted):

| File | Rule | Key navigation to replace |
|---|---|---|
| `SqlSelectStatement.cs` | `SELECT` | `disp` offset for optional `SET_QUANTIFIER`; `TABLE_EXPRESSION` FROM=`[0]`, optional WHERE=`[1]`; **variable-order** trailing loop over `LimitOffset`/`SortSpecificationList`/`GroupSpecificationList` → use typed accessors. |
| `SqlInsertStatement.cs` | `INSERT` | `[0]`=IDENTIFIER, `[1]`=FIELDS_LIST, `[2]`=VALUES vs SELECT (reject GROUP/ORDER). |
| `SqlUpdateStatement.cs` | `UPDATE` | `[0]`=IDENTIFIER, `[1]`=UPDATE_LIST, optional `[2]`=WHERE (boolean + no-aggregate checks). |
| `SqlDeleteStatement.cs` | `DELETE` | `[0]`=IDENTIFIER, optional `[1]`=WHERE. |
| `SqlUpdateAssign.cs` | `UPDATE_ASSIGN` | `[0]`=FIELD, `[1]`=EXPR or SELECT (1-col). |
| `SetStatement.cs` | `SET` | iterate SET_LIST; per item `[0]`=name, optional `[1]`=EXPR. |
| `DeclareStatement.cs` / `ImportStatement.cs` | `DECLARE`/`IMPORT` | iterate DECLARE_LIST; item `[0]`=name (`"?"+`), `[1]`=type. |
| `ExitStatement.cs` | `EXIT` | optional `[0]`=EXPR (grammar `*` but only `[0]` read). |
| `IfStatement.cs` | `IFTHEN` | iterate children; `VariableRoot`=body vs condition; stateful ELSE→implicit true. |
| `WhileDoStatement.cs` | `WHILEDO` | `[0]`=cond, `[1]`=ROOT body. |
| `ForDoStatement.cs` | `FORDO` | fixed `[0]`init `[1]`cond `[2]`next `[3]`body ROOTs. |
| `SwitchStatement.cs` | `SWITCH` | `[0]`=operand; loop `VariableRoot` vs CASE EXPR; stateful pairing; empty `CASE x:` handled. |
| `AssignStatement.cs` | `ASSIGN_EXPR` | `[0]`=param, `[1]`=EXPR. |
| `AddFieldStatement.cs` | `ADD_FIELD_TO` | `[0]`name(String) `[1]`value `[2]`target Row. |
| `AddRowStatement.cs` | `ADD_ROW_TO` | `[0]`value(Row) `[1]`target RowSet. |
| `DeclareCursorStatement.cs` | `DECLARE_CURSOR` | `[0]`=name(`"?"+`), `[1]`=SELECT. |
| `OpenCursorStatement.cs` / `CloseCursorStatement.cs` | `OPEN/CLOSE_CURSOR` | `[0]`=param (Cursor). |
| `BreakStatement.cs` / `ContinueStatement.cs` | `BREAK`/`CONTINUE` | only `.Position` → `ctx.Start`. |
| `BlockStatement.cs` | (generic ROOT wrapper) | AST ctor `[0]`→ParseNodeToLinq (appears unused; verify then port or drop). |

**Clause/table classes** (ctor takes `SqlStatement`/`Statement` + `ASTNode`):

| File | Rule | Key navigation |
|---|---|---|
| `SqlFromClause.cs` | `FROM_CLAUSE` | iterate TABLE_REFERENCE_LIST; dispatch TablePrimary/QualifiedJoin/AutoJoin. |
| `SqlWhereClause.cs` | `WHERE_CLAUSE` | `[0]`=BOOL_EXPR. |
| `SqlPrimaryTable.cs` | `TABLE_PRIMARY` | `[0]`=name, optional `[1]`=correlation. |
| `SqlAutoJoinedTable.cs` | `AUTO_JOIN` | `[0]`=left ref, `[1]`=right TablePrimary. |
| `SqlQualifiedJoinedTable.cs` | `QUALIFIED_JOIN` | ⚠️ **HIGHEST RISK:** fixed `[0..3]` assumes `JOIN_TYPE` present, but grammar makes it optional → use typed `joinType()` accessor. `JoinType=node2.Value`; JOIN_SPECIFICATION→JOIN_CONDITION→BOOL_EXPR deferred parse in `TryExpression()` (stored `ASTNode? mExpressionNode` → store the context instead). |
| `SqlSelectList.cs` | `SELECT_LIST` | `[0]`=`*` (Asterisk) vs SELECT_SUBLIST iterate. |
| `SqlFieldAlias.cs` (`SqlExpressionAlias`) | `EXPR_ALIAS` | `[0]`=EXPR, optional `[1]`=alias; sets `IgnoreAlias`. |
| `SqlSortSpecification.cs` | `SORT_SPECIFICATION` | `[0]`=EXPR, optional `[1]`=`DESC`/`ASC`. |
| `SqlGroupSpecification.cs` | `GROUP_SPECIFICATION` | `[0]`=EXPR. |
| `SqlSelectExpression.cs` | `SELECT_EXPR` | `[0]`=SELECT (1-col check). |

**Expression leaf classes:**

| File | Rule | Key navigation |
|---|---|---|
| `SqlField.cs` | `FIELD` | optional prefix: `Count>1` → `[0]`prefix `[1]`name, else `[0]`name. |
| `GlobalParameter.cs` | `GLOBAL_PARAMETER(_SIMPLE)` | `[0]`=name(incl `?`), optional `[1]`=type. |
| `AssignExpression.cs` | `ASSIGN_EXPR` operands | LHS→GlobalParameter, RHS→expr. |
| `GetField.cs` | `GET_FIELD_CALL` | `[0]`row `[1]`name(String) `[2]`BASE_TYPE. |
| `GetRow.cs` | `GET_ROW_CALL` | `[0]`rowset `[1]`index(Integer). |
| `GetRowsCount.cs` | `ROWS_COUNT_CALL` | `[0]`rowset. |
| `Fetch.cs` | `FETCH_CALL` | `[0]`cursor param. |
| `SqlInExpression.cs` | `IN_PREDICATE` | LHS + RHS dispatch: `IN_VALUE_ARGS` iterate vs `SELECT` (1-col). |

**Cleanup (no walker, import only):** `SqlAggrFunc.cs`, `SqlCallFuncExpression.cs`, `SqlConstantCollection.cs`, `SqlFieldCollection.cs` — drop `using Hime.Redist;`. **Dead AST ctors — delete rather than port:** `SqlBinaryExpression(…, ASTNode, …)` and `SqlUnaryExpression(…, ASTNode, …)` (all call sites use the `SqlBaseExpression` ctor).

### A4. `.csproj`
Replace `Hime.SDK`/`Hime.Build.Task` refs + the `CompileGrammar`/`AddGrammarFiles`/`CleanGrammar` targets + `.bin` `EmbeddedResource` wiring with `Antlr4.Runtime.Standard` PackageReference + `Antlr4BuildTasks` and `<Antlr4 Include="Sql.g4">` (Listener off, Visitor on as needed). Target stays `netstandard2.0` (both ANTLR packages support it). Delete `sql.gram`.

---

## Part B — `EF.Test` migration (wrapper over `IAstNode`)

The tree is already abstracted behind **`IAstNode`** (`Symbol`, `Value`, `Children`); all ~184 `.ParseSql()` call-sites and every assertion helper (`Utils/SqlParser/*`, `SqlDb/SqlQueryBuilder/Utils/AstNode*Extensions.cs`) talk only to `IAstNode`. Real Hime coupling = **2 files**. Generated types are `SqlTestParser`/`SqlTestLexer` (namespace `Gehtsoft.eF.Test.SqlParser`); grammar options `ParserType=RNGLR`.

1. **`Gehtsoft.EF.Test/sql.gram` → `SqlTest.g4`** — port the `SqlTest` grammar (DML + DDL `CREATE/ALTER/DROP` + `UNION [ALL]` + `HAVING` + the test-only `DEBUG_EXPR -> 'DEBUG'! EXPR`, reached via `.DebugExpr()`; tests wrap raw expressions as `"DEBUG " + expr`). ⚠️ Here the tree shape **matters**: `AstNodeCommonExtensions.cs` / `AstNodeSelectExtensions.cs` hard-code grammar **symbol names** (`FIELD`, `EQ_OP`, `SELECT_LIST/SELECT_SUBLIST/EXPR_ALIAS`, `JOIN_TYPE`, `LIMIT_OFFSET/OFFSET`, …) and nesting/paths. Author the `.g4` with **matching rule names** and have the wrapper collapse promoted (`^`)/dropped (`!`) tokens as Hime did, so those helpers stay unchanged.
2. **`Utils/SqlParser/AstNodeWrapper.cs`** — rewrite to wrap ANTLR `IParseTree`: rule context → `Symbol` = rule name (uppercased to match expected symbol names), `Children` from context children; terminal → `Symbol`/`Value` (`ITerminalNode`). Reproduce the Hime symbol paths. (Also drop the stray `using Microsoft.OData.Edm;`.)
3. **`Utils/SqlParser/SqlParserExtensions.cs`** — rewrite the single `ParseSql()` method: ANTLR lexer → `CommonTokenStream` → `SqlTestParser` → root; custom `BaseErrorListener`; throw the same `ArgumentException` on failure; return the new wrapper. Keep generated type names `SqlTestParser`/`SqlTestLexer` (or update this one construction site).
4. **Delete 4 dead `using Hime.*` lines** (otherwise they break the build once packages are gone): `Utils/SqlParser/AstNodeExtensions.cs`, `SqlDb/SqlQueryBuilder/Utils/AstNodeAssertionsExtensions.cs` (`using Hime.SDK.Grammars;`), `Entity/Query/ConditionBuilder.cs` (`using Hime.SDK.Grammars.LR;`), `Entity/Query/QueryiesOnDb_Infrastructure.cs` (`using Hime.SDK.Output;`).
5. **`.csproj`** — swap Hime lines (approx. 64-83) for ANTLR runtime + `<Antlr4 Include="SqlTest.g4">`; drop `.bin` resources. Delete `sql.gram`.

**Untouched** (already `IAstNode`-only): `IAstNode.cs`, `IAstNodeChildren.cs`, `AstNodeImpl.cs`, `AstNodeExtensions.cs` (minus using), `SqlParserTest.cs`, `XmlNodeAssertionsExtensions.cs` (defines `AstNodeAssertions`), all three `AstNode*Extensions.cs` in SqlQueryBuilder/Utils, and the 184 call-sites — **iff** grammar symbol names/shape are preserved.

**Call-site distribution** (all via `SqlParserExtensions.ParseSql`, 184 total): `SqlDb/SqlQueryBuilder/Select.cs` (33), `Entity/Query/ConditionBuilder.cs` (29), `Entity/Query/SelectQueries.cs` (28), `SqlDb/SqlQueryBuilder/ConditionBuilder.cs` (28), `SqlDb/SqlQueryBuilder/CreateTable.cs` (16), `AlterTable.cs` (14), `UpdateQueries.cs` (13), `Entity/OData/ModelBuilder.cs` (7), `Entity/Query/UpdateQueries.cs` (7), `CreateIndex.cs` (6), `Drop.cs` (3).

---

## Execution phases

- **Phase 0 — Baseline & spike.** Capture a **golden corpus**: run representative scripts (from the 137 `.Sql` tests + `doc/src/esql|sqlcmd|sqlgen`) through the current Hime build; snapshot parse-tree dumps and execution results as the behavioral oracle (no shim-diff safety net exists for a native rewrite). Stand up `Antlr4.Runtime.Standard` + `Antlr4BuildTasks` on a branch; confirm codegen+build on dev **and CI (Java present)**. Port a `SELECT … FROM … WHERE` vertical slice end-to-end to lock conventions.
- **Phase 1 — `Sql.g4`** (A1). Resolve ambiguity warnings; verify precedence vs golden trees.
- **Phase 2 — Parse entry + errors** (A2).
- **Phase 3 — Consumer rewrite** (A3): `SqlExpressionParser` and `SqlQualifiedJoinedTable` first (highest risk), then statements, clauses, leaves; cleanup dead ctors/usings.
- **Phase 4 — `EF.Test`** (Part B).
- **Phase 5 — Remove Hime & finalize.** Delete both `.gram` files, Hime package refs, MSBuild targets, `.bin` wiring from both `.csproj`. Update `CLAUDE.md`.

---

## Verification

- Run the full **`Gehtsoft.EF.Db.SqlDb.Sql.Test`** suite (137 tests) — all green.
- Run **`Gehtsoft.EF.Test`** — the 184 `.ParseSql()` structural assertions green (breakage → tree-shape mismatch in `.g4`/wrapper, fix there, not in tests) + `SqlParserTest.cs` self-tests.
- **Diff against the Phase-0 golden corpus** (parse + execution results) to catch behavioral drift the unit tests don't cover.
- Confirm `dotnet build` succeeds with **no Hime reference anywhere** (`grep -ri hime` clean outside git history) and the ANTLR toolchain runs on CI.
- Sanity-check the token contract: string literals kept raw (no unescaping), `?`-prefixed params, `REAL` dot-decimal, DATE/DATETIME formats, function/type/keyword text.

## Risk hotspots (ordered)

1. `SqlQualifiedJoinedTable` — optional `JOIN_TYPE` breaks fixed `[0..3]` indexing (use typed accessor).
2. `SqlExpressionParser` — operator-rooted → labeled-alternative dispatch; MINUS/PLUS binary-vs-unary; TRIM optional-spec index shift.
3. `SqlSelectStatement` — `disp` offset + variable-order trailing clauses.
4. `EF.Test` grammar — symbol-name/shape parity so 184 call-sites + assertion helpers stay unchanged.
5. Expression precedence/associativity equivalence (ANTLR ALL(*) deterministic vs Hime RNGLR).
6. Token text fidelity (raw strings, culture-sensitive `REAL`, `?` params).
7. Build/CI Java prerequisite for `Antlr4BuildTasks`.
8. **Error-column base:** Hime `Position.Column` is **1-based**; ANTLR `charPositionInLine` is **0-based**. `SqlError`/`SqlErrorListener` must `+1` to preserve reported columns (check any test asserting error positions).

---
---

# Reference appendices (researched data — avoid re-analysis)

## Appendix A — Hime→ANTLR API cheat-sheet

| Hime (`Hime.Redist`) | ANTLR4 (`Antlr4.Runtime`) |
|---|---|
| `new SqlParser(new SqlLexer(textReader))` | `new SqlParser(new CommonTokenStream(new SqlLexer(new AntlrInputStream(textReader))))` |
| `parser.Parse()` → `ParseResult` | `parser.root()` → `RootContext` (entry rule = grammar axiom) |
| `result.IsSuccess` / `result.Root` | no success flag — root context always returned; errors via listener |
| `result.Errors` (`ParseError`: `.Position.Line/.Column`, `.Message`) | `parser.RemoveErrorListeners(); parser.AddErrorListener(new SqlErrorListener())` where listener overrides `SyntaxError(recognizer, offendingSymbol, line, charPositionInLine, msg, e)` |
| `node.Symbol.ID == SqlParser.ID.VariableXxx` | `ctx is SqlParser.XxxContext` (labeled alt: `is SqlParser.XxxLabelContext`) |
| `node.Symbol.ID == SqlLexer.ID.TerminalInt` | `node is ITerminalNode t && t.Symbol.Type == SqlLexer.INT` |
| `node.Symbol.Name` (error text only) | `SqlParser.ruleNames[ctx.RuleIndex]` or `ctx.GetType().Name` |
| `node.Value` (terminal text) | `terminalNode.GetText()` / `token.Text` |
| `node.Value` (promoted operator, e.g. `VariableMulOp`) | the operator token accessor on the labeled alt context |
| `node.Children[i]` | typed accessors: `ctx.expr(i)`, `ctx.IDENTIFIER()`, `ctx.setQuantifier()`, or `ctx.GetChild(i)` |
| `node.Children.Count` | `ctx.ChildCount`, or `ctx.expr().Count` for a specific repeated sub-rule |
| `node.Position.Line` | `ctx.Start.Line` (1-based, same as Hime) |
| `node.Position.Column` | `ctx.Start.Column` (**0-based** — add 1 to match Hime) |

Codegen: `Antlr4BuildTasks` + `Antlr4.Runtime.Standard` (both support netstandard2.0). `.csproj`: `<Antlr4 Include="Sql.g4"><Listener>false</Listener><Visitor>true</Visitor></Antlr4>`. Generated types land in the namespace set by the `@header`/`-package`; keep names `SqlParser`/`SqlLexer` (and `SqlTestParser`/`SqlTestLexer`) to minimize call-site churn.

## Appendix B — `.Sql` expression precedence chain (from `sql.gram`)

Layered, all `^`-promoted; **operator token is promoted** so the operator becomes the node symbol. Reproduce this precedence in the ANTLR left-recursive `expr` rule (lowest→highest binding is bottom-up here):

```
OR_BOOL_EXPR  -> AND_BOOL_EXPR (OR_OP  AND_BOOL_EXPR)*        // OR   (lowest)
AND_BOOL_EXPR -> UX_BOOL_EXPR  (AND_OP UX_BOOL_EXPR)*         // AND
UX_BOOL_EXPR  -> [NOT_OP] COMPARE_EXPR                        // NOT (unary prefix)
COMPARE_EXPR  -> REL_EXPR | LIKE_EXPR | IN_PREDICATE | NULL_PREDICATE
REL_EXPR      -> ADD_EXPR ((EQ|NEQ|GT|GE|LT|LE)_OP COM_EXPR)* // relational
ADD_EXPR      -> MUL_EXPR ((PLUS|MINUS|CONCAT)_OP MUL_EXPR)*  // + - ||
MUL_EXPR      -> UNARY_EXPR ((MUL|DIV)_OP UNARY_EXPR)*        // * /
UNARY_EXPR    -> [MINUS_OP|PLUS_OP] SIMPLE_EXPR              // unary ± (highest)
SIMPLE_EXPR   -> FIELD | GLOBAL_PARAMETER | FUNC_CALL | BRACKET_EXPR | CONSTANT | SELECT_EXPR | AGGR_CALL
```
Notes: `LIKE_EXPR -> COM_EXPR LIKE_OP CONCAT_EXPR`; `IN_PREDICATE -> EXPR IN_OP IN_PREDICATE_VALUE`; `NULL_PREDICATE -> EXPR NULL_OP`. `CONCAT_EXPR` restricts operands to `SIMPLE_EXPR`. `EXPR -> OR_BOOL_EXPR | ASSIGN_EXPR`. Unary vs binary `±` currently disambiguated by child count — split into distinct labeled alternatives in ANTLR.

## Appendix C — `SqlExpressionParser.ParseExpression` full dispatch (the hardest rewrite)

Recursive; currently `switch (fieldNode.Symbol.ID)`. Each row: current symbol → what it builds → operand navigation. Rewrite as dispatch on labeled `expr` alt context + typed operand accessors.

| Symbol.ID | Builds | Operands / notes |
|---|---|---|
| `VariableSelectExpr` | `SqlSelectExpression(node)` | `node.Children[0]` = SELECT |
| `VariableField` | `SqlField(node)` | — |
| `VariableNull` | `SqlConstant(null, Unknown)` | sentinel `"NULL"` → null |
| `TerminalInt` | `SqlConstant(int.Parse(Value), Integer)` | digits only |
| `TerminalReal` | `SqlConstant(double.Parse(Value), Double)` | ⚠ default culture, dot decimal |
| `TerminalStringdq/Stringsq` | `SqlConstant(Value.Substring(1,len-2), String)` | quotes stripped, body raw |
| `VariableBooleanTrue/False` | `SqlConstant(true/false, Boolean)` | — |
| `VariableDateConst` | `SqlConstant(DateTime, DateTime)` | `Children[0].Value` strip-quotes, `yyyy-MM-dd` InvariantCulture |
| `VariableDatetimeConst` | `SqlConstant(DateTime, DateTime)` | 4 formats: `yyyy-MM-dd[ HH[:mm[:ss]]]` |
| `VariableAnd/Or/Ge/Gt/Le/Lt/Eq/Neq/Concat/Mul/DivOp` | `SqlBinaryExpression` (const-folds via `TryGetConstant`) | operands `Children[0]`,`Children[1]` |
| `VariableMinusOp` | **binary Minus if `Children.Count>1` else unary Minus** | — |
| `VariablePlusOp` | **binary Plus if `Children.Count>1` else unary Plus** | — |
| `VariableNotOp` | unary Not | operand `Children[0]` |
| `VariableTrimCall` | `SqlCallFuncExpression("TRIM"/"LTRIM"/"RTRIM", String)` | default param `Children[0]`; if `Count>1`: `Children[0]`=spec (`VariableTrimLeading`→LTRIM, `VariableTrimTrailing`→RTRIM), param=`Children[1]`; param must be String |
| `VariableStrFuncCall` | `SqlCallFuncExpression(Children[0].Value, String)` | param `Children[1]` (String) |
| `VariableCastFuncCall` | func = `Children[0].Value`; result type per name (TOSTRING→String, TOINT→Integer, TODOUBLE→Double, TODATE→DateTime, TOTIMESTAMP→Integer) | param `Children[1]` |
| `VariableMathFuncCall` | func = `Children[0].Value` (ABS); result = param type | param `Children[1]` (Integer/Double) |
| `VariableAggrFunc` | `SqlAggrFunc(Children[0].Value, SqlField(Children[1]), COUNT?Integer:null)` | — |
| `VariableAggrCountAll` | `SqlAggrFunc("COUNT", null, Integer)` | — |
| `VariableExactLikeOp/NotLikeOp` | `SqlCallFuncExpression("LIKE"/"NOTLIKE", Boolean)` | `Children[0]`,`Children[1]` both String |
| `VariableBoolStrFuncCall` | `SqlCallFuncExpression(Children[0].Value, Boolean)` | params `Children[1]`,`Children[2]` (String) — **3 children** |
| `VariableExactInOp/NotInOp` | `SqlInExpression(Children[0], In/NotIn, Children[1])` | — |
| `VariableExactNullOp/NotNullOp` | unary IsNull/IsNotNull | operand `Children[0]` |
| `VariableGlobalParameter(Simple)` | `GlobalParameter(node)` | — |
| `VariableLastResultCall` | `GetLastResult()` | — |
| `VariableRowsCountCall` | `GetRowsCount(node)` | — |
| `VariableGetRowCall` | `GetRow(node)` | — |
| `VariableGetFieldCall` | `GetField(node)` | — |
| `VariableNewRowsetCall/NewRowCall` | `NewRowSet()` / `NewRow()` | — |
| `VariableFetchCall` | `Fetch(node)` | — |
| `VariableAssignExpr` | `AssignExpression(Children[0], Children[1])` | LHS param, RHS expr |

## Appendix D — `EF.Test` tree-shape contract (symbol names & paths the wrapper MUST reproduce)

The `EF.Test` migration keeps `AstNode*Extensions.cs` unchanged, so the ANTLR tree (as exposed via the new `AstNodeWrapper`) must yield **these exact `IAstNode.Symbol` names and path structures**. Paths use the helper mini-XPath (`/child`, `//descendant`, `symbol(value)`, index is **1-based** in `SelectNode`).

**Symbol names asserted** (`AstNodeCommonExtensions.cs`):
`FIELD`, `IDENTIFIER`, `PARAM`, `BOOLEAN_TRUE`, `BOOLEAN_FALSE`, `INT`, `REAL`, `STRINGDQ`, `STRINGSQ`, `NULL`, `AGGR_COUNT_ALL`, `AGGR_FUNC`, `MATH_FUNC_CALL`, `BOOL_STR_FUNC_CALL`, `CAST_FUNC_CALL`, `STR_FUNC_CALL`, `TRIM_CALL`, `DATE_FUNC_CALL`, `TWO_ARG_FUNC_CALL`, `DEBUG_EXPR`.
**Operator symbols** (`OPS[]`): `MINUS_OP`, `PLUS_OP`, `MUL_OP`, `DIV_OP`, `CONCAT_OP`, `EQ_OP`, `NEQ_OP`, `GT_OP`, `GE_OP`, `LT_OP`, `LE_OP`, `LIKE_OP`, `NOT_LIKE_OP`, `IN_OP`, `NOT_IN_OP`, `EXISTS_OP`, `NOT_EXISTS_OP`, `NULL_OP`, `NOT_NULL_OP`, `NOT_OP`, `AND_OP`, `OR_OP`.

**Structural rules the helpers rely on:**
- Field: alias present iff `/IDENTIFIER` count > 1; alias = 1st `IDENTIFIER`, name = 2nd (else name = 1st).
- Func call: name = `/*` child index 1 (`.Value`); args = remaining `/*` children (arg *i* at `/*` index *i*+2); TRIM arg at `/*` index 1; COUNT(*) → 0 args.
- Operator node: args are its `/*` children (arg *i* at index *i*+1).

**Path literals asserted** (`AstNodeSelectExtensions.cs`) — the grammar rule nesting must match:
```
/DEBUG_EXPR/*                                             (DebugExpr)
/SELECT                                                    (SelectStatement)
/SELECT_LIST/SELECT_SUBLIST/EXPR_ALIAS                     (Resultset)
/TABLE_EXPRESSION/FROM_CLAUSE/TABLE_REFERENCE_LIST/*       (AllTables)
TABLE_PRIMARY/TABLE_NAME/IDENTIFIER  &  TABLE_PRIMARY/IDENTIFIER   (TableName/Alias)
/JOIN_CONDITION/*  &  /JOIN_TYPE/*                         (join detect/type/condition)
/TABLE_EXPRESSION/WHERE_CLAUSE  ·  /HAVING_CLAUSE          (where/having)
/LIMIT_OFFSET/OFFSET  ·  /LIMIT_OFFSET/LIMIT              (limit/offset)
/SORT_SPECIFICATION_LIST/SORT_SPECIFICATION               (sort)
/GROUP_SPECIFICATION_LIST/GROUP_SPECIFICATION             (group)
/SET_QUANTIFIER/*                                         (distinct/all)
```
`TableJoinType` expects `/JOIN_TYPE/*` child symbol e.g. `JOIN_TYPE_INNER`/`_LEFT`/`_RIGHT`/`_FULL` (defaults to `JOIN_TYPE_INNER`). Sort direction = 2nd child `.Value` (`ASC`/`DESC`, default `ASC`).

⚠️ This is why the `EF.Test` `.g4` must keep the **Hime rule names** and the wrapper must **collapse `^`/`!`** exactly (promoted child replaces parent; dropped tokens absent). The full `SqlTest` grammar to port is in Appendix F.

## Appendix E — Keyword/literal → lexer-token inventory

Every quoted literal in the grammars (must become lexer tokens; keyword tokens declared **before** `IDENTIFIER`). Case-sensitive.

**`.Sql` grammar** — keywords: `ABS ADD ALL AND AS ASC AUTO AVG BOOLEAN BOTH BREAK CASE CLOSE CONTAINS CONTINUE COUNT CURSOR DATE DATETIME DECLARE DELETE DESC DISTINCT DOUBLE ELSE ELSIF END ENDSWITH EXIT FALSE FETCH FIELD FOR FROM FULL GET_FIELD GET_ROW IF IMPORT IN INNER INSERT INTEGER INTO IS JOIN LAST_RESULT LEADING LEFT LIKE LIMIT LOOP LOWER LTRIM MAX MIN NEW_ROW NEW_ROWSET NEXT NOT NULL OFFSET ON OPEN OR OTHERWISE OUTER RIGHT ROW ROWSET ROWS_COUNT RTRIM SELECT SET STARTSWITH STRING SUM SWITCH THEN TO TODATE TODOUBLE TOINT TOSTRING TOTIMESTAMP TRAILING TRIM TRUE UPDATE UPPER VALUES WHERE WHILE WITH`. Multi/compound tokens: `'GROUP BY'`, `'ORDER BY'` (embedded space), `'COUNT(*)'` (→`AGGR_COUNT_ALL`). Operators/punct: `( ) * + , - . / : := ; < <= <> = > >= ? ||`.

**`EF.Test` (`SqlTest`) grammar** — keywords: `ABS ADD ALL ALTER AND AS ASC AUTOINCREMENT AVG BOTH COLUMN CONTAINS COUNT CREATE D DAY DEBUG DEFAULT DELETE DESC DISTINCT DROP ENDSWITH EXISTS FALSE FOREIGN FROM FULL HAVING HOUR IF IN INDEX INNER INSERT INTO IS JOIN KEY LEADING LEFT LENGTH LIKE LIMIT LOWER LTRIM MAX MIN MINUTE MONTH NOT NULL OFFSET ON OR OUTER PRIMARY REFERENCES RIGHT ROUND RTRIM SECOND SELECT SET STARTSWITH SUM TABLE TODATE TODOUBLE TOINT TOSTRING TOTIMESTAMP TRAILING TRIM TRUE UNION UNIQUE UPDATE UPPER VALUES VIEW WHERE YEAR`. Compound: `'GROUP BY'`, `'ORDER BY'`, `'COUNT(*)'`. Param prefixes: `: ? @`. Note `'D'` prefixes date constants (`DATE_CONST -> 'D'! STRING_CONST`).

## Appendix F — Grammars to port (verbatim, since `.gram` files are deleted in Phase 5)

The two source grammars are preserved here so the `.g4` authoring has the full spec even after the originals are removed. Reproduce rule structure; translate `^`/`!` per the approach in Parts A/B. See repo files `Gehtsoft.EF.Db.SqlDb.Sql/sql.gram` and `Gehtsoft.EF.Test/sql.gram` while they still exist; both are reproduced in this repo's git history at commit `6c4bfa2`. (Full text intentionally not duplicated inline to keep this doc navigable — copy the two `.gram` files into an `archive/` folder in Phase 0 before deletion so they remain available during the whole migration.)
