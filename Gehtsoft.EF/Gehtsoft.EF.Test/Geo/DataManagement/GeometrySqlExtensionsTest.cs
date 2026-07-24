using System;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataManagement
{
    /// <summary>
    /// Tests the NTS SQL-builder-layer helpers (<see cref="GeometrySqlExtensions"/>): binding an NTS
    /// geometry as WKB and reading it back from a plain byte[] column round-trips through the codec, a null
    /// geometry maps to SQL NULL and back to a null object, and every helper guards a null query argument.
    /// The round-trip stores raw WKB in an ordinary blob column (no spatial engine required), so it runs on
    /// plain in-memory SQLite.
    /// </summary>
    public class GeometrySqlExtensionsTest
    {
        [Fact]
        public void BindGeometryParam_NullQuery_Throws()
        {
            SqlDbQuery q = null;
            ((Action)(() => q.BindGeometryParam("p", new Point(1, 2)))).Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void GetGeometry_NullQuery_Throws()
        {
            SqlDbQuery q = null;
            ((Action)(() => q.GetGeometry(0))).Should().Throw<ArgumentNullException>();
            ((Action)(() => q.GetGeometry("shape"))).Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void BindAndGetGeometry_PlainBlobRoundTrip()
        {
            var id = new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true };
            var shape = new TableDescriptor.ColumnInfo { Name = "shape", DbType = DbType.Binary, Nullable = true };
            var table = new TableDescriptor("geo_sql_ext_rt", new[] { id, shape });

            using var connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetQuery(connection.GetCreateTableBuilder(table)))
                q.ExecuteNoData();

            var insert = connection.GetInsertQueryBuilder(table);
            insert.ReturnAutoincrement = false;
            using (var q = connection.GetQuery(insert))
            {
                q.BindGeometryParam("shape", new Point(3, 4));
                q.ExecuteNoData();
            }
            using (var q = connection.GetQuery(insert))
            {
                q.BindGeometryParam("shape", null); // SQL NULL
                q.ExecuteNoData();
            }

            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(shape, "shape");
            select.AddOrderBy(id);
            using (var q = connection.GetQuery(select))
            {
                q.ExecuteReader();

                q.ReadNext().Should().BeTrue();
                Geometry g = q.GetGeometry(0, 4326);
                g.Should().BeOfType<Point>();
                ((Point)g).X.Should().Be(3);
                ((Point)g).Y.Should().Be(4);

                q.ReadNext().Should().BeTrue();
                q.GetGeometry("shape", 4326).Should().BeNull();
            }
        }
    }
}
