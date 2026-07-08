# Dynamic properties — known issues

## 1. Dynamic-property INSERT fails on MySQL and Oracle (multi-statement batched command) — ✅ FIXED 2026-07-05

**Fix (two root causes, both addressed):**
1. **Open readback reader.** The owner insert read its auto-id back through a `DataReader` and left it
   open on the connection; the follow-up props command (a second command on the same connection) was
   then rejected on **Postgres / MSSQL / MySQL** ("command already in progress" / "open DataReader" /
   "connection in use"). Fixed by closing that reader as soon as the id is read: added idempotent
   `SqlDbQuery.CloseReader()` and call it in `UpdateQueryToTypeBinder` right after the auto-id readback.
2. **Per-row readback tail in the batch.** Each batched props insert emitted the driver's
   autoincrement read-back — `; SELECT LAST_INSERT_ID();` (→ `;;` empty statement on **MySQL**) and
   `RETURNING id INTO :id` (→ repeated `:id` output param, **ORA-50028**, on **Oracle**). The generated
   props-row ids are never used, so added `InsertQueryBuilder.ReturnAutoincrement` (default `true` =
   historical behavior); each driver's `BuildQuery` guards its read-back on it, and the batched props
   inserts set it `false`. The DB still generates the id; only the read-back is suppressed.

**Verified:** debug test 6/6 drivers, `DynamicPropertiesMultiDeleteTest` 18/18, entity insert tests
42/42, full dynamic-properties suite 132/132. (Historical detail of the defect below.)

---

**Discovered 2026-07-04** by the first multi-driver run of a dynamic-properties test
(`DynamicPropertiesMultiDeleteTest`). All dynamic-properties CRUD tests before it were **SQLite-only**
(in-memory), so this was latent.

**Symptom** — seeding (an insert of an owner with properties) throws in
`InsertEntityQuery.SaveDynamicProperties` → the combined multi-statement insert built with
`MultiSqlQueryBuilder`:
- **MySQL:** `System.InvalidOperationException : This MySqlConnection is already in use.`
- **Oracle:** `System.ArgumentException : ORA-50028: Invalid parameter binding (Parameter 'id')`
- SQLite: works. (PostgreSQL / SQL Server not yet characterized, but every non-SQLite connection failed.)

**Where** — `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityQuery/InsertEntityQuery.cs` (`SaveDynamicProperties`
→ `BuildInsert`), which packs one `InsertQueryBuilder` per property into a single `MultiSqlQueryBuilder`
command (owner param shared, per-row params suffixed) and runs it as one `ExecuteNoData`.

**Likely cause** — the *multi-statement single command* is the common factor:
- MySqlConnector rejects/serializes multiple statements per command differently ("connection already in use").
- Oracle wraps the block in `BEGIN … END;` (via `PreBlock`/`PostBlock`) and the suffixed/owner parameter
  binding doesn't line up (`ORA-50028`).

**Impact** — dynamic-properties **insert** (and therefore any test that seeds data) works only on SQLite
today. This blocks multi-driver testing of *all* dynamic-properties CRUD, not just MultiDelete.

**Not yet fixed.** Options to evaluate: execute the per-property inserts as separate commands instead of
one batched multi-statement command (simplest, portable, loses the single-round-trip optimization); or
make `MultiSqlQueryBuilder` execution driver-correct for MySQL/Oracle. Revisit alongside the temp-table
work (see `../ENTITY_WHERE_PROBLEM.md`).

**Test status** — now GREEN on all drivers. `DynamicPropertiesMultiDeleteTest` (18/18) and the minimal
`DynamicPropertiesMultiStatementDebugTest` (6/6) both pass on every configured connection. The debug
test can stay as a fast, focused multi-statement-insert regression, or be removed now that MultiDelete
covers insert-with-props on all drivers.
