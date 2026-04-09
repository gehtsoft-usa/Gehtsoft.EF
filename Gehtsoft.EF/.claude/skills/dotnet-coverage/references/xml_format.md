# dotnet-coverage XML Format Reference

This documents the XML schema produced by `dotnet-coverage collect -f xml` and `dotnet-coverage merge -f xml`.

## Top-level Structure

```xml
<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<results>
  <modules>
    <module ...>
      <functions>
        <function ...>
          <ranges>
            <range ... />
          </ranges>
        </function>
      </functions>
      <source_files>
        <source_file ... />
      </source_files>
      <skipped_functions>
        <skipped_function ... />
      </skipped_functions>
    </module>
  </modules>
  <skipped_modules>
    <skipped_module ... />
  </skipped_modules>
</results>
```

## `<module>` Attributes

| Attribute | Description |
|-----------|-------------|
| `id` | SHA1 hash identifier |
| `name` | Assembly filename (e.g. `Gehtsoft.EF.Db.SqlDb.dll`) |
| `path` | Same as name in most cases |
| `block_coverage` | Percentage of blocks covered (0-100) |
| `line_coverage` | Percentage of lines covered (0-100) |
| `blocks_covered` | Number of covered blocks |
| `blocks_not_covered` | Number of uncovered blocks |
| `lines_covered` | Number of fully covered lines |
| `lines_partially_covered` | Number of partially covered lines |
| `lines_not_covered` | Number of uncovered lines |

## `<function>` Attributes

| Attribute | Description |
|-----------|-------------|
| `id` | Numeric identifier |
| `token` | IL metadata token (e.g. `0x6000001`) |
| `name` | Full method signature with qualified parameter types. Example: `BuildQuery(System.Text.StringBuilder, Gehtsoft.EF.Db.SqlDb.QueryBuilder.TableDescriptor.ColumnInfo)` |
| `namespace` | .NET namespace |
| `type_name` | Containing class/struct name. Compiler-generated types use XML-escaped angle brackets: `&lt;&gt;c__DisplayClass` for `<>c__DisplayClass` |
| `block_coverage` | Percentage (0-100) |
| `line_coverage` | Percentage (0-100) |
| `blocks_covered` / `blocks_not_covered` | Block counts |
| `lines_covered` / `lines_partially_covered` / `lines_not_covered` | Line counts |

## `<range>` Attributes

| Attribute | Description |
|-----------|-------------|
| `source_id` | References `<source_file id="...">` within the **same module** |
| `start_line` / `end_line` | Line numbers in source file |
| `start_column` / `end_column` | Column numbers |
| `covered` | Coverage status: `yes`, `no`, or `partial` |

**Important**: `source_id` is scoped per module. ID `"0"` in module A refers to a different file than ID `"0"` in module B.

## `<source_file>` Attributes

| Attribute | Description |
|-----------|-------------|
| `id` | Numeric ID referenced by `source_id` in ranges |
| `path` | Absolute path to source file (e.g. `D:\develop\...\SomeFile.cs`) |
| `checksum_type` | Hash algorithm (typically `SHA256`) |
| `checksum` | File content hash |

## `<skipped_function>` Attributes

| Attribute | Description |
|-----------|-------------|
| `id` | Numeric identifier |
| `name` | Method name |
| `type_name` | Containing type |
| `reason` | Why skipped, e.g. `attribute_excluded` (has `[ExcludeFromCodeCoverage]`) |

## `<skipped_module>` Attributes

| Attribute | Description |
|-----------|-------------|
| `name` | Assembly filename |
| `path` | Assembly filename |
| `reason` | `no_symbols` (no PDB) or `path_is_excluded` (excluded by configuration) |

## Notes

- There is **no per-test attribution** in this format. Coverage is aggregated across all tests in the run. To get per-test coverage, run a single test with `--filter` and collect separately.
- Type hierarchy is **implicit** — functions are flat within a module. Group by `(namespace, type_name)` to reconstruct the class structure.
- The `name` attribute on `<function>` contains fully-qualified parameter types. For display, strip to short names (e.g. `StringBuilder` instead of `System.Text.StringBuilder`).
