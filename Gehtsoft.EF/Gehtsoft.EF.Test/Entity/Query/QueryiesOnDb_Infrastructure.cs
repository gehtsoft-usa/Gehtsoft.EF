using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Hime.SDK.Output;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class QueryiesOnDb_Infrastructure : IClassFixture<QueryiesOnDb_Infrastructure.Fixture>
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
                        Scope = "ifrstructure",
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

            var e = new EntityWithCallback
            {
                Value = "abcd",
                IntValue = 123
            };

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

        [Entity(Scope = "ifrstructure")]
        public class TestFunctionEntity
        {
            [PrimaryKey]
            public int ID { get; set; } = 1;

            [EntityProperty]
            public int IntValue { get; set; }

            [EntityProperty]
            public double RealValue { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string StringValue { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string StringValue1 { get; set; }

            [EntityProperty(Nullable = true)]
            public DateTime? DateValue { get; set; }
        }

        private IDisposable SetupFunctionTest(string connectionName, out SqlDbConnection connection)
        {
            var c = mFixture.GetInstance(connectionName);
            using (var query = c.GetDropEntityQuery<TestFunctionEntity>())
                query.Execute();
            using (var query = c.GetCreateEntityQuery<TestFunctionEntity>())
                query.Execute();

            connection = c;
            return new DelayedAction(() =>
            {
                using (var query = c.GetDropEntityQuery<TestFunctionEntity>())
                    query.Execute();
            });
        }

        private static void TestFunctionInsert(SqlDbConnection connection, string propertyName, object value)
        {
            var e = new TestFunctionEntity();
            e.GetType().GetProperty(propertyName).SetValue(e, value);
            using (var query = connection.GetInsertEntityQuery<TestFunctionEntity>())
                query.Execute(e);
        }

        private static T TestFunctionRead<T>(SqlDbConnection connection, string propertyName, SqlFunctionId functionId)
        {
            using (var query = connection.GetSelectEntitiesQueryBase<TestFunctionEntity>())
            {
                query.Where.Property(nameof(TestFunctionEntity.ID)).Eq(1);
                query.AddExpressionToResultset(connection.GetLanguageSpecifics().GetSqlFunction(
                    functionId, new[] { query.GetReference(propertyName).Alias }
                    ), DbType.Object, "fn");
                query.Execute();
                if (!query.ReadNext())
                    return default;

                return query.GetValue<T>(0);
            }
        }

        private static T TestFunctionRead<T>(SqlDbConnection connection, string propertyName, SqlFunctionId functionId, object arg2)
        {
            using (var query = connection.GetGenericSelectEntityQuery<TestFunctionEntity>())
            {
                query.Where.Property(nameof(TestFunctionEntity.ID)).Eq(1);
                query.AddExpressionToResultset(connection.GetLanguageSpecifics().GetSqlFunction(
                    functionId, new[] { query.GetReference(propertyName).Alias, arg2.ToString() }
                    ), DbType.Object, "fn");
                query.Execute();
                if (!query.ReadNext())
                    return default;

                return query.GetValue<T>(0);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Abs(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.IntValue), -5);
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.IntValue), SqlFunctionId.Abs)
                .Should().Be(5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_AbsReal(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.RealValue), -5.123);
            TestFunctionRead<double>(connection, nameof(TestFunctionEntity.RealValue), SqlFunctionId.Abs)
                .Should().Be(5.123);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Round1(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.RealValue), 5.123);
            TestFunctionRead<double>(connection, nameof(TestFunctionEntity.RealValue), SqlFunctionId.Round)
                .Should().Be(5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Round2(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.RealValue), 5.553);
            TestFunctionRead<double>(connection, nameof(TestFunctionEntity.RealValue), SqlFunctionId.Round)
                .Should().Be(6);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Round3(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.RealValue), 5.553);
            TestFunctionRead<double>(connection, nameof(TestFunctionEntity.RealValue), SqlFunctionId.Round, 1)
                .Should().Be(5.6);
            TestFunctionRead<double>(connection, nameof(TestFunctionEntity.RealValue), SqlFunctionId.Round, 2)
                .Should().Be(5.55);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Trim(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), " abcdef ");
            TestFunctionRead<string>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.Trim)
                .Should().Be("abcdef");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Left(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), "abcdef");
            TestFunctionRead<string>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.Left, 3)
                .Should().Be("abc");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_TrimLeft(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), " abcdef ");
            TestFunctionRead<string>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.TrimLeft)
                .Should().Be("abcdef ");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_TrimRight(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), " abcdef ");
            TestFunctionRead<string>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.TrimRight)
                .Should().Be(" abcdef");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Upper(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), "abcdef");
            TestFunctionRead<string>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.Upper)
                .Should().Be("ABCDEF");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Lower(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), "ABCDEF");
            TestFunctionRead<string>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.Lower)
                .Should().Be("abcdef");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_ToString(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.IntValue), 123);
            TestFunctionRead<string>(connection, nameof(TestFunctionEntity.IntValue), SqlFunctionId.ToString)
                .Should().Be("123");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_ToInt(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), "123");
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.ToInteger)
                .Should().Be(123);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_ToDouble(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.StringValue), "123.456");
            TestFunctionRead<double>(connection, nameof(TestFunctionEntity.StringValue), SqlFunctionId.ToDouble)
                .Should().Be(123.456);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Year(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.DateValue), new DateTime(2010, 11, 25));
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.DateValue), SqlFunctionId.Year)
                .Should().Be(2010);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Hour(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.DateValue), new DateTime(2010, 11, 25, 22, 45, 58));
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.DateValue), SqlFunctionId.Hour)
                .Should().Be(22);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Minute(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.DateValue), new DateTime(2010, 11, 25, 22, 45, 58));
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.DateValue), SqlFunctionId.Minute)
                .Should().Be(45);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Second(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.DateValue), new DateTime(2010, 11, 25, 22, 45, 58));
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.DateValue), SqlFunctionId.Second)
                .Should().Be(58);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Month(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.DateValue), new DateTime(2010, 11, 25));
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.DateValue), SqlFunctionId.Month)
                .Should().Be(11);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Day(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            TestFunctionInsert(connection, nameof(TestFunctionEntity.DateValue), new DateTime(2010, 11, 25));
            TestFunctionRead<int>(connection, nameof(TestFunctionEntity.DateValue), SqlFunctionId.Day)
                .Should().Be(25);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Functions_Concat(string connectionName)
        {
            using var finalizer = SetupFunctionTest(connectionName, out var connection);

            var e = new TestFunctionEntity()
            {
                StringValue = "abc",
                StringValue1 = "def"
            };

            using (var query = connection.GetInsertEntityQuery<TestFunctionEntity>())
                query.Execute(e);

            using (var query = connection.GetGenericSelectEntityQuery<TestFunctionEntity>())
            {
                query.Where.Property(nameof(TestFunctionEntity.ID)).Eq(1);
                query.AddExpressionToResultset(
                    connection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat,
                        new[]
                        {
                            query.GetReference(nameof(TestFunctionEntity.StringValue)).Alias,
                            query.GetReference(nameof(TestFunctionEntity.StringValue1)).Alias,
                        }),
                    DbType.String, "r");

                query.Execute();
                query.ReadNext();
                query.GetValue<string>(0).Should().Be("abcdef");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SqliteDate_StoreDateMode(bool mode)
        {
            SqliteGlobalOptions.StoreDateAsString.Should().BeFalse();
            using var delayedAction = new DelayedAction(() => SqliteGlobalOptions.StoreDateAsString = false);
            SqliteGlobalOptions.StoreDateAsString = mode;
            using var connection = SqliteDbConnectionFactory.CreateMemory();

            using (var query = connection.GetCreateEntityQuery<TestFunctionEntity>())
                query.Execute();

            var e = new TestFunctionEntity() { DateValue = new DateTime(2010, 05, 23, 22, 12, 55, DateTimeKind.Utc) };

            using (var query = connection.GetInsertEntityQuery<TestFunctionEntity>())
                query.Execute(e);

            using (var query = connection.GetSelectEntitiesQueryBase<TestFunctionEntity>())
            {
                var r = query.GetReference(nameof(TestFunctionEntity.DateValue));
                query.AddToResultset(nameof(TestFunctionEntity.DateValue), "dt");
                query.AddExpressionToResultset($"YEAR({r.Alias})", DbType.Int32, "y");
                query.AddExpressionToResultset($"MONTH({r.Alias})", DbType.Int32, "m");
                query.AddExpressionToResultset($"DAY({r.Alias})", DbType.Int32, "d");
                query.AddExpressionToResultset($"HOUR({r.Alias})", DbType.Int32, "h");
                query.AddExpressionToResultset($"MINUTE({r.Alias})", DbType.Int32, "n");
                query.AddExpressionToResultset($"SECOND({r.Alias})", DbType.Int32, "s");

                query.Execute();
                query.ReadNext();

                if (mode)
                    query.GetValue<string>(0).Should().Be("2010-05-23 22:12:55");
                else
                    query.GetValue<double>(0).Should().BeApproximately(e.DateValue.Value.ToOADate(), 1.0 / 86400.0);

                query.GetValue<int>(1).Should().Be(2010);
                query.GetValue<int>(2).Should().Be(05);
                query.GetValue<int>(3).Should().Be(23);
                query.GetValue<int>(4).Should().Be(22);
                query.GetValue<int>(5).Should().Be(12);
                query.GetValue<int>(6).Should().Be(55);
            }
        }
    }
}



