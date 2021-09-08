using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class QueryiesOnDb_Infrastructure: IClassFixture<QueryiesOnDb_Infrastructure.Fixture>
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.ConnectionNames(flags, mFlags);

        private readonly Fixture mFixture;

        public QueryiesOnDb_Infrastructure(Fixture fixture)
        {
            mFixture = fixture;
        }

        public class Fixture : ConnectionFixtureBase
        {
        }

        public class TestDataTypeEntity : DynamicEntity
        {
            public override EntityAttribute EntityAttribute
            {
                get
                {
                    return new EntityAttribute()
                    {
                        Scope = "updateentity0",
                        Table = "dynamictest",
                    };
                }
            }

            private static Type mPropertyType;
            private static DbType mColumnType;
            private static bool mNullable;
            private static int mSize, mPrecision;

            public static void Configure(Type propertyType, DbType columnType, int size, int precision, bool nullable)
            {
                mPropertyType = propertyType;
                mColumnType = columnType;
                mNullable = nullable;
                mSize = size;
                mPrecision = precision;
            }

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties()
            {
                yield return new DynamicEntityProperty()
                {
                    EntityPropertyAttribute = new EntityPropertyAttribute()
                    {
                        Field = "f",
                        DbType = mColumnType,
                        Size = mSize,
                        Precision = mPrecision,
                        Nullable = mNullable
                    },
                    Name = "F",
                    PropertyType = mPropertyType
                };
            }
        }

        private static IEnumerable<object[]> TestDataTypeArgs(string connection)
        {
            yield return new object[] { DbType.String, 32, 0, true, typeof(string), "the text" };
            yield return new object[] { DbType.String, 32, 0, true, typeof(string), null };

            yield return new object[] { DbType.Boolean, 0, 0, false, typeof(bool), true };
            yield return new object[] { DbType.Boolean, 0, 0, false, typeof(bool), false };
            yield return new object[] { DbType.Boolean, 32, 0, true, typeof(bool?), null };

            yield return new object[] { DbType.Guid, 0, 0, false, typeof(Guid), "f5cab275-1181-47be-a1a3-ca9a804867bf" };
            yield return new object[] { DbType.Guid, 0, 0, true, typeof(Guid?), null };

            yield return new object[] { DbType.Date, 0, 0, true, typeof(DateTime), "2010-11-24" };
            yield return new object[] { DbType.Date, 32, 0, true, typeof(DateTime?), null };

            yield return new object[] { DbType.DateTime, 0, 0, true, typeof(DateTime), "2020-05-27 10:45:38" };
            yield return new object[] { DbType.DateTime, 32, 0, true, typeof(DateTime?), null };

            yield return new object[] { DbType.Int32, 0, 0, false, typeof(int), 5 };
            yield return new object[] { DbType.Int32, 0, 0, false, typeof(int), int.MaxValue };
            yield return new object[] { DbType.Int32, 0, 0, false, typeof(int), int.MinValue };
            yield return new object[] { DbType.Int32, 0, 0, true, typeof(int?), null };

            yield return new object[] { DbType.Int64, 0, 0, false, typeof(long), long.MaxValue };
            yield return new object[] { DbType.Int64, 0, 0, false, typeof(long), long.MinValue };
            yield return new object[] { DbType.Int64, 0, 0, true, typeof(long?), null };

            yield return new object[] { DbType.Double, 0, 0, false, typeof(double), 1.234 };
            if (!connection.Equals("oracle", StringComparison.OrdinalIgnoreCase))
            {
                yield return new object[] { DbType.Double, 0, 0, false, typeof(double), 1234567891234568.12 };
                yield return new object[] { DbType.Double, 0, 0, false, typeof(double), Double.MaxValue };
            }
            yield return new object[] { DbType.Double, 0, 0, true, typeof(double?), null };

            yield return new object[] { DbType.Decimal, 0, 3, false, typeof(decimal), 1.234m };

            if (connection.Equals("oracle", StringComparison.OrdinalIgnoreCase) ||
                connection.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
                yield return new object[] { DbType.Decimal, 0, 2, false, typeof(decimal), 1_234_567_891_234.12m };
            else
                yield return new object[] { DbType.Decimal, 0, 2, false, typeof(decimal), 12_345_678_912_345.12m };

            yield return new object[] { DbType.Decimal, 0, 0, true, typeof(decimal?), null };

            yield return new object[] { DbType.Binary, 32, 0, true, typeof(byte[]), new byte[] { 1, 2, 3, 4, 5 } };
            yield return new object[] { DbType.Binary, 32, 0, true, typeof(byte[]), null };
        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.ConnectionNamesWithArgs),
            "", typeof(QueryiesOnDb_Infrastructure), nameof(TestDataTypeArgs),
            MemberType = typeof(SqlConnectionSources))]
        public void TestDataType(string connectionName, DbType dbType, int size, int precision, bool nullable, Type dataType, object value)
        {
            value = TestValue.Translate(dataType, value);
            AllEntities.Inst.ForgetType(typeof(TestDataTypeEntity));
            TestDataTypeEntity.Configure(dataType, dbType, size, precision, nullable);
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetDropEntityQuery<TestDataTypeEntity>())
                query.Execute();

            using (var query = connection.GetCreateEntityQuery<TestDataTypeEntity>())
                query.Execute();

            using (var query = connection.GetInsertEntityQuery<TestDataTypeEntity>())
            {
                dynamic e = new TestDataTypeEntity();
                e.F = value;
                query.Execute(e);
            }

            using (var query = connection.GetSelectEntitiesQuery<TestDataTypeEntity>())
            {
                dynamic e = query.ReadOne<TestDataTypeEntity>();
                ((object)e.F).Should().BeEquivalentTo(value);
            }
        }

        [Entity(Scope = "updateentity0")]
        public class EntityWithCallback : IEntitySerializationCallback
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Size = 32)]
            public string Value { get; set; }

            [EntityProperty(Size = 32)]
            public string ValueUpperCase { get; protected set; }

            [EntityProperty]
            public int IntValue { get; set; }

            public int DoubleIntValue { get; protected set; }

            public void AfterDeserealization(SelectEntitiesQueryBase query)
            {
                DoubleIntValue = IntValue * 2;
            }

            public void BeforeSerialization(Db.SqlDb.SqlDbConnection connection)
            {
                ValueUpperCase = Value?.ToUpper(CultureInfo.InvariantCulture);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SerializationCallback(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetDropEntityQuery<EntityWithCallback>())
                query.Execute();

            using (var query = connection.GetCreateEntityQuery<EntityWithCallback>())
                query.Execute();

            var e = new EntityWithCallback();

            e.Value = "abcd";
            e.IntValue = 123;

            using (var query = connection.GetInsertEntityQuery<EntityWithCallback>())
                query.Execute(e);

            using (var query = connection.GetSelectEntitiesQuery<EntityWithCallback>())
            {
                query.Where.Property(nameof(EntityWithCallback.ID)).Eq(e.ID);
                var e1 = query.ReadOne<EntityWithCallback>();
                e1.Value.Should().Be("abcd");
                e1.ValueUpperCase.Should().Be("ABCD");
                e1.IntValue.Should().Be(123);
                e1.DoubleIntValue.Should().Be(246);
            }

            e.Value = "def";
            e.IntValue = 12;

            using (var query = connection.GetUpdateEntityQuery<EntityWithCallback>())
                query.Execute(e);

            using (var query = connection.GetSelectEntitiesQuery<EntityWithCallback>())
            {
                query.Where.Property(nameof(EntityWithCallback.ID)).Eq(e.ID);
                var e1 = query.ReadOne<EntityWithCallback>();
                e1.Value.Should().Be("def");
                e1.ValueUpperCase.Should().Be("DEF");
                e1.IntValue.Should().Be(12);
                e1.DoubleIntValue.Should().Be(24);
            }
        }
    }
}

