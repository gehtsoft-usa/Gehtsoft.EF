# DB-instance lock — build state

*Snapshot 2026-07-15. Branch `geo`. Plan: `PLAN.md`. Prerequisite (B) for the Schema Catalogue.
Uncommitted; commit only when asked; `version.proj` untouched.*

## Design note — built on the query builders, not raw SQL

All lock DML goes through the framework's query builders (`GetCreateTableBuilder`,
`GetInsertQueryBuilder`, `GetSelectQueryBuilder`, `GetUpdateQueryBuilder` + `ConditionBuilder`), never
hand-written SQL — the builders own parameter-prefix (`@` vs Oracle `:`), quoting and termination.
Two reusable extensions were made to support this:

- **`SqlFunctionId.Now`** (current **UTC** timestamp in the dialect's DateTime representation) and
  **`SqlFunctionId.LinuxSeconds`** (integer unix epoch seconds) added to `GetSqlFunction`, overridden in
  all 5 dialects. SQLite honours both DateTime models with pure SQL: `strftime` for the ISO-string mode,
  `JULIANDAY('now') - 2415018.5` for the OADate-`double` mode (2415018.5 = the OADate epoch's Julian day,
  the same conversion `ToDate` already uses) — **no custom scalar function needed** after all. The lease
  uses `LinuxSeconds` (integer math, no DateTime-model concern).
- **`AQueryBuilder.SuppressScalarProtection`** (builder-level bool, default false; mirrored on
  `IConditionBuilderInfoProvider` so `ConditionBuilder` operands honour it): when set, the builder skips
  the string-scalar SQL-injection guard on ALL its raw-expression methods (Select `AddExpressionToResultset`
  /`AddToResultset`/order/group, Update `AddUpdateColumn`/`AddUpdateColumnExpression`, condition `Raw`).
  Flag-first ordering (`if (!Suppress && Policy && ContainsScalar)`). Needed because the dialect NOW
  renderings legitimately contain quoted literals. NOT yet applied to the entity-layer
  `EntityQueryConditionBuilder.Raw` (separate higher layer; extend later if a caller needs it).

## What is built (v1 — portable lease fallback + API surface)

Decisions confirmed with the user 2026-07-15: **non-reentrant** (re-acquire of a held name from the
same connection throws immediately), **lease duration = per-acquire optional parameter** with a modest
default.

- **`EfExceptionCode.LockTimeout`** + message (`EfSqlException.cs`).
- **`IDbInstanceLock : IDisposable`** — `Name`, `IsHeld`. `Gehtsoft.EF.Db.SqlDb.InstanceLock`.
- **`SqlDbConnection` API** (partial `InstanceLock/SqlDbConnection.InstanceLock.cs`):
  - `IDbInstanceLock AcquireInstanceLock(string name, TimeSpan timeout, TimeSpan? leaseDuration = null)`
    — throws `LockTimeout` on contention.
  - `bool TryAcquireInstanceLock(string name, TimeSpan timeout, out IDbInstanceLock handle, TimeSpan? leaseDuration = null)`
    — non-throwing.
  - `DefaultInstanceLockLease` = 30 s (public static). Poll interval 50 ms (client-side; lease clock is
    the server's).
  - Reentrancy: name is **reserved** in a per-connection held-set before acquire (throws
    `InvalidOperationException` on a held name), released on timeout/failure and on handle dispose.
  - `protected virtual AcquireInstanceLockCore(name, timeout, lease)` = the seam a driver overrides with
    its native advisory lock (Phase 4). Base = lease fallback; bookkeeping/guard stay in the public
    methods.
- **Lease fallback** (`ef_catalog_lock`, self-bootstrapping): columns `name` (PK), `owner` (opaque
  GUID token), `expires_at` (INTEGER epoch seconds). Acquire = one atomic, **server-clocked**
  conditional `UPDATE … WHERE name=@n AND (owner IS NULL OR expires_at < <server-now>)` in a
  timeout/backoff loop; row seeded (owner NULL, expires 0) if absent (concurrent-seed race tolerated).
  Release clears the token only if still ours (`WHERE owner=@me`); dispose idempotent + best-effort.
- **`SqlDbLanguageSpecifics.CurrentServerTimeEpochSeconds`** — new virtual, the single clock source
  (server epoch seconds). Default throws `FeatureNotSupported`; **all 5 dialects override** it
  (SQLite `strftime('%s','now')`, PG `EXTRACT(EPOCH FROM now())`, MySQL `UNIX_TIMESTAMP()`, MSSQL
  `DATEDIFF_BIG(SECOND,'19700101',SYSUTCDATETIME())`, Oracle `SYS_EXTRACT_UTC(SYSTIMESTAMP)` diff).
- **`LeaseInstanceLock`** internal handle (`InstanceLock/LeaseInstanceLock.cs`).

## Tests — verified HERE (all 5 live drivers configured on this box)

Namespace `Gehtsoft.EF.Test.InstanceLock`, **24/24 green**:
- `LeaseInstanceLockTest` (9) — SQLite, shared **file** DB (not `:memory:`, which is per-connection) so
  two connections contend: acquire+table-bootstrap+handle-state; contended timeout → release → succeed;
  `Acquire` throws `LockTimeout`; expired-lease reclaim; release clears only own token; dispose
  idempotent; non-reentrant throw; re-acquire after release; distinct names don't contend.
- `InstanceLockActivationTest` (10 = 5 drivers × 2) — cross-driver acquire/release smoke test proving the
  lock activates on sqlite/mysql/mssql/oracle/pgsql.
- `SqlFunctionNowTest` (5) — cross-driver `Now`/`LinuxSeconds` values checked against the client's UTC
  clock (also exercises the `SuppressScalarProtection` projection path).

## Not done yet (later phases / needs live DBs)

- **Native advisory locks per driver** (Phase 4): pg `pg_advisory_lock`, MSSQL `sp_getapplock`
  `@LockOwner='Session'`, MySQL `GET_LOCK`, Oracle `DBMS_LOCK` (+ not-granted → lease fallback).
  Each overrides `AcquireInstanceLockCore`, needs its own handle + session-drop auto-release test.
- Per-driver **lease-fallback** live validation (the 5 epoch expressions run live only on SQLite here;
  the other 4 are written but AST/text-only so far).
- RL1 session-vs-pooling pinning: moot for the lease (keyed by token in a table, not by session);
  becomes real for the native session-scoped locks — revisit in Phase 4.
- RL3 lease liveness: v1 is generous-lease-and-document; no heartbeat thread.
- No async acquire variant (blocking acquire only, by design).
