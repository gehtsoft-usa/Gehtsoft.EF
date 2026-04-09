---
name: dotnet-coverage
description: "Analyze .NET unit test code coverage from dotnet-coverage XML files. Use this skill whenever the user mentions code coverage, test coverage, coverage gaps, coverage reports, dotnet-coverage, .coverage files, or wants to improve test coverage for .NET/C# projects. Also trigger when the user has coverage.xml files, wants to collect coverage for a .NET solution, or asks what a specific test covers. Even if the user just says 'coverage' in the context of a .NET project, use this skill."
---

# .NET Test Coverage Analyzer

Analyze unit test coverage for .NET projects using `dotnet-coverage` XML data. The focus is on discovering meaningful coverage gaps — connected to real user scenarios, not just raw line counts — and creating actionable plans to improve coverage.

## Prerequisites

The `dotnet-coverage` global tool must be installed:

```bash
dotnet tool install -g dotnet-coverage
```

## Obtaining Coverage Data

Every analysis command requires a coverage XML file. Before running any analysis, resolve where the data comes from:

1. If the user provided a specific file path, use that directly.
2. Otherwise, look for `coverage.xml` in the project root.
   - **If found**: check its modification time (`stat` or `ls -l`), report the date/age to the user, and ask: "Found coverage.xml from <date/time> (<N hours/days ago>). Do you want to use this file or re-collect coverage?" Wait for the user's answer before proceeding.
   - **If not found**: tell the user no coverage data exists and you will collect it now, then proceed to collection.

Do NOT silently reuse a stale file. Do NOT silently re-collect when a file already exists.

After collecting or converting coverage data, check that the output file (e.g., `coverage.xml`) is listed in the project's `.gitignore`. If it is not, add it. Coverage files are large, machine-generated, and should never be committed.

## Collecting Coverage

There are two ways to get coverage XML data. Do not wrap these in scripts — run them directly.

### Fresh collection from tests

**Always collect against the solution file (`.sln`), never against an individual test project.** Running against the solution ensures all test projects execute and coverage spans the entire codebase. Find the `.sln` file first — if there are multiple, ask the user which one to use.

```bash
dotnet-coverage collect "dotnet test <solution.sln>" -f xml -o coverage.xml
```

Do NOT use paths to individual `.csproj` test projects (e.g., `MyProject.Tests/MyProject.Tests.csproj`) unless the user explicitly asks to scope collection to a specific test project.

### Converting Visual Studio binary coverage files

If coverage was already collected by Visual Studio (`.coverage` files):

```bash
dotnet-coverage merge --output coverage.xml <file.coverage> -f xml
```

### Per-test collection

To find what a specific test covers, collect coverage for just that test. Even here, run against the solution so all production assemblies are instrumented:

```bash
dotnet-coverage collect "dotnet test <solution.sln> --filter FullyQualifiedName~<TestName>" -f xml -o test_coverage.xml
```

Use `~` (contains) for partial match. If the filter is ambiguous (matches multiple tests), ask the user to provide a more specific name.

## Analysis Scripts

All scripts live in `scripts/` relative to this skill's directory. They output JSON to stdout. Find the skill directory path and use it as the base for script paths.

### General Coverage Report

```bash
python3 <skill-dir>/scripts/coverage_report.py <coverage.xml> --mode summary
```

Shows overall coverage percentage, per-module breakdown (sorted worst-first), and function counts per module (fully covered / partially covered / uncovered).

### Top N Least Covered (Gaps)

```bash
python3 <skill-dir>/scripts/coverage_report.py <coverage.xml> --mode gaps --top 20
```

Options:
- `--top N` — number of items (default: 20)
- `--group-by {function|type|namespace|module}` — grouping level (default: function)
- `--module <name>` — filter to modules containing this string
- `--namespace <name>` — filter to namespaces containing this string

Use `--group-by type` for a higher-level view that clusters methods by class.

### Per-Test Coverage

After collecting single-test coverage (see "Per-test collection" above):

```bash
python3 <skill-dir>/scripts/find_test_coverage.py --full <full_coverage.xml> --test <test_coverage.xml>
```

Shows what modules/functions the test exercises and highlights areas where the full suite has weak coverage that this test contributes to.

### Coverage Improvement Plan

```bash
python3 <skill-dir>/scripts/coverage_plan.py <coverage.xml> --target-coverage 90
```

Options:
- `--target-coverage N` — target line coverage % (default: 90)
- `--max-areas N` — max functional areas to report (default: 10)
- `--module <name>` / `--namespace <name>` — scope the analysis

## Common Options (All Scripts)

- `--include-mechanical` — include mechanical methods in analysis (excluded by default)
- `--include-tests` — include test assembly modules like `*.Test.dll` (excluded by default)
- `--module <name>` — filter to modules whose name contains this string
- `--namespace <name>` — filter to namespaces containing this string

## Filtering Third-Party Libraries

Coverage collection often instruments third-party libraries that happen to be loaded (e.g., `MySqlConnector.dll`, `Npgsql.dll`). These clutter the analysis. When the user's project has a clear namespace root (e.g., `Gehtsoft.EF`), use `--namespace` to focus on project code:

```bash
python3 <skill-dir>/scripts/coverage_report.py coverage.xml --mode gaps --top 20 --namespace Gehtsoft.EF
```

If unsure about the project namespace, look at the module names in the summary output — production modules typically share a common prefix. Ask the user if it's ambiguous.

## Mechanical Method Filtering

By default, the scripts exclude methods that are "mechanical" — they exist for structural reasons and testing them individually provides little value:

**Excluded by name pattern:**
- Property accessors: `get_*`, `set_*`
- Constructors: `.ctor`, `.cctor`
- Standard overrides: `Dispose`, `ToString`, `GetHashCode`, `Equals`
- Operator overloads: `op_*`
- `CompareTo`

**Excluded by type pattern (compiler-generated):**
- Closure classes: `<>c__DisplayClass*`
- Async state machines: `<*>d__*`
- Local functions: `<*>g__*`
- Record synthesized types: types containing `$`

These methods get tested implicitly when the real scenarios they support are tested. If the user specifically wants to see them, pass `--include-mechanical`.

## Interpreting Results: Scenario-Based Approach

This is the most important part of the skill. Raw coverage numbers are useful for triage, but the real value comes from connecting gaps to user-facing behavior.

### When presenting coverage gaps

Do NOT just list uncovered methods. Instead:

1. **Group by functional area.** Look at the namespace and type names to understand what area of the application the gap belongs to. Present gaps as "The SQL expression parser has untested paths for handling nested conditions" rather than "SqlExpressionParser.ParseBinaryExpression has 99 uncovered lines."

2. **Explain what's at risk.** For each gap area, describe what user-facing behavior could break without test coverage. "If the OData date filtering logic has a bug, API consumers querying by date range will get wrong results" is actionable. "Function X at line Y is uncovered" is not.

3. **Suggest scenario-based tests.** Recommend tests that exercise real workflows, not individual methods:
   - Bad: "Write a test for `SqlParser.ParseWhereClause`"
   - Good: "Test scenario: Build a query that filters records by multiple conditions (date range AND status), execute it against a test database, verify the result set matches expected records"

4. **Read source files when needed.** The coverage data includes source file paths. When the function names alone don't make the purpose clear, read the source code to understand what the uncovered code does before making recommendations. The source paths are in the JSON output's `source_file` fields.

5. **Prioritize business logic over plumbing.** An uncovered `CreateConnection()` wrapper matters less than an uncovered `ProcessOrder()` method. Use judgment about what code is core business logic vs infrastructure/framework support.

### When creating an improvement plan

Run `coverage_plan.py` to get structured data, then:

1. Write a markdown report file (e.g., `coverage_plan.md`) with:
   - Executive summary: current coverage, target, lines to cover
   - Top functional areas ranked by gap size, with scenario-based test suggestions for each
   - Quick wins section: functions needing just 1-5 more covered lines
   - Per-module status showing which are below target

2. Also discuss interactively — present the 3-5 biggest gaps and ask which area the user wants to focus on first. Different teams have different priorities.

### Template for presenting a functional area gap

```
## [Area Name] — [brief description of what this code does]
**Current coverage**: X% (Y uncovered lines across Z types)
**What's at risk**: [User-facing behavior that lacks test verification]
**Suggested test scenarios**:
1. [Setup → Action → Verification scenario]
2. [Another scenario covering different paths]
**Quick wins in this area**: [Any functions with just 1-2 uncovered lines]
**Expected coverage gain**: ~N lines if all scenarios are implemented
```

## Reference

For details on the XML format (element attributes, source_id scoping, coverage states), see `references/xml_format.md` in this skill's directory.
