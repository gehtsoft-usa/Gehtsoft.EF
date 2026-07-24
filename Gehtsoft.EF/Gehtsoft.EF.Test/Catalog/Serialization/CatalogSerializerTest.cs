using System;
using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Catalog;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Catalog.Serialization
{
    /// <summary>
    /// Pure in-memory tests (no DB) for the catalogue serializer: round-trip fidelity across every
    /// column flavour, enum-by-name storage, backward tolerance (missing members -> defaults),
    /// forward tolerance (unknown members ignored) and the newer-than-supported version flag.
    /// </summary>
    public sealed class CatalogSerializerTest
    {
        private static readonly DateTime SampleDate = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        private sealed class ProbeDoc
        {
            public int Age { get; set; }
        }

        // A descriptor exercising every DDL-relevant column/index flavour the DTO must preserve.
        private static TableDescriptor BuildProbeTable()
        {
            TableDescriptor other = new TableDescriptor("other_table") { Scope = "cat" };
            other.Add(new TableDescriptor.ColumnInfo() { ID = "Id", Name = "id", DbType = DbType.Int32, PrimaryKey = true });

            TableDescriptor table = new TableDescriptor("catalog_probe") { Scope = "cat", View = false, Obsolete = false };

            table.Add(new TableDescriptor.ColumnInfo() { ID = "Id", Name = "id", DbType = DbType.Int64, PrimaryKey = true, Autoincrement = true, Nullable = false });
            table.Add(new TableDescriptor.ColumnInfo() { ID = "Name", Name = "name", DbType = DbType.String, Size = 64, Unique = true, Sorted = true, Nullable = false });
            table.Add(new TableDescriptor.ColumnInfo() { ID = "Age", Name = "age", DbType = DbType.Int32, Nullable = true });
            table.Add(new TableDescriptor.ColumnInfo() { ID = "Score", Name = "score", DbType = DbType.Double, Size = 10, Precision = 2 });
            table.Add(new TableDescriptor.ColumnInfo() { ID = "Ref", Name = "ref", DbType = DbType.Int32, ForeignTable = other });
            table.Add(new TableDescriptor.ColumnInfo() { ID = "Flag", Name = "flag", DbType = DbType.Boolean, DefaultValue = true });
            table.Add(new TableDescriptor.ColumnInfo() { ID = "Created", Name = "created", DbType = DbType.DateTime, DefaultValue = SampleDate });
            table.Add(new TableDescriptor.ColumnInfo() { ID = "Ignored", Name = "ignored", DbType = DbType.String, Size = 16, IgnoreRead = true });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "Geo",
                Name = "geo",
                DbType = DbType.Binary,
                Geometry = new GeometryColumnMetadata(typeof(byte[]), 4326, GeometrySubtype.Point, true, true, true,
                    new List<SpatialIndexDefinition> { new SpatialIndexDefinition("gidx", true, 0, 0, 10, 10, 0.5) }),
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "Doc",
                Name = "doc",
                DbType = DbType.String,
                Json = new JsonColumnMetadata(typeof(ProbeDoc),
                    new List<JsonIndexDefinition> { new JsonIndexDefinition("$.age", DbType.Int32, false, "doc_age") }),
            });

            return table;
        }

        private static List<CompositeIndex> BuildIndexes()
        {
            var byName = new CompositeIndex("by_name");
            byName.Add("name", SortDir.Asc);

            var functional = new CompositeIndex("upper_name") { ExcludeFor = new[] { "mssql", "mysql" } };
            functional.Add(SqlFunctionId.Upper, "name", SortDir.Desc);

            CompositeIndex json = CompositeIndex.ForJson("doc_city", "doc", "$.city", DbType.String);

            return new List<CompositeIndex> { byName, functional, json };
        }

        private static CatalogSnapshot Snapshot(CatalogTableDto table) => new CatalogSnapshot { Table = table };

        [Fact]
        public void RoundTrip_IsStable_AndLossless()
        {
            var serializer = new CatalogSerializer();
            CatalogTableDto dto = serializer.FromDescriptor(BuildProbeTable(), BuildIndexes());

            string json1 = serializer.Serialize(Snapshot(dto));
            CatalogSnapshot restored = serializer.Deserialize(json1);
            string json2 = serializer.Serialize(Snapshot(restored.Table));

            // Deterministic + lossless: a second round trip reproduces byte-identical JSON.
            json2.Should().Be(json1);
            restored.SchemaFormatVersion.Should().Be(CatalogSerializer.CurrentVersion);
            restored.IsNewerThanSupported.Should().BeFalse();
        }

        [Fact]
        public void RoundTrip_PreservesEveryFieldFlavour()
        {
            var serializer = new CatalogSerializer();
            CatalogTableDto dto = serializer.FromDescriptor(BuildProbeTable(), BuildIndexes());
            CatalogTableDto table = serializer.Deserialize(serializer.Serialize(Snapshot(dto))).Table;

            table.Name.Should().Be("catalog_probe");
            table.Scope.Should().Be("cat");
            table.Columns.Should().HaveCount(10);

            CatalogColumnDto id = table.Columns[0];
            id.Name.Should().Be("id");
            id.DbType.Should().Be("Int64");
            id.PrimaryKey.Should().BeTrue();
            id.Autoincrement.Should().BeTrue();

            CatalogColumnDto name = table.Columns[1];
            name.DbType.Should().Be("String");
            name.Size.Should().Be(64);
            name.Unique.Should().BeTrue();
            name.Sorted.Should().BeTrue();

            table.Columns[3].Precision.Should().Be(2);
            table.Columns[4].ForeignTable.Should().Be("other_table");

            CatalogColumnDto flag = table.Columns[5];
            flag.Default.Should().NotBeNull();
            flag.Default.TypeName.Should().Be("Boolean");
            flag.Default.Value.Should().Be("True");

            CatalogColumnDto created = table.Columns[6];
            created.Default.TypeName.Should().Be("DateTime");
            created.Default.Value.Should().Be(SampleDate.ToString("o", System.Globalization.CultureInfo.InvariantCulture));

            table.Columns[7].IgnoreRead.Should().BeTrue();

            CatalogGeometryDto geo = table.Columns[8].Geometry;
            geo.Should().NotBeNull();
            geo.Srid.Should().Be(4326);
            geo.Subtype.Should().Be("Point");
            geo.HasZ.Should().BeTrue();
            geo.HasM.Should().BeTrue();
            geo.Indexes.Should().HaveCount(1);
            geo.Indexes[0].Name.Should().Be("gidx");
            geo.Indexes[0].HasBoundingBox.Should().BeTrue();
            geo.Indexes[0].MaxX.Should().Be(10);
            geo.Indexes[0].Tolerance.Should().Be(0.5);

            CatalogJsonDto doc = table.Columns[9].Json;
            doc.Should().NotBeNull();
            doc.ClrType.Should().Contain("ProbeDoc");
            doc.Indexes.Should().HaveCount(1);
            doc.Indexes[0].Path.Should().Be("$.age");
            doc.Indexes[0].DbType.Should().Be("Int32");

            table.CompositeIndexes.Should().HaveCount(3);
            table.CompositeIndexes[0].Fields[0].Name.Should().Be("name");
            table.CompositeIndexes[0].Fields[0].Direction.Should().Be("Asc");

            CatalogCompositeIndexDto functional = table.CompositeIndexes[1];
            functional.ExcludeFor.Should().BeEquivalentTo(new[] { "mssql", "mysql" });
            functional.Fields[0].Function.Should().Be("Upper");
            functional.Fields[0].Direction.Should().Be("Desc");

            CatalogCompositeIndexFieldDto jsonField = table.CompositeIndexes[2].Fields[0];
            jsonField.JsonPath.Should().Be("$.city");
            jsonField.JsonType.Should().Be("String");
        }

        [Fact]
        public void Serialize_StoresEnumsByName_AndStampsVersion()
        {
            var serializer = new CatalogSerializer();
            CatalogTableDto dto = serializer.FromDescriptor(BuildProbeTable(), BuildIndexes());

            string json = serializer.Serialize(Snapshot(dto));

            json.Should().Contain("\"schemaFormatVersion\":1");
            json.Should().Contain("\"dbType\":\"Int64\"");   // enum by NAME, not its integer
            json.Should().NotContain("\"dbType\":0");
        }

        [Fact]
        public void Deserialize_MissingMembers_GetDefaults()
        {
            var serializer = new CatalogSerializer();
            string json = "{\"schemaFormatVersion\":1,\"table\":{\"name\":\"t\"}}";

            CatalogSnapshot snapshot = serializer.Deserialize(json);

            snapshot.Table.Name.Should().Be("t");
            snapshot.Table.Scope.Should().BeNull();
            snapshot.Table.View.Should().BeFalse();
            snapshot.Table.Obsolete.Should().BeFalse();
            snapshot.Table.Columns.Should().NotBeNull().And.BeEmpty();
            snapshot.Table.CompositeIndexes.Should().NotBeNull().And.BeEmpty();
            snapshot.IsNewerThanSupported.Should().BeFalse();
        }

        [Fact]
        public void Deserialize_UnknownMembers_AreIgnored()
        {
            var serializer = new CatalogSerializer();
            string json = "{\"schemaFormatVersion\":1,\"table\":{\"name\":\"t\",\"futureThing\":123,\"columns\":[{\"name\":\"c\",\"dbType\":\"Int32\",\"somethingNew\":true}]}}";

            CatalogSnapshot snapshot = serializer.Deserialize(json);

            snapshot.Table.Name.Should().Be("t");
            snapshot.Table.Columns.Should().HaveCount(1);
            snapshot.Table.Columns[0].DbType.Should().Be("Int32");
            snapshot.IsNewerThanSupported.Should().BeFalse();
        }

        [Fact]
        public void Deserialize_NewerVersion_IsFlagged_ButDoesNotThrow()
        {
            var serializer = new CatalogSerializer();
            string json = "{\"schemaFormatVersion\":999,\"table\":{\"name\":\"t\"}}";

            CatalogSnapshot snapshot = serializer.Deserialize(json);

            snapshot.SchemaFormatVersion.Should().Be(999);
            snapshot.IsNewerThanSupported.Should().BeTrue();
        }

        [Fact]
        public void Serialize_NullArgument_Throws()
        {
            var serializer = new CatalogSerializer();
            Action act = () => serializer.Serialize(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void FromDescriptor_NullIndexes_YieldsNoCompositeIndexes()
        {
            var serializer = new CatalogSerializer();
            CatalogTableDto dto = serializer.FromDescriptor(BuildProbeTable(), null);
            dto.CompositeIndexes.Should().BeEmpty();
            dto.Columns.Should().HaveCount(10);
        }
    }
}
