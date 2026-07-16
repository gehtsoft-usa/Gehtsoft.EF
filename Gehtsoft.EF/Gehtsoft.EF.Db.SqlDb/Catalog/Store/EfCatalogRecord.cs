using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.Catalog.Store
{
    /// <summary>
    /// One row of the EF-owned schema catalogue: a single applied-schema snapshot for a table at one
    /// point on its version timeline. The catalogue is an <b>append-only history</b> - a new row is
    /// added whenever the recorded state of a <c>(scope, table)</c> pair actually changes (its version
    /// or its snapshot); a plain re-apply of an identical state appends nothing. The latest
    /// <see cref="Migrated"/> row for a pair is its current applied state.
    ///
    /// The history shape (per-version rows + the <see cref="Migrated"/> marker) is present from v1 even
    /// though torn-write recovery is not yet implemented, because this table is hand-bootstrapped
    /// outside the catalogue and reshaping it later would be exactly the fragile manual migration the
    /// catalogue exists to remove. In v1 every appended row is written with <see cref="Migrated"/> =
    /// `true` (apply-then-record); the two-phase <c>false → apply → true</c> write and resume-in-progress
    /// behaviour are a later phase, needing no further change to this shape.
    ///
    /// It is managed directly through the ordinary entity machinery - like
    /// <see cref="EntityQueries.CreateEntity.Patch.EfPatchHistoryRecord"/> - and never through the
    /// catalogue controller itself, so the catalogue table is not catalogued recursively. The catalogued
    /// table's scope is normalized to the empty string when it has none, so the lookup column is always
    /// non-null and matchable with a plain equality.
    /// </summary>
    [Entity(Table = "ef_catalog", Scope = "ef_catalog")]
    public class EfCatalogRecord
    {
        /// <summary>The surrogate primary key; also the monotonic ordering key that identifies the latest row.</summary>
        [AutoId]
        public int ID { get; set; }

        /// <summary>The catalogued table's scope, normalized to the empty string when it has none.</summary>
        [EntityProperty(Sorted = true, Size = 128, Nullable = false)]
        public string Scope { get; set; }

        /// <summary>The catalogued table's name.</summary>
        [EntityProperty(Sorted = true, Size = 128, Nullable = false)]
        public string TableName { get; set; }

        /// <summary>The DB version supplied to Create/UpdateTables when this state was applied.</summary>
        [EntityProperty(Size = 64, Nullable = true)]
        public string Version { get; set; }

        /// <summary>The UTC time this row's state was applied.</summary>
        [EntityProperty]
        public DateTime AppliedUtc { get; set; }

        /// <summary>
        /// Whether the DB was actually brought to this row's state. Always `true` in v1 (apply then
        /// record); reserved as the torn-write recovery signal for a later phase, where an in-progress
        /// write is recorded `false` first. <see cref="Store.CatalogStore.ReadApplied"/> already ignores
        /// `false` rows.
        /// </summary>
        [EntityProperty]
        public bool Migrated { get; set; }

        /// <summary>
        /// Whether this row is a <b>tombstone</b> - the table was dropped at this <see cref="Version"/>.
        /// The latest row of a <c>(scope, table)</c> pair being a tombstone means the table is currently
        /// absent from the catalogue: <see cref="Store.CatalogStore.ReadApplied"/> and
        /// <see cref="Store.CatalogStore.ReadAppliedForScope"/> treat it as no entry, so a dropped table
        /// is neither re-created nor re-dropped, while reintroducing it in the model diffs
        /// <c>null → CreateTable</c> (a new live row after the tombstone). The tombstone keeps the
        /// last-known <see cref="Snapshot"/> for audit. `false` on every ordinary applied-state row.
        /// </summary>
        [EntityProperty]
        public bool Dropped { get; set; }

        /// <summary>The schema-format version of the stored <see cref="Snapshot"/>.</summary>
        [EntityProperty]
        public int SchemaFormatVersion { get; set; }

        /// <summary>The serialized <see cref="CatalogSnapshot"/> text for this applied state (unbounded).</summary>
        [EntityProperty(Size = 0, Nullable = false)]
        public string Snapshot { get; set; }
    }
}
