using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.Catalog;
using Gehtsoft.EF.Db.SqlDb.Catalog.Diff;
using Xunit;

namespace Gehtsoft.EF.Test.Catalog.Diff
{
    /// <summary>
    /// Pure in-memory tests (no DB) for the catalogue diff engine: create/drop table, add/drop/alter
    /// column, the single-column / composite / spatial / JSON index deltas, geometry-metadata and
    /// column-family replacements, the opaque-JSON-ClrType rule, deterministic ordering, and no-op on
    /// identical DTOs.
    /// </summary>
    public sealed class CatalogDiffTest
    {
        private static CatalogColumnDto Col(string name, string dbType = "String", int size = 32, bool nullable = true)
            => new CatalogColumnDto { Id = name, Name = name, DbType = dbType, Size = size, Nullable = nullable };

        private static CatalogTableDto Table(string name, params CatalogColumnDto[] columns)
        {
            CatalogTableDto dto = new CatalogTableDto { Name = name, Scope = "s" };
            for (int i = 0; i < columns.Length; i++)
                dto.Columns.Add(columns[i]);
            return dto;
        }

        private static CatalogColumnDto GeoCol(string name, int srid, string subtype = "Point", bool hasZ = false, bool hasM = false, params CatalogSpatialIndexDto[] indexes)
        {
            CatalogColumnDto c = new CatalogColumnDto { Id = name, Name = name, DbType = "Binary" };
            c.Geometry = new CatalogGeometryDto { Srid = srid, Subtype = subtype, HasZ = hasZ, HasM = hasM, Nullable = true };
            for (int i = 0; i < indexes.Length; i++)
                c.Geometry.Indexes.Add(indexes[i]);
            return c;
        }

        private static CatalogSpatialIndexDto SpatialIdx(string name)
            => new CatalogSpatialIndexDto { Name = name, HasBoundingBox = false, MinX = double.NaN, MinY = double.NaN, MaxX = double.NaN, MaxY = double.NaN, Tolerance = 0 };

        private static CatalogColumnDto JsonCol(string name, string clrType, params CatalogJsonIndexDto[] indexes)
        {
            CatalogColumnDto c = new CatalogColumnDto { Id = name, Name = name, DbType = "String" };
            c.Json = new CatalogJsonDto { ClrType = clrType };
            for (int i = 0; i < indexes.Length; i++)
                c.Json.Indexes.Add(indexes[i]);
            return c;
        }

        private static CatalogJsonIndexDto JsonIdx(string name, string path, string dbType = "Int32", bool unique = false)
            => new CatalogJsonIndexDto { Name = name, Path = path, DbType = dbType, Unique = unique };

        private static CatalogCompositeIndexDto CompositeIdx(string name, params string[] fields)
        {
            CatalogCompositeIndexDto idx = new CatalogCompositeIndexDto { Name = name };
            for (int i = 0; i < fields.Length; i++)
                idx.Fields.Add(new CatalogCompositeIndexFieldDto { Name = fields[i], Direction = "Asc" });
            return idx;
        }

        private static CatalogChange Single(IReadOnlyList<CatalogChange> changes)
        {
            changes.Count.Should().Be(1);
            return changes[0];
        }

        [Fact]
        public void IdenticalTables_ProduceNoChanges()
        {
            var a = Table("t", Col("id", "Int32", 0, false), Col("name"));
            var b = Table("t", Col("id", "Int32", 0, false), Col("name"));
            CatalogDiff.Compare(a, b).Should().BeEmpty();
        }

        [Fact]
        public void StoredNull_CreatesTable()
        {
            var desired = Table("t", Col("id"));
            CatalogChange change = Single(CatalogDiff.Compare(desired, null));
            change.Kind.Should().Be(CatalogChangeKind.CreateTable);
            change.Table.Should().BeSameAs(desired);
        }

        [Fact]
        public void DesiredNull_DropsTable()
        {
            var stored = Table("t", Col("id"));
            CatalogChange change = Single(CatalogDiff.Compare(null, stored));
            change.Kind.Should().Be(CatalogChangeKind.DropTable);
            change.TableName.Should().Be("t");
        }

        [Fact]
        public void GainDynamicProperties_ProducesAddDynamicPropertiesTable()
        {
            var stored = Table("t", Col("id", "Int32", 0, false));
            var desired = Table("t", Col("id", "Int32", 0, false));
            desired.HasDynamicProperties = true;
            CatalogChange change = Single(CatalogDiff.Compare(desired, stored));
            change.Kind.Should().Be(CatalogChangeKind.AddDynamicPropertiesTable);
            change.TableName.Should().Be("t");
        }

        [Fact]
        public void LoseDynamicProperties_ProducesDropDynamicPropertiesTable()
        {
            var stored = Table("t", Col("id", "Int32", 0, false));
            stored.HasDynamicProperties = true;
            var desired = Table("t", Col("id", "Int32", 0, false));
            CatalogChange change = Single(CatalogDiff.Compare(desired, stored));
            change.Kind.Should().Be(CatalogChangeKind.DropDynamicPropertiesTable);
            change.TableName.Should().Be("t");
        }

        [Fact]
        public void SameDynamicProperties_ProducesNoChange()
        {
            var stored = Table("t", Col("id", "Int32", 0, false));
            stored.HasDynamicProperties = true;
            var desired = Table("t", Col("id", "Int32", 0, false));
            desired.HasDynamicProperties = true;
            CatalogDiff.Compare(desired, stored).Should().BeEmpty();
        }

        [Fact]
        public void AddedColumn_ProducesAddColumn()
        {
            var stored = Table("t", Col("id"));
            var desired = Table("t", Col("id"), Col("name"));
            CatalogChange change = Single(CatalogDiff.Compare(desired, stored));
            change.Kind.Should().Be(CatalogChangeKind.AddColumn);
            change.Column.Name.Should().Be("name");
        }

        [Fact]
        public void RemovedColumn_ProducesDropColumn()
        {
            var stored = Table("t", Col("id"), Col("name"));
            var desired = Table("t", Col("id"));
            CatalogChange change = Single(CatalogDiff.Compare(desired, stored));
            change.Kind.Should().Be(CatalogChangeKind.DropColumn);
            change.Column.Name.Should().Be("name");
        }

        [Theory]
        [InlineData("String", 64, true, "String", 32, true)]     // size change
        [InlineData("String", 32, false, "String", 32, true)]    // nullability change
        [InlineData("Int64", 0, true, "Int32", 0, true)]         // type change
        public void ColumnDefinitionChange_ProducesAlterColumn(string dt1, int s1, bool n1, string dt2, int s2, bool n2)
        {
            var desired = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = dt1, Size = s1, Nullable = n1 });
            var stored = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = dt2, Size = s2, Nullable = n2 });
            CatalogChange change = Single(CatalogDiff.Compare(desired, stored));
            change.Kind.Should().Be(CatalogChangeKind.AlterColumn);
            change.Column.DbType.Should().Be(dt1);
            change.PreviousColumn.DbType.Should().Be(dt2);
        }

        [Fact]
        public void DefaultValueChange_ProducesAlterColumn()
        {
            var desired = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = new CatalogColumnDefault { TypeName = "Int32", Value = "1" } });
            var stored = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = new CatalogColumnDefault { TypeName = "Int32", Value = "2" } });
            Single(CatalogDiff.Compare(desired, stored)).Kind.Should().Be(CatalogChangeKind.AlterColumn);
        }

        [Fact]
        public void SingleColumnSortedFlag_TogglesIndex()
        {
            var withoutIdx = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Sorted = false });
            var withIdx = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Sorted = true });

            Single(CatalogDiff.Compare(withIdx, withoutIdx)).Kind.Should().Be(CatalogChangeKind.AddIndex);
            Single(CatalogDiff.Compare(withoutIdx, withIdx)).Kind.Should().Be(CatalogChangeKind.DropIndex);
        }

        [Fact]
        public void UniqueFlag_TogglesUniqueIndex()
        {
            var plain = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Unique = false });
            var unique = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Unique = true });

            CatalogChange add = Single(CatalogDiff.Compare(unique, plain));
            add.Kind.Should().Be(CatalogChangeKind.AddIndex);
            add.Unique.Should().BeTrue();
        }

        [Fact]
        public void CompositeIndex_AddAndDrop()
        {
            var none = Table("t", Col("a"), Col("b"));
            var with = Table("t", Col("a"), Col("b"));
            with.CompositeIndexes.Add(CompositeIdx("ix_ab", "a", "b"));

            Single(CatalogDiff.Compare(with, none)).Kind.Should().Be(CatalogChangeKind.AddCompositeIndex);
            Single(CatalogDiff.Compare(none, with)).Kind.Should().Be(CatalogChangeKind.DropCompositeIndex);
        }

        [Fact]
        public void CompositeIndexShapeChange_ProducesDropThenAdd()
        {
            var desired = Table("t", Col("a"), Col("b"));
            desired.CompositeIndexes.Add(CompositeIdx("ix", "a", "b"));
            var stored = Table("t", Col("a"), Col("b"));
            stored.CompositeIndexes.Add(CompositeIdx("ix", "a"));   // same name, different fields

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropCompositeIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddCompositeIndex);
        }

        [Fact]
        public void SpatialIndex_AddAndDrop_OnUnchangedGeometry()
        {
            var without = Table("t", GeoCol("g", 4326));
            var with = Table("t", GeoCol("g", 4326, indexes: SpatialIdx("sx_g")));

            CatalogChange add = Single(CatalogDiff.Compare(with, without));
            add.Kind.Should().Be(CatalogChangeKind.AddSpatialIndex);
            add.ColumnName.Should().Be("g");

            Single(CatalogDiff.Compare(without, with)).Kind.Should().Be(CatalogChangeKind.DropSpatialIndex);
        }

        [Fact]
        public void GeometryMetadataChange_ReplacesColumn()
        {
            var desired = Table("t", GeoCol("g", 3857));
            var stored = Table("t", GeoCol("g", 4326));

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropGeometryColumn);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddGeometryColumn);
        }

        [Fact]
        public void GeometryColumn_AddAndDrop()
        {
            var without = Table("t", Col("id"));
            var with = Table("t", Col("id"), GeoCol("g", 4326));

            Single(CatalogDiff.Compare(with, without)).Kind.Should().Be(CatalogChangeKind.AddGeometryColumn);
            Single(CatalogDiff.Compare(without, with)).Kind.Should().Be(CatalogChangeKind.DropGeometryColumn);
        }

        [Fact]
        public void JsonIndex_AddAndDrop()
        {
            var without = Table("t", JsonCol("doc", "MyDoc"));
            var with = Table("t", JsonCol("doc", "MyDoc", JsonIdx("jx", "$.age")));

            Single(CatalogDiff.Compare(with, without)).Kind.Should().Be(CatalogChangeKind.AddJsonIndex);
            Single(CatalogDiff.Compare(without, with)).Kind.Should().Be(CatalogChangeKind.DropJsonIndex);
        }

        [Fact]
        public void JsonClrTypeChange_IsIgnored()
        {
            // ClrType is opaque (RS2): a different backing CLR type is not a DDL change.
            var desired = Table("t", JsonCol("doc", "NewDoc", JsonIdx("jx", "$.age")));
            var stored = Table("t", JsonCol("doc", "OldDoc", JsonIdx("jx", "$.age")));
            CatalogDiff.Compare(desired, stored).Should().BeEmpty();
        }

        [Fact]
        public void ColumnFamilyChange_ReplacesColumn()
        {
            var desired = Table("t", GeoCol("c", 4326));       // geometry
            var stored = Table("t", Col("c", "String", 32));   // plain

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropColumn);          // old plain column
            changes[1].Kind.Should().Be(CatalogChangeKind.AddGeometryColumn);   // new geometry column
        }

        [Fact]
        public void BothNull_ProducesNoChanges()
        {
            CatalogDiff.Compare(null, null).Should().BeEmpty();
        }

        [Fact]
        public void SpatialIndexShapeChange_SameName_ProducesDropThenAdd()
        {
            var stored = Table("t", GeoCol("g", 4326, indexes: SpatialIdx("sx")));
            var changedIdx = new CatalogSpatialIndexDto { Name = "sx", HasBoundingBox = true, MinX = 0, MinY = 0, MaxX = 10, MaxY = 10, Tolerance = 0.5 };
            var desired = Table("t", GeoCol("g", 4326, indexes: changedIdx));

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropSpatialIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddSpatialIndex);
        }

        [Fact]
        public void SpatialIndexUnchanged_SameNameAndShape_IsNoOp()
        {
            var stored = Table("t", GeoCol("g", 4326, indexes: SpatialIdx("sx")));
            var desired = Table("t", GeoCol("g", 4326, indexes: SpatialIdx("sx")));
            CatalogDiff.Compare(desired, stored).Should().BeEmpty();
        }

        [Theory]
        [InlineData("$.age", "$.year", "Int32", "Int32", false, false)]   // path change
        [InlineData("$.age", "$.age", "Int64", "Int32", false, false)]    // value type change
        [InlineData("$.age", "$.age", "Int32", "Int32", true, false)]     // uniqueness change
        public void JsonIndexShapeChange_SameName_ProducesDropThenAdd(string p1, string p2, string t1, string t2, bool u1, bool u2)
        {
            var desired = Table("t", JsonCol("doc", "Doc", JsonIdx("jx", p1, t1, u1)));
            var stored = Table("t", JsonCol("doc", "Doc", JsonIdx("jx", p2, t2, u2)));

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropJsonIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddJsonIndex);
        }

        [Fact]
        public void CompositeIndexFieldChange_SameFieldCount_ProducesDropThenAdd()
        {
            // Same name, same field count, but a field's direction differs -> exercises the field compare.
            var desired = Table("t", Col("a"), Col("b"));
            var d = new CatalogCompositeIndexDto { Name = "ix" };
            d.Fields.Add(new CatalogCompositeIndexFieldDto { Name = "a", Direction = "Asc" });
            d.Fields.Add(new CatalogCompositeIndexFieldDto { Name = "b", Direction = "Desc" });
            desired.CompositeIndexes.Add(d);

            var stored = Table("t", Col("a"), Col("b"));
            var s = new CatalogCompositeIndexDto { Name = "ix" };
            s.Fields.Add(new CatalogCompositeIndexFieldDto { Name = "a", Direction = "Asc" });
            s.Fields.Add(new CatalogCompositeIndexFieldDto { Name = "b", Direction = "Asc" });
            stored.CompositeIndexes.Add(s);

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropCompositeIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddCompositeIndex);
        }

        [Fact]
        public void CompositeIndexIdenticalFields_IsNoOp()
        {
            var desired = Table("t", Col("a"), Col("b"));
            desired.CompositeIndexes.Add(CompositeIdx("ix", "a", "b"));
            var stored = Table("t", Col("a"), Col("b"));
            stored.CompositeIndexes.Add(CompositeIdx("ix", "a", "b"));
            CatalogDiff.Compare(desired, stored).Should().BeEmpty();
        }

        [Fact]
        public void CompositeIndexExcludeForChange_ProducesDropThenAdd()
        {
            var desired = Table("t", Col("a"));
            var d = CompositeIdx("ix", "a");
            d.ExcludeFor = new List<string> { "mysql" };
            desired.CompositeIndexes.Add(d);

            var stored = Table("t", Col("a"));
            var s = CompositeIdx("ix", "a");
            s.ExcludeFor = new List<string> { "oracle" };
            stored.CompositeIndexes.Add(s);

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropCompositeIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddCompositeIndex);
        }

        [Fact]
        public void CompositeIndexSameExcludeFor_IsNoOp()
        {
            var desired = Table("t", Col("a"));
            var d = CompositeIdx("ix", "a");
            d.ExcludeFor = new List<string> { "mysql" };
            desired.CompositeIndexes.Add(d);

            var stored = Table("t", Col("a"));
            var s = CompositeIdx("ix", "a");
            s.ExcludeFor = new List<string> { "mysql" };
            stored.CompositeIndexes.Add(s);

            CatalogDiff.Compare(desired, stored).Should().BeEmpty();
        }

        [Fact]
        public void CompositeIndexExcludeForCountChange_ProducesDropThenAdd()
        {
            var desired = Table("t", Col("a"));
            var d = CompositeIdx("ix", "a");
            d.ExcludeFor = new List<string> { "mysql", "oracle" };
            desired.CompositeIndexes.Add(d);

            var stored = Table("t", Col("a"));
            var s = CompositeIdx("ix", "a");
            s.ExcludeFor = new List<string> { "mysql" };
            stored.CompositeIndexes.Add(s);

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropCompositeIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddCompositeIndex);
        }

        [Fact]
        public void CompositeIndexExcludeForAdded_ProducesDropThenAdd()
        {
            var desired = Table("t", Col("a"));
            var d = CompositeIdx("ix", "a");
            d.ExcludeFor = new List<string> { "mysql" };   // gained an exclusion
            desired.CompositeIndexes.Add(d);

            var stored = Table("t", Col("a"));
            stored.CompositeIndexes.Add(CompositeIdx("ix", "a"));   // ExcludeFor == null

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropCompositeIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddCompositeIndex);
        }

        [Fact]
        public void CompositeIndexExcludeForRemoved_ProducesDropThenAdd()
        {
            var desired = Table("t", Col("a"));
            desired.CompositeIndexes.Add(CompositeIdx("ix", "a"));   // ExcludeFor == null

            var stored = Table("t", Col("a"));
            var s = CompositeIdx("ix", "a");
            s.ExcludeFor = new List<string> { "mysql" };   // lost an exclusion
            stored.CompositeIndexes.Add(s);

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropCompositeIndex);
            changes[1].Kind.Should().Be(CatalogChangeKind.AddCompositeIndex);
        }

        [Fact]
        public void DefaultTypeChange_ProducesAlterColumn()
        {
            var desired = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int64", Default = new CatalogColumnDefault { TypeName = "Int64", Value = "0" } });
            var stored = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int64", Default = new CatalogColumnDefault { TypeName = "Int32", Value = "0" } });
            Single(CatalogDiff.Compare(desired, stored)).Kind.Should().Be(CatalogChangeKind.AlterColumn);
        }

        [Fact]
        public void DefaultAdded_ProducesAlterColumn()
        {
            var desired = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = new CatalogColumnDefault { TypeName = "Int32", Value = "0" } });
            var stored = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = null });
            Single(CatalogDiff.Compare(desired, stored)).Kind.Should().Be(CatalogChangeKind.AlterColumn);
        }

        [Fact]
        public void DefaultRemoved_ProducesAlterColumn()
        {
            var desired = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = null });
            var stored = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = new CatalogColumnDefault { TypeName = "Int32", Value = "0" } });
            Single(CatalogDiff.Compare(desired, stored)).Kind.Should().Be(CatalogChangeKind.AlterColumn);
        }

        [Fact]
        public void NoDefaultBothSides_IsNoOp()
        {
            var desired = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = null });
            var stored = Table("t", new CatalogColumnDto { Id = "c", Name = "c", DbType = "Int32", Default = null });
            CatalogDiff.Compare(desired, stored).Should().BeEmpty();
        }

        [Fact]
        public void ColumnFamilyChange_GeometryToPlain_ReplacesColumn()
        {
            var desired = Table("t", Col("c", "String", 32));   // plain
            var stored = Table("t", GeoCol("c", 4326));         // geometry

            var changes = CatalogDiff.Compare(desired, stored);
            changes.Count.Should().Be(2);
            changes[0].Kind.Should().Be(CatalogChangeKind.DropGeometryColumn);   // old geometry column
            changes[1].Kind.Should().Be(CatalogChangeKind.AddColumn);            // new plain column
        }

        [Fact]
        public void MixedChanges_AreOrdered_DropsColumnsAltersAddsThenIndexes()
        {
            // stored: id, gone(plain), keep(with sorted idx), comp index on (id)
            var stored = Table("t",
                Col("id", "Int32", 0, false),
                Col("gone"),
                new CatalogColumnDto { Id = "keep", Name = "keep", DbType = "Int32", Sorted = true });
            stored.CompositeIndexes.Add(CompositeIdx("ix_old", "id"));

            // desired: id, keep(no idx, but altered type), added(new), comp index on (id, added)
            var desired = Table("t",
                Col("id", "Int32", 0, false),
                new CatalogColumnDto { Id = "keep", Name = "keep", DbType = "Int64", Sorted = false },
                Col("added"));
            desired.CompositeIndexes.Add(CompositeIdx("ix_new", "id", "added"));

            var changes = CatalogDiff.Compare(desired, stored);

            // Expected buckets in order: drop index (keep sorted), drop composite (ix_old),
            // drop column (gone), alter column (keep), add column (added), add composite (ix_new).
            var kinds = new List<CatalogChangeKind>();
            for (int i = 0; i < changes.Count; i++)
                kinds.Add(changes[i].Kind);

            // Drops (indexes) come before column drops, which come before alters, adds, then add-indexes.
            int lastDropIndex = kinds.LastIndexOf(CatalogChangeKind.DropIndex);
            int dropComposite = kinds.IndexOf(CatalogChangeKind.DropCompositeIndex);
            int dropColumn = kinds.IndexOf(CatalogChangeKind.DropColumn);
            int alter = kinds.IndexOf(CatalogChangeKind.AlterColumn);
            int addColumn = kinds.IndexOf(CatalogChangeKind.AddColumn);
            int addComposite = kinds.IndexOf(CatalogChangeKind.AddCompositeIndex);

            lastDropIndex.Should().BeGreaterThanOrEqualTo(0);
            dropComposite.Should().BeGreaterThanOrEqualTo(0);
            dropColumn.Should().BeGreaterThan(dropComposite);
            alter.Should().BeGreaterThan(dropColumn);
            addColumn.Should().BeGreaterThan(alter);
            addComposite.Should().BeGreaterThan(addColumn);
        }
    }
}
