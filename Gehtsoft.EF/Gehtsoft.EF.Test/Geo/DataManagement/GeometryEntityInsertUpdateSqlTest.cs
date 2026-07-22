using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataManagement
{
    /// <summary>
    /// DB-free proof that the <b>entity</b> insert/update query path inherits the pure-SQL geometry auto-wrap
    /// (Increment 1) with no entity-layer code: the entity builders delegate to the pure-SQL
    /// <c>InsertQueryBuilder</c>/<c>UpdateQueryBuilder</c> over the entity's <c>TableDescriptor</c>, whose
    /// geometry column carries the <c>Geometry</c> metadata, so the VALUES / SET side of a geometry column is
    /// the dialect constructor <c>ST_GeomFromWKB(@col, srid)</c> while a plain column stays a bare parameter.
    /// The dummy dialect renders the portable OGC grammar; per-driver renderings are covered by
    /// GeometryRenderTest and the round-trip acceptance tests. (Asserted by substring because the test SQL
    /// grammar has no function-valued INSERT/UPDATE rule to parse into an AST - the same reason
    /// GeometryValueWrapTest asserts exact strings.)
    /// </summary>
    public class GeometryEntityInsertUpdateSqlTest
    {
        [Entity(Scope = "geo_ent_sql", Table = "geo_ent_sql")]
        public class GeoEntSql
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name")]
            public string Name { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        [Fact]
        public void EntityInsert_WrapsGeometryColumn_LeavesPlainColumnBare()
        {
            using var connection = GeoConnection();
            using var insert = connection.GetInsertEntityQuery<GeoEntSql>();
            insert.Builder.PrepareQuery();
            string sql = insert.Builder.Query;

            sql.Should().Contain("ST_GeomFromWKB(@shape, 4326)");
            sql.Should().Contain("@name");
            sql.Should().NotContain("ST_GeomFromWKB(@name");
        }

        [Fact]
        public void EntityUpdate_WrapsGeometryColumn_LeavesPlainColumnBare()
        {
            using var connection = GeoConnection();
            using var update = connection.GetUpdateEntityQuery<GeoEntSql>();
            update.Builder.PrepareQuery();
            string sql = update.Builder.Query;

            sql.Should().Contain("shape=ST_GeomFromWKB(@shape, 4326)");
            sql.Should().Contain("name=@name");
        }
    }
}
