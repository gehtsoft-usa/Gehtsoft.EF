# Entity WHERE is welded to one SQL statement — problem & proposed refactor

**Status:** *Deferred.* A contained **workaround** is used for EAV `MultiDelete`/`MultiUpdate`
(see "Why deferred" + the Phase 2 plan). This document records the real problem and the correct fix
so we can pick it up when a second consumer appears. Written 2026-07-04.

---

## 1. The problem

### The two ground assumptions being violated
The `EntityQuery` / entity-condition layer is built on two assumptions:

- **A1 — the WHERE can be rendered to SQL eagerly, at author-time**, because we always know the one
  query we're building. `q.Where.Property("x").Eq(5)` resolves the column to a single alias string
  and binds the value immediately; the text is written straight into that query's SQL `ConditionBuilder`.
- **A2 — one entity query is exactly one SQL statement.** `EntityQuery.PrepareQuery` does
  `mQuery.CommandText = mBuilder.QueryBuilder.Query` — one builder, one statement.

**A1 depends on A2.** Eager rendering is only possible *because* "one query = one statement"
guarantees a single, known target with a single alias scheme. Break A2 and A1 collapses with it.

### What breaks them
A dynamic-property entity is **an aggregate of tables** (root table + EAV side table). So a single
logical operation — "delete/update the entities matching predicate P" — is no longer one statement;
it is a **coordinated plan** over root + side tables, all keyed by the one predicate P:

```
DELETE FROM owner_props WHERE owner IN (SELECT owner.id FROM owner WHERE P);   -- child, FK order
DELETE FROM owner       WHERE P;                                                -- root
```

Now P must appear in **two** statements — and they render it **differently**:

- The **modify** statement (`DELETE`/`UPDATE`) qualifies columns by **table name**: `owner.col`
  (`QueryWithWhereBuilder.GetAlias` → `"{Table.Name}.{Column.Name}"`). It *must*: SQLite does not
  allow an alias on a `DELETE`/`UPDATE` target, so table-name qualification is the portable choice.
- The **select** for the `IN (…)` sub-query qualifies by a **synthetic alias**: `entity7.col`
  (`SelectQueryBuilder.GetAlias` → `"{queryEntity.Alias}.{Column.Name}"`). It *must*: selects support
  joins / multiple occurrences of a table, which need aliases.

So the same logical predicate genuinely produces **different SQL text** in a delete vs a select, and
there is no single aliasing that satisfies both. A predicate that has already been **rendered to a
string** is therefore welded to one statement and cannot be reused in the other.

### The forcing constraint (why the tidy version is impossible)
The tidiest plan would render P **once** into a select and have every statement say
`… IN (SELECT owner FROM owner WHERE P)`. But **MySQL forbids the root one** —
`DELETE FROM owner WHERE id IN (SELECT … FROM owner …)` → error 1093, *"You can't specify target table
'owner' for update in FROM clause."* So the root delete/update cannot reference a select over its own
table; it must carry P **directly**. Hence P is genuinely needed in two places, rendered two ways.

---

## 2. The correct fix — detach EntityWhere from SQLWhere; make predicates copyable

**Detach the abstract predicate (EntityWhere) from the query-specific SQL (SQLWhere), allow copying
of abstract predicates, and delay conversion to query-specific SQL to the very last moment.**

- **EntityWhere = an abstract predicate**, not eager SQL. `q.Where` stops resolving aliases and
  assembling SQL at author-time; it *records* the predicate as data: column references kept as
  `(type, occurrence, name)`, plus literal values, sub-queries, raw fragments, operators, function
  wraps (`Sum`/`Year`/…), logical connectives, and group/bracket structure — i.e. a small predicate AST.
- **Copyable.** Because it is pure data, a predicate can be **copied and attached to N queries**. The
  two-target problem then dissolves: build P once, copy it onto the root modify query *and* the child
  select — no fan-out plumbing, no shared-sink coordination.
- **Compile at the last moment.** `PrepareQuery` becomes a **plan**: it compiles 1..N SQL statements
  and, for each, walks the recorded predicate, resolving each column ref against **that statement's**
  builder (its own `GetAlias`), binding values on the target query, and feeding the existing
  `ConditionBuilder` (SQLWhere). The SQL-generation layer is *unchanged* — we just feed it later.

This relaxes **both** assumptions: A1 (predicate is recorded, not eagerly rendered) and A2 (a query
compiles to a plan of statements). For a normal query the plan has one target and the predicate is
compiled once → **byte-identical SQL to today**, only the *timing* of resolution moves from
author-time to prepare-time.

`DynamicPropertyOf<T>` fits naturally: a predicate node that, at compile, emits
`root-pk IN (SELECT owner FROM <t>_props WHERE name=@n AND <valcol> {op} @v)` — the root-pk ref
resolves per target, the sub-query is self-contained.

### Backward-compatibility contract (the hard part)
1. **Public surface frozen** — every method/extension on `EntityQueryConditionBuilder` /
   `SingleEntityQueryConditionBuilder` keeps its exact signature and return type; only internals change.
2. **Single-target compiles to byte-identical SQL** — the full (~3000) test suite is the oracle.
3. **Preserve early failure** — keep a validate-only resolve at author-time (confirm the property
   exists) so `ColumnNotFound` still throws early; store the ref, render for real at compile.
4. **`ToString()`, `Having`, cross-query `GetReference` / `.Query(…)` / `.In(query)` sub-queries** all
   keep working (compile against the primary target; sub-queries are self-contained tokens).
5. **Audit the leaky accessors** — `SingleEntityQueryConditionBuilder.Left/Right/Op/ParameterName(s)`
   are public-ish and would be empty until compile; find and handle their consumers.

### Staging (to avoid a big untested cliff)
- **Stage 0** — audit consumers of the leaky accessors; inventory the token kinds the recorder must cover.
- **Stage 1** — introduce the predicate recorder behind the frozen API; `PrepareQuery` compiles
  single-target. **No dynamic properties yet.** Goal: full suite green with identical SQL. (Big, risky,
  self-contained step.)
- **Stage 2** — generalize `PrepareQuery` to a plan (1..N statements); reimplement EAV `MultiDelete`
  cascade on it (root delete + `owner IN (SELECT root WHERE P)`), both compiling the copied predicate.
- **Stage 3** — `MultiUpdate` reuses the plan.

---

## 3. Why deferred — workaround chosen for now
- **Blast radius.** The fix touches the **universal** entity-condition builder used by *every* entity
  query; correctness bar is byte-identical SQL across the whole suite. That's a large, real investment
  in a hot path.
- **No consumer outside EAV yet.** The only thing that needs "one predicate, two statements" today is
  EAV `MultiDelete` / `MultiUpdate`. Dynamic properties are deliberately scoped as *storage + occasional
  filtering*, not a hot general feature — funding a core refactor solely for it isn't justified now.
- **Decision:** implement a **contained workaround** for the two EAV cascades; schedule this refactor
  as a deliberate, separately-scoped effort **only when a second consumer appears** (e.g. general
  predicate reuse / query composition / another multi-table aggregate).

### The workaround as actually implemented (EAV MultiDelete; MultiUpdate to reuse it) — 2026-07-05
The cascade reuses the modify statement's own rendered WHERE (which is `<table>.<col>` qualified),
realigning only the qualifier prefix to the sub-select's alias scheme (`<entityN>.<col>`) — a rewrite
of *builder-generated* text, never hand-written SQL. Three shapes in `MultiDeleteEntityQuery`, chosen by
whether the WHERE reads `_props`:
- **no WHERE / regular-column WHERE** → one combined command: `DELETE props WHERE owner IN (SELECT id
  FROM owner WHERE <cond>); DELETE owner WHERE <cond>`.
- **WHERE filters on a dynamic property** → **materialize** the matched owner ids to the client, then,
  in a **nested transaction**, batches ≤50: delete props (subquery form if the engine allows a table in
  its own delete's sub-query, else `IN [ids]`), then delete owners by `IN [ids]`. Client-side
  materialization (not a temp table) was NG's call — dynamic properties are storage + occasional
  filtering, not mass ops.

### General limitation surfaced: self-referencing DML on MySQL
MySQL/MariaDB reject a `DELETE`/`UPDATE` whose sub-query reads the **same** table being modified
(error 1093). This is a *general* limitation — a caller can hit it with their own
`MultiDelete<T> WHERE id IN (SELECT … FROM T …)`, not only via EAV cascades. Captured as
`SqlDbLanguageSpecifics.SelfReferenceInDeleteAllowed` (true; MySQL false). The cascade consults it:
where self-reference is disallowed, the props delete goes by materialized `IN [ids]` instead of a
sub-query. Our cascade otherwise never emits a table-in-its-own-sub-query.

### (superseded note) earlier raw-splice idea
An earlier sketch spliced the rendered WHERE text into a hand-built `SELECT … FROM owner WHERE <text>`.
Rejected: it emits SQL structure (non-portable). The implemented version builds the sub-select with a
`SelectQueryBuilder` and only rewrites the column-qualifier prefix — no hand-written SQL.
