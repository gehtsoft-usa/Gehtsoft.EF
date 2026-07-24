using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Deep, DB-free tests of the two-form geometry read surface: projecting a geometry column in its
    /// <see cref="GeometryValueForm.Native"/> form (the raw column, for a server-side operand) versus the
    /// default <see cref="GeometryValueForm.Wkb"/> form (wrapped in the WKB output function, for a client
    /// read), and feeding a native-form subquery as the operand of a geometry predicate (no
    /// <c>ST_GeomFromWKB</c> constructor wrap, unlike the bound-parameter form). The dummy dialect renders
    /// the portable OGC grammar; the generated statement is asserted by exact string. Aliases come from a
    /// process-global counter, so they are bound from the generated SQL rather than hard-coded.
    /// </summary>
    public class GeometryNativeFormSqlTest
    {
        private static TableDescriptor GeoTable(string name)
            => new TableDescriptor(name, new[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                new TableDescriptor.ColumnInfo
                {
                    Name = "shape", DbType = DbType.Binary, Nullable = true,
                    Geometry = new GeometryColumnMetadata(typeof(byte[]), 4326, GeometrySubtype.Point, false, false, true, System.Array.Empty<SpatialIndexDefinition>()),
                },
            });

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        // Reads the alias assigned to a specific table: "... <table> AS <alias> ..." -> "<alias>".
        private static string AliasAfter(string query, string table)
        {
            string anchor = table + " AS ";
            int start = query.IndexOf(anchor, System.StringComparison.Ordinal) + anchor.Length;
            int end = start;
            while (end < query.Length && query[end] != ' ' && query[end] != ')')
                end++;
            return query.Substring(start, end - start);
        }

        [Fact]
        public void Select_NativeForm_ProjectsRawColumn_NoOutputWrap()
        {
            using var connection = GeoConnection();
            var table = GeoTable("geo_rt");
            var select = connection.GetSelectQueryBuilder(table);
            select.AddGeometryValueToResultset(table["shape"], "shape", GeometryValueForm.Native);
            select.PrepareQuery();

            // query ends with "... FROM geo_rt AS <alias>"
            string alias = select.Query.Substring(select.Query.LastIndexOf(' ') + 1);
            select.Query.Should().Be($"SELECT {alias}.shape AS shape FROM geo_rt AS {alias}");
        }

        [Fact]
        public void Select_WkbForm_IsTheDefault_AndWrapsInOutputFunction()
        {
            using var connection = GeoConnection();
            var table = GeoTable("geo_rt");
            var select = connection.GetSelectQueryBuilder(table);
            // no form argument -> Wkb (the default, client-readable form)
            select.AddGeometryValueToResultset(table["shape"], "shape");
            select.PrepareQuery();

            string alias = select.Query.Substring(select.Query.LastIndexOf(' ') + 1);
            select.Query.Should().Be($"SELECT ST_AsBinary({alias}.shape) AS shape FROM geo_rt AS {alias}");
        }

        [Fact]
        public void Where_GeoPredicate_WithNativeSubqueryOperand_NoConstructorWrap()
        {
            using var connection = GeoConnection();

            // the subquery yields a NATIVE geometry (no output wrap), to be used directly as the operand
            var other = GeoTable("geo_other");
            var sub = connection.GetSelectQueryBuilder(other);
            sub.AddGeometryValueToResultset(other["shape"], "s", GeometryValueForm.Native);

            var table = GeoTable("geo_rt");
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(table["id"]);
            select.Where.GeoPredicate(SqlGeoPredicateId.Intersects, table["shape"], sub);
            select.PrepareQuery();

            string a = AliasAfter(select.Query, "geo_rt");
            string b = AliasAfter(select.Query, "geo_other");
            select.Query.Should().Be(
                $"SELECT {a}.id FROM geo_rt AS {a} WHERE ST_Intersects({a}.shape, (SELECT {b}.shape AS s FROM geo_other AS {b}))");
        }
    }
}
