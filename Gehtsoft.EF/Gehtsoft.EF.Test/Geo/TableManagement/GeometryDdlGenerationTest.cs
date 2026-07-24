using System;
using System.Data;
using System.Text;
using AwesomeAssertions;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.MysqlDb;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.PostgresDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.TableManagement
{
    /// <summary>
    /// Deep, DB-free tests of the per-driver geometry column / spatial-index DDL generation and the
    /// capability gate. (Behavioural create/drop against a live engine is covered by the acceptance tier.)
    /// </summary>
    public class GeometryDdlGenerationTest
    {
        private static TableDescriptor.ColumnInfo GeoColumn(string name, int srid, GeometrySubtype subtype,
            bool hasZ, bool hasM, bool nullable, params SpatialIndexDefinition[] indexes)
            => new TableDescriptor.ColumnInfo
            {
                Name = name,
                DbType = DbType.Binary,
                Nullable = nullable,
                Geometry = new GeometryColumnMetadata(typeof(byte[]), srid, subtype, hasZ, hasM, nullable, indexes),
            };

        private static TableDescriptor.ColumnInfo InTable(string table, TableDescriptor.ColumnInfo column)
        {
            // constructing the descriptor wires column.Table
            _ = new TableDescriptor(table, new[] { column });
            return column;
        }

        private static SpatialIndexDefinition Box(string name)
            => new SpatialIndexDefinition(name, true, -180, -90, 180, 90, 0.005);

        private static SpatialIndexDefinition NoBox(string name)
            => new SpatialIndexDefinition(name, false, double.NaN, double.NaN, double.NaN, double.NaN, 0.005);

        [Fact]
        public void Postgres_Column_RendersTypmodWithDimensionAndSrid()
        {
            var col = GeoColumn("shape", 4326, GeometrySubtype.Point, hasZ: true, hasM: true, nullable: false);
            new PostgresDbLanguageSpecifics().GeometryColumnDDL(col).Should().Be("geometry(PointZM,4326) NOT NULL");
        }

        [Fact]
        public void Postgres_Column_AnySubtype_Nullable()
        {
            var col = GeoColumn("shape", 3857, GeometrySubtype.Geometry, false, false, nullable: true);
            new PostgresDbLanguageSpecifics().GeometryColumnDDL(col).Should().Be("geometry(Geometry,3857)");
        }

        [Fact]
        public void Mssql_Column_IsGeometry()
        {
            var col = GeoColumn("shape", 4326, GeometrySubtype.Point, false, false, nullable: false);
            new MssqlDbLanguageSpecifics().GeometryColumnDDL(col).Should().Be("geometry NOT NULL");
        }

        [Fact]
        public void Oracle_Column_IsSdoGeometry()
        {
            var col = GeoColumn("shape", 4326, GeometrySubtype.Point, false, false, nullable: true);
            new OracleDbLanguageSpecifics().GeometryColumnDDL(col).Should().Be("SDO_GEOMETRY");
        }

        [Fact]
        public void Mysql_Column_NotNullSrid_WhenIndexed()
        {
            var col = GeoColumn("shape", 4326, GeometrySubtype.Polygon, false, false, nullable: true, NoBox("shape_sidx"));
            new MySql8LanguageSpecifics().GeometryColumnDDL(col).Should().Be("POLYGON NOT NULL SRID 4326");
        }

        [Fact]
        public void Mysql_Column_RejectsZorM()
        {
            var col = GeoColumn("shape", 4326, GeometrySubtype.Point, hasZ: true, hasM: false, nullable: false);
            ((Action)(() => new MySql8LanguageSpecifics().GeometryColumnDDL(col)))
                .Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
        }

        [Fact]
        public void MariaDb_Column_OmitsSrid()
        {
            // MariaDB has no SRID column attribute; the dialect must not emit it.
            var col = GeoColumn("shape", 4326, GeometrySubtype.Polygon, false, false, nullable: true, NoBox("shape_sidx"));
            new MariaDbLanguageSpecifics().GeometryColumnDDL(col).Should().Be("POLYGON NOT NULL");
        }

        [Fact]
        public void Mssql_SpatialIndex_UsesGeometryGridAndBoundingBox()
        {
            var col = InTable("geo_t", GeoColumn("shape", 4326, GeometrySubtype.Point, false, false, false, Box("shape_sidx")));
            var builder = new MssqlTableDdlBuilder(new MssqlDbLanguageSpecifics());
            var sb = new StringBuilder();

            builder.HandleGeometryAfterQuery(sb, col);
            string sql = sb.ToString();

            sql.Should().Contain("CREATE SPATIAL INDEX");
            sql.Should().Contain("USING GEOMETRY_GRID");
            sql.Should().Contain("BOUNDING_BOX = (-180, -90, 180, 90)");
        }

        [Fact]
        public void Mssql_SpatialIndex_WithoutBoundingBox_Throws()
        {
            var col = InTable("geo_t", GeoColumn("shape", 4326, GeometrySubtype.Point, false, false, false, NoBox("shape_sidx")));
            var builder = new MssqlTableDdlBuilder(new MssqlDbLanguageSpecifics());

            ((Action)(() => builder.HandleGeometryAfterQuery(new StringBuilder(), col)))
                .Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
        }

        [Fact]
        public void GeometryColumn_OnUnsupportedDialect_ThrowsOnCreate()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometry.Should().BeFalse();

            var table = new TableDescriptor("geo_doc", new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                GeoColumn("shape", 4326, GeometrySubtype.Point, false, false, true),
            });

            var builder = connection.GetCreateTableBuilder(table);
            ((Action)(() => builder.PrepareQuery()))
                .Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
        }
    }
}
