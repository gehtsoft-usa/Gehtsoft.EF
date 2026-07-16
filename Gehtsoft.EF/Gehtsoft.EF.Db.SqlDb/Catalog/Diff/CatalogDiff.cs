using System;
using System.Collections.Generic;
using Gehtsoft.EF.Db.SqlDb.Catalog;

namespace Gehtsoft.EF.Db.SqlDb.Catalog.Diff
{
    /// <summary>
    /// Compares a table's desired schema (from the current model) against its stored applied schema
    /// (from the catalogue) and produces the ordered list of <see cref="CatalogChange"/> steps that
    /// bring the database from stored to desired. Pure and DB-agnostic - both sides are
    /// <see cref="CatalogTableDto"/>, and the result feeds the DDL emitter in a later phase.
    ///
    /// Identity is by name: a column by its SQL name, an index by its logical name; a rename therefore
    /// reads as a drop plus an add (v1 does not detect renames). Emitted steps are deterministically
    /// ordered so they are safe to apply in sequence: drop indexes, drop columns, alter columns, add
    /// columns, add indexes (dependents removed before their columns; columns added before their
    /// indexes).
    /// </summary>
    public static class CatalogDiff
    {
        /// <summary>
        /// Computes the changes from <paramref name="stored"/> to <paramref name="desired"/>.
        /// A `null` <paramref name="stored"/> means the table has no prior state (→ create it);
        /// a `null` <paramref name="desired"/> means the table is gone (→ drop it).
        /// </summary>
        /// <param name="desired">The desired table schema, or `null` if the table should not exist.</param>
        /// <param name="stored">The stored applied schema, or `null` on first contact.</param>
        /// <returns>The ordered change list; empty when the two sides are equivalent.</returns>
        public static IReadOnlyList<CatalogChange> Compare(CatalogTableDto desired, CatalogTableDto stored)
        {
            List<CatalogChange> changes = new List<CatalogChange>();

            if (desired == null && stored == null)
                return changes;
            if (stored == null)
            {
                changes.Add(CatalogChange.CreateTable(desired));
                return changes;
            }
            if (desired == null)
            {
                changes.Add(CatalogChange.DropTable(stored.Name));
                return changes;
            }

            string table = desired.Name;

            // Ordered buckets, concatenated safely at the end.
            List<CatalogChange> dropIndexes = new List<CatalogChange>();
            List<CatalogChange> dropColumns = new List<CatalogChange>();
            List<CatalogChange> alterColumns = new List<CatalogChange>();
            List<CatalogChange> addColumns = new List<CatalogChange>();
            List<CatalogChange> addIndexes = new List<CatalogChange>();

            Dictionary<string, CatalogColumnDto> storedColumns = ByColumnName(stored.Columns);
            Dictionary<string, CatalogColumnDto> desiredColumns = ByColumnName(desired.Columns);

            // Drops: stored columns absent from desired (stored declaration order).
            for (int i = 0; i < stored.Columns.Count; i++)
            {
                CatalogColumnDto s = stored.Columns[i];
                if (!desiredColumns.ContainsKey(s.Name))
                    dropColumns.Add(IsGeometry(s)
                        ? CatalogChange.DropGeometryColumn(table, s)
                        : CatalogChange.DropColumn(table, s));
            }

            // Adds and per-column changes (desired declaration order).
            for (int i = 0; i < desired.Columns.Count; i++)
            {
                CatalogColumnDto d = desired.Columns[i];
                if (!storedColumns.TryGetValue(d.Name, out CatalogColumnDto s))
                {
                    // Brand-new column: the add carries its own single-column and nested indexes.
                    addColumns.Add(IsGeometry(d)
                        ? CatalogChange.AddGeometryColumn(table, d)
                        : CatalogChange.AddColumn(table, d));
                    continue;
                }

                DiffExistingColumn(table, d, s, dropIndexes, dropColumns, alterColumns, addColumns, addIndexes);
            }

            DiffCompositeIndexes(table, desired, stored, dropIndexes, addIndexes);

            // The dynamic-property side table is dropped before the owner's other changes and created
            // after them (it references the owner's primary key).
            if (stored.HasDynamicProperties && !desired.HasDynamicProperties)
                changes.Add(CatalogChange.DropDynamicPropertiesTable(table));

            changes.AddRange(dropIndexes);
            changes.AddRange(dropColumns);
            changes.AddRange(alterColumns);
            changes.AddRange(addColumns);
            changes.AddRange(addIndexes);

            if (desired.HasDynamicProperties && !stored.HasDynamicProperties)
                changes.Add(CatalogChange.AddDynamicPropertiesTable(table));
            return changes;
        }

        // Diffs a column that exists on both sides. Handles a change of column "family" (plain /
        // geometry / json) as a drop+add, an in-place definition change as an alter, and the index
        // deltas (single-column flags, spatial, JSON) as their own add/drop steps.
        private static void DiffExistingColumn(string table, CatalogColumnDto desired, CatalogColumnDto stored,
            List<CatalogChange> dropIndexes, List<CatalogChange> dropColumns,
            List<CatalogChange> alterColumns, List<CatalogChange> addColumns, List<CatalogChange> addIndexes)
        {
            // A change of column family (plain <-> geometry <-> json) is not an in-place alter: replace.
            if (Family(desired) != Family(stored))
            {
                dropColumns.Add(IsGeometry(stored)
                    ? CatalogChange.DropGeometryColumn(table, stored)
                    : CatalogChange.DropColumn(table, stored));
                addColumns.Add(IsGeometry(desired)
                    ? CatalogChange.AddGeometryColumn(table, desired)
                    : CatalogChange.AddColumn(table, desired));
                return;
            }

            if (IsGeometry(desired))
            {
                // Incompatible geometry metadata (SRID/subtype/Z/M/nullability) is not portably
                // alterable in place: replace the column. Its spatial indexes ride the drop+add.
                if (!GeometryScalarEquals(desired.Geometry, stored.Geometry))
                {
                    dropColumns.Add(CatalogChange.DropGeometryColumn(table, stored));
                    addColumns.Add(CatalogChange.AddGeometryColumn(table, desired));
                    return;
                }
                DiffSpatialIndexes(table, desired.Name, desired.Geometry, stored.Geometry, dropIndexes, addIndexes);
                return;
            }

            // Plain and JSON columns share the same scalar definition compare; a real difference is an
            // in-place alter.
            if (!ColumnDefinitionEquals(desired, stored))
                alterColumns.Add(CatalogChange.AlterColumn(table, desired, stored));

            // Single-column index flags on an existing column.
            DiffSingleColumnIndex(table, desired.Name, desired.Sorted, stored.Sorted, false, dropIndexes, addIndexes);
            DiffSingleColumnIndex(table, desired.Name, desired.Unique, stored.Unique, true, dropIndexes, addIndexes);

            if (IsJson(desired))
                DiffJsonIndexes(table, desired.Name, desired.Json, stored.Json, dropIndexes, addIndexes);
        }

        private static void DiffSingleColumnIndex(string table, string columnName, bool desired, bool stored, bool unique,
            List<CatalogChange> dropIndexes, List<CatalogChange> addIndexes)
        {
            if (desired == stored)
                return;
            if (desired)
                addIndexes.Add(CatalogChange.AddIndex(table, columnName, unique));
            else
                dropIndexes.Add(CatalogChange.DropIndex(table, columnName, unique));
        }

        private static void DiffSpatialIndexes(string table, string columnName, CatalogGeometryDto desired, CatalogGeometryDto stored,
            List<CatalogChange> dropIndexes, List<CatalogChange> addIndexes)
        {
            Dictionary<string, CatalogSpatialIndexDto> storedByName = new Dictionary<string, CatalogSpatialIndexDto>(StringComparer.Ordinal);
            for (int i = 0; i < stored.Indexes.Count; i++)
                storedByName[stored.Indexes[i].Name] = stored.Indexes[i];
            Dictionary<string, CatalogSpatialIndexDto> desiredByName = new Dictionary<string, CatalogSpatialIndexDto>(StringComparer.Ordinal);
            for (int i = 0; i < desired.Indexes.Count; i++)
                desiredByName[desired.Indexes[i].Name] = desired.Indexes[i];

            for (int i = 0; i < stored.Indexes.Count; i++)
            {
                CatalogSpatialIndexDto s = stored.Indexes[i];
                if (!desiredByName.TryGetValue(s.Name, out CatalogSpatialIndexDto d) || !SpatialIndexEquals(d, s))
                    dropIndexes.Add(CatalogChange.DropSpatialIndex(table, columnName, s));
            }
            for (int i = 0; i < desired.Indexes.Count; i++)
            {
                CatalogSpatialIndexDto d = desired.Indexes[i];
                if (!storedByName.TryGetValue(d.Name, out CatalogSpatialIndexDto s) || !SpatialIndexEquals(d, s))
                    addIndexes.Add(CatalogChange.AddSpatialIndex(table, columnName, d));
            }
        }

        // Both columns are guaranteed to be JSON here (the caller only dispatches after the column
        // family matches), so desired/stored and their index lists are non-null.
        private static void DiffJsonIndexes(string table, string columnName, CatalogJsonDto desired, CatalogJsonDto stored,
            List<CatalogChange> dropIndexes, List<CatalogChange> addIndexes)
        {
            List<CatalogJsonIndexDto> desiredIdx = desired.Indexes;
            List<CatalogJsonIndexDto> storedIdx = stored.Indexes;

            Dictionary<string, CatalogJsonIndexDto> storedByName = new Dictionary<string, CatalogJsonIndexDto>(StringComparer.Ordinal);
            for (int i = 0; i < storedIdx.Count; i++)
                storedByName[storedIdx[i].Name] = storedIdx[i];
            Dictionary<string, CatalogJsonIndexDto> desiredByName = new Dictionary<string, CatalogJsonIndexDto>(StringComparer.Ordinal);
            for (int i = 0; i < desiredIdx.Count; i++)
                desiredByName[desiredIdx[i].Name] = desiredIdx[i];

            for (int i = 0; i < storedIdx.Count; i++)
            {
                CatalogJsonIndexDto s = storedIdx[i];
                if (!desiredByName.TryGetValue(s.Name, out CatalogJsonIndexDto d) || !JsonIndexEquals(d, s))
                    dropIndexes.Add(CatalogChange.DropJsonIndex(table, columnName, s));
            }
            for (int i = 0; i < desiredIdx.Count; i++)
            {
                CatalogJsonIndexDto d = desiredIdx[i];
                if (!storedByName.TryGetValue(d.Name, out CatalogJsonIndexDto s) || !JsonIndexEquals(d, s))
                    addIndexes.Add(CatalogChange.AddJsonIndex(table, columnName, d));
            }
        }

        private static void DiffCompositeIndexes(string table, CatalogTableDto desired, CatalogTableDto stored,
            List<CatalogChange> dropIndexes, List<CatalogChange> addIndexes)
        {
            Dictionary<string, CatalogCompositeIndexDto> storedByName = new Dictionary<string, CatalogCompositeIndexDto>(StringComparer.Ordinal);
            for (int i = 0; i < stored.CompositeIndexes.Count; i++)
                storedByName[stored.CompositeIndexes[i].Name] = stored.CompositeIndexes[i];
            Dictionary<string, CatalogCompositeIndexDto> desiredByName = new Dictionary<string, CatalogCompositeIndexDto>(StringComparer.Ordinal);
            for (int i = 0; i < desired.CompositeIndexes.Count; i++)
                desiredByName[desired.CompositeIndexes[i].Name] = desired.CompositeIndexes[i];

            for (int i = 0; i < stored.CompositeIndexes.Count; i++)
            {
                CatalogCompositeIndexDto s = stored.CompositeIndexes[i];
                if (!desiredByName.TryGetValue(s.Name, out CatalogCompositeIndexDto d) || !CompositeIndexEquals(d, s))
                    dropIndexes.Add(CatalogChange.DropCompositeIndex(table, s));
            }
            for (int i = 0; i < desired.CompositeIndexes.Count; i++)
            {
                CatalogCompositeIndexDto d = desired.CompositeIndexes[i];
                if (!storedByName.TryGetValue(d.Name, out CatalogCompositeIndexDto s) || !CompositeIndexEquals(d, s))
                    addIndexes.Add(CatalogChange.AddCompositeIndex(table, d));
            }
        }

        private static Dictionary<string, CatalogColumnDto> ByColumnName(List<CatalogColumnDto> columns)
        {
            Dictionary<string, CatalogColumnDto> map = new Dictionary<string, CatalogColumnDto>(StringComparer.Ordinal);
            for (int i = 0; i < columns.Count; i++)
                map[columns[i].Name] = columns[i];
            return map;
        }

        private static bool IsGeometry(CatalogColumnDto column) => column.Geometry != null;

        private static bool IsJson(CatalogColumnDto column) => column.Json != null;

        // The column "family" - a change of family is a replace, not an alter.
        private static int Family(CatalogColumnDto column)
        {
            if (column.Geometry != null)
                return 1;
            if (column.Json != null)
                return 2;
            return 0;
        }

        // The DDL-relevant scalar definition of a plain/JSON column. Excludes index flags (Sorted /
        // Unique - handled as index changes), IgnoreRead (not DDL) and Id (identity is the SQL name).
        private static bool ColumnDefinitionEquals(CatalogColumnDto a, CatalogColumnDto b)
        {
            return string.Equals(a.DbType, b.DbType, StringComparison.Ordinal)
                && a.Size == b.Size
                && a.Precision == b.Precision
                && a.PrimaryKey == b.PrimaryKey
                && a.Autoincrement == b.Autoincrement
                && a.Nullable == b.Nullable
                && string.Equals(a.ForeignTable, b.ForeignTable, StringComparison.Ordinal)
                && DefaultEquals(a.Default, b.Default);
        }

        private static bool DefaultEquals(CatalogColumnDefault a, CatalogColumnDefault b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            return string.Equals(a.TypeName, b.TypeName, StringComparison.Ordinal)
                && string.Equals(a.Value, b.Value, StringComparison.Ordinal);
        }

        // Called only for two geometry columns (family already matched), so both are non-null.
        private static bool GeometryScalarEquals(CatalogGeometryDto a, CatalogGeometryDto b)
        {
            return a.Srid == b.Srid
                && string.Equals(a.Subtype, b.Subtype, StringComparison.Ordinal)
                && a.HasZ == b.HasZ
                && a.HasM == b.HasM
                && a.Nullable == b.Nullable;
        }

        private static bool SpatialIndexEquals(CatalogSpatialIndexDto a, CatalogSpatialIndexDto b)
        {
            return string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                && a.HasBoundingBox == b.HasBoundingBox
                && DoubleEquals(a.MinX, b.MinX)
                && DoubleEquals(a.MinY, b.MinY)
                && DoubleEquals(a.MaxX, b.MaxX)
                && DoubleEquals(a.MaxY, b.MaxY)
                && DoubleEquals(a.Tolerance, b.Tolerance);
        }

        // NaN is the "not declared" sentinel for bounding-box ordinates, so NaN must equal NaN here.
        private static bool DoubleEquals(double a, double b)
            => a.Equals(b);

        private static bool JsonIndexEquals(CatalogJsonIndexDto a, CatalogJsonIndexDto b)
        {
            return string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                && string.Equals(a.Path, b.Path, StringComparison.Ordinal)
                && string.Equals(a.DbType, b.DbType, StringComparison.Ordinal)
                && a.Unique == b.Unique;
        }

        // Called only when the two indexes share a name (matched by name upstream), so name equality is
        // a given; shape equality is what matters here.
        private static bool CompositeIndexEquals(CatalogCompositeIndexDto a, CatalogCompositeIndexDto b)
        {
            if (!StringListEquals(a.ExcludeFor, b.ExcludeFor))
                return false;
            if (a.Fields.Count != b.Fields.Count)
                return false;
            for (int i = 0; i < a.Fields.Count; i++)
                if (!CompositeFieldEquals(a.Fields[i], b.Fields[i]))
                    return false;
            return true;
        }

        private static bool CompositeFieldEquals(CatalogCompositeIndexFieldDto a, CatalogCompositeIndexFieldDto b)
        {
            return string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                && string.Equals(a.Function, b.Function, StringComparison.Ordinal)
                && string.Equals(a.Direction, b.Direction, StringComparison.Ordinal)
                && string.Equals(a.JsonPath, b.JsonPath, StringComparison.Ordinal)
                && string.Equals(a.JsonType, b.JsonType, StringComparison.Ordinal);
        }

        private static bool StringListEquals(List<string> a, List<string> b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            return true;
        }
    }
}
