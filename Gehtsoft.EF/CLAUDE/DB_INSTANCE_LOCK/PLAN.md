# DB-instance lock — `IDbInstanceLock` (PLAN, for Gate)

*Standalone, reusable framework capability. Prerequisite (B) for the Schema Catalogue (`../SCHEMA_CATALOGUE/
DESIGN.md`) but independent of it — a cross-process DB mutex the framework lacks today. Drafted
2026-07-14; **no code until approved.** Process per [[feedback_phase_process]].*

## Goal

A **DB-instance-wide advisory mutex** so two processes running schema updates against the same database
serialize. **Driving constraint:** because DDL **auto-commits on Oracle/MySQL**, the lock must be
**session/advisory-scoped and held across the whole critical section** — a transaction-scoped lock cannot
span auto-committing DDL. "Acquire, then read the catalogue as one" means the *acquire+read* is ordered;
the *hold* outlives any single transaction.

## API surface

On `SqlDbConnection` (abstract partial class; drivers subclass) — the natural seam, since a lock is tied
to a live DB **session**:

- `IDbInstanceLock AcquireInstanceLock(string name, TimeSpan timeout)` — blocks up to `timeout`; throws
  `EfSqlException(EfExceptionCode.LockTimeout)` (new code) on contention.
- `bool TryAcquireInstanceLock(string name, TimeSpan timeout, out IDbInstanceLock handle)` — non-throwing.
- `IDbInstanceLock : IDisposable` — `Dispose()` releases (idempotent); exposes `Name`, `IsHeld`.
- Default `name` for the catalogue = a single well-known constant (instance-wide mutex per Gate-1
  decision); the API takes a name so it is reusable for other coarse locks.

Base implementation on `SqlDbConnection` = the **portable lease fallback**; each driver overrides with its
**native advisory lock**. Driver identity via `GetLanguageSpecifics().DbName`.

## Native advisory locks (preferred — most auto-release on disconnect → free crash recovery)

- **PostgreSQL:** `pg_try_advisory_lock(key)` in a poll/backoff loop until `timeout`; `pg_advisory_unlock(key)`
  on dispose. `key` = stable 64-bit hash of `name`. Session-scoped → auto-released if the session drops.
- **MSSQL:** `sp_getapplock @Resource=name, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=<ms>`;
  `sp_releaseapplock @Resource=name, @LockOwner='Session'`. Returns ≥0 on success.
- **MySQL:** `GET_LOCK(name, <timeout-seconds>)` → 1 success / 0 timeout / NULL error; `RELEASE_LOCK(name)`.
  Auto-released on disconnect.
- **Oracle:** `DBMS_LOCK.ALLOCATE_UNIQUE(name, lockhandle)` + `DBMS_LOCK.REQUEST(lockhandle, X_MODE,
  timeout)`; `DBMS_LOCK.RELEASE`. Requires `EXECUTE ON DBMS_LOCK` — if not granted, **fall back to the
  lease table** (detect + document).

## Portable lease fallback (base impl; used by SQLite, and Oracle without DBMS_LOCK)

- Self-bootstrapping table **`ef_catalog_lock`**: `name` (PK), `owner` (opaque token), `expires_at`
  (DB-server time). Acquire = a conditional `UPDATE … SET owner=@me, expires_at=@now+lease WHERE name=@n
  AND (owner IS NULL OR expires_at < @now)` (INSERT the row first if missing), retry/backoff until
  `timeout`. Release = `UPDATE … SET owner=NULL WHERE owner=@me`.
- **Lease + expiry** so a crashed holder frees automatically (native locks don't need this; the table does).
  Clock source = **DB server time**, never the client, to avoid skew.
- **Lease duration is CONFIGURABLE (decided)** — a lease/timeout option (on the connection or a global
  options object; default modest, e.g. tens of seconds, not minutes), so the caller sizes it to their
  longest realistic update. v1 uses generous-lease-and-document (no heartbeat thread); revisit heartbeat
  only if a real case needs an unbounded critical section.
- SQLite note: usually single-process; the lease table still gives correct cross-process behaviour on a
  shared file. (`BEGIN EXCLUSIVE` is rejected — transaction-scoped, cannot span DDL.)

## Connection/session caveat (must design)

The lock lives on a specific DB **session**; that session must **not** be returned to a pool or reused for
unrelated work while the lock is held, or the lock's scope becomes undefined. Decide whether
`AcquireInstanceLock` pins the current `SqlDbConnection` for the handle's lifetime (recommended) and
document that the caller holds the connection open across the critical section.

## Testing (a SEPARATE test set)

New namespace `Gehtsoft.EF.Test.InstanceLock`:

- **Lease fallback on SQLite (always runs here):** acquire → a second acquirer (second connection) times
  out → release → second now succeeds; a **stale/expired** lease row is reclaimed (write an expired row,
  assert acquisition); dispose is idempotent; release only clears own token.
- **Per-driver native (live DB where configured; AST/text assert otherwise):** acquire/contended-timeout/
  release; **session-drop auto-release** (dispose/close the holding connection → a second acquirer
  succeeds) for pg/mssql/mysql; Oracle DBMS_LOCK path + the not-granted→lease fallback.
- Timeout semantics: `TryAcquire` returns false (no throw); `Acquire` throws `LockTimeout`.

## Risks / to confirm

- **RL1 — session scope vs pooling** (see caveat): the handle must keep its session; verify no pool reuse.
- **RL2 — Oracle DBMS_LOCK privilege**: detect absence, fall back to lease, document the grant.
- **RL3 — lease liveness**: generous-lease-and-document vs heartbeat; pick per max realistic update time.
- **RL4 — reentrancy**: recommend **non-reentrant** in v1 (a second acquire of a held name from the same
  session either blocks or is rejected) — or track depth. Decide at review.
- **RL5 — name→key hashing (Postgres/Oracle)**: stable, collision-tolerant (single well-known catalogue
  name makes collisions a non-issue in practice).

## Constraints

netstandard2.0 explicit `<Compile Include>` for product files; no LINQ; `ArgumentNullException.ThrowIfNull
(x, nameof(x))`; new `EfExceptionCode.LockTimeout`; never touch `version.proj`; product bugs →
`KNOWN_ISSUES.md`, tests never adapted.
