using System;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.MysqlDb;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.PostgresDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Discovery
{
    /// <summary>
    /// Coverage for float/Single and short/Int16 support that was previously missing
    /// from the entity-to-column discovery ladder and the per-driver DDL type mapping.
    /// `float` is treated exactly like `double` (same SQL type), `short` like the other
    /// integers.
    /// </summary>
    public class SingleAndShortTypeTest
    {
        [Entity(Scope = "single_support", Table = "single_support")]
        public class NumericAutoEntity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public short ShortValue { get; set; }

            [EntityProperty(Nullable = true)]
            public short? NullableShort { get; set; }

            [EntityProperty]
            public float FloatValue { get; set; }

            [EntityProperty(Nullable = true)]
            public float? NullableFloat { get; set; }
        }

        [Fact]
        public void TypeToDb_MapsFloatAndShort()
        {
            var specifics = new SqliteDbLanguageSpecifics();

            specifics.TypeToDb(typeof(float), out var f).Should().BeTrue();
            f.Should().Be(DbType.Single);

            specifics.TypeToDb(typeof(float?), out var nf).Should().BeTrue();
            nf.Should().Be(DbType.Single);

            specifics.TypeToDb(typeof(short), out var s).Should().BeTrue();
            s.Should().Be(DbType.Int16);
        }

        [Fact]
        public void Discovery_AutoDetectsFloatAndShort()
        {
            var table = AllEntities.Inst[typeof(NumericAutoEntity)].TableDescriptor;

            var shortColumn = table["ShortValue"];
            shortColumn.DbType.Should().Be(DbType.Int16);

            var nullableShort = table["NullableShort"];
            nullableShort.DbType.Should().Be(DbType.Int16);
            nullableShort.Nullable.Should().BeTrue();

            // float follows the same pattern as double: Single, default size 18 / precision 7
            var floatColumn = table["FloatValue"];
            floatColumn.DbType.Should().Be(DbType.Single);
            floatColumn.Size.Should().Be(18);
            floatColumn.Precision.Should().Be(7);

            var nullableFloat = table["NullableFloat"];
            nullableFloat.DbType.Should().Be(DbType.Single);
            nullableFloat.Nullable.Should().BeTrue();
        }

        [Fact]
        public void Single_DdlType_MirrorsDouble_AcrossAllDrivers()
        {
            SqlDbLanguageSpecifics[] drivers =
            {
                new SqliteDbLanguageSpecifics(),
                new MssqlDbLanguageSpecifics(),
                new MysqlDbLanguageSpecifics(),
                new OracleDbLanguageSpecifics(),
                new PostgresDbLanguageSpecifics(),
            };

            (int size, int precision)[] shapes = { (0, 0), (0, 4), (18, 7) };

            foreach (var driver in drivers)
            {
                foreach (var (size, precision) in shapes)
                {
                    driver.TypeName(DbType.Single, size, precision, false)
                        .Should().Be(driver.TypeName(DbType.Double, size, precision, false),
                            because: $"{driver.GetType().Name} must map Single like Double (size={size}, precision={precision})");
                }
            }
        }

        [Fact]
        public void Sqlite_RoundTrips_FloatAndShort()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();

            using (var q = connection.GetCreateEntityQuery<NumericAutoEntity>())
                q.Execute();

            var entity = new NumericAutoEntity
            {
                ShortValue = 12345,
                NullableShort = null,
                FloatValue = 3.5f,
                NullableFloat = 1.25f,
            };

            using (var q = connection.GetInsertEntityQuery<NumericAutoEntity>())
                q.Execute(entity);

            NumericAutoEntity read;
            using (var q = connection.GetSelectEntitiesQuery<NumericAutoEntity>())
            {
                q.Execute();
                read = q.ReadOne<NumericAutoEntity>();
            }

            read.Should().NotBeNull();
            read.ShortValue.Should().Be((short)12345);
            read.NullableShort.Should().BeNull();
            read.FloatValue.Should().Be(3.5f);
            read.NullableFloat.Should().Be(1.25f);
        }
    }
}
