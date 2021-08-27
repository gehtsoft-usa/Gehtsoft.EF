using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Microsoft.OData.UriParser;
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Binders
{
    public class UpdateBinder
    {
        public enum TestEnum
        {
            V0 = 0,
            V1 = 1,
            V2 = 2,
            V3 = 3
        }

        [Entity(Scope = "updatebinder")]
        public class Dict
        {
            [PrimaryKey]
            public string ID { get; set; }
            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "updatebinder")]
        public class Entity1
        {
            [AutoId]
            public int ID { get; set; }
            [EntityProperty]
            public short F1 { get; set; }
            [EntityProperty]
            public TestEnum E1 { get; set; }
            [EntityProperty]
            public int F2 { get; set; }
            [EntityProperty]
            public long F3 { get; set; }
            [EntityProperty]
            public double F4 { get; set; }
            [EntityProperty]
            public decimal F5 { get; set; }
            [EntityProperty]
            public DateTime F6 { get; set; }
            [EntityProperty]
            public TimeSpan F7 { get; set; }
            [EntityProperty]
            public bool F8 { get; set; }
            [EntityProperty]
            public Guid F9 { get; set; }
            [EntityProperty]
            public byte[] F10 { get; set; }
            [EntityProperty(Size = 10)]
            public string F11 { get; set; }
            [EntityProperty]
            public int? F12 { get; set; }
            [ForeignKey]
            public Dict F13 { get; set; }
        }

        [Fact]
        public void BindInsert()
        {
            var td = AllEntities.Get<Entity1>().TableDescriptor;
            var binder = new UpdateQueryToTypeBinder(typeof(Entity1));
            binder.AutoBind(td);

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;

            dbquery.ReturnReader = new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection()
                {
                    new DummyDbDataReaderResult()
                    {
                        Columns = new DummyDbDataReaderColumnCollection()
                        {
                            { "", DbType.Int32 }
                        },
                        Data = new DummyDbDataReaderColumnDataRows()
                        {
                            { 123 }
                        }
                    }
                }
            };

            var guid = Guid.Parse("82f1a6ad-83f8-44f4-8136-eb58d20fe1d1");

            var e = new Entity1()
            {
                F1 = short.MaxValue,
                F2 = int.MaxValue,
                F3 = long.MaxValue,
                F4 = 1.234,
                F5 = 4.567m,
                F6 = new DateTime(1995, 4, 26),
                F7 = new TimeSpan(5, 27, 13),
                F8 = true,
                F9 = guid,
                F10 = new byte[] { 1, 2, 3 },
                F11 = "abcdef",
                F12 = null,
                F13 = new Dict() { ID = "dictid", Name = "dictvalue" }
            };

            binder.BindAndExecute(query, e, true);

            dbquery.Parameters.Count.Should().Be(14);

            var p = dbquery.Parameters["@f1"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int16);
            p.Value.Should().Be(e.F1);

            p = dbquery.Parameters["@f2"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int32);
            p.Value.Should().Be(e.F2);

            p = dbquery.Parameters["@f3"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int64);
            p.Value.Should().Be(e.F3);

            p = dbquery.Parameters["@f4"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Double);
            p.Value.Should().Be(e.F4);

            p = dbquery.Parameters["@f5"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Decimal);
            p.Value.Should().Be(e.F5);

            p = dbquery.Parameters["@f6"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.DateTime);
            p.Value.Should().Be(e.F6);

            p = dbquery.Parameters["@f7"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Time);
            p.Value.Should().Be(e.F7);

            p = dbquery.Parameters["@f8"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be("1");

            p = dbquery.Parameters["@f9"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be("82f1a6ad-83f8-44f4-8136-eb58d20fe1d1");

            p = dbquery.Parameters["@f10"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Binary);
            p.Value.Should().Be(e.F10);

            p = dbquery.Parameters["@f11"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be(e.F11);

            p = dbquery.Parameters["@f12"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int32);
            p.Value.Should().Be(DBNull.Value);

            p = dbquery.Parameters["@f13"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be("dictid");

            e.ID.Should().Be(123);
        }

        [Fact]
        public void BindUpdate()
        {
            var td = AllEntities.Get<Entity1>().TableDescriptor;
            var binder = new UpdateQueryToTypeBinder(typeof(Entity1));
            binder.AutoBind(td);

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;

            var guid = Guid.Parse("82f1a6ad-83f8-44f4-8136-eb58d20fe1d1");

            var e = new Entity1()
            {
                ID = 123,
                F1 = short.MaxValue,
                F2 = int.MaxValue,
                F3 = long.MaxValue,
                F4 = 1.234,
                F5 = 4.567m,
                F6 = new DateTime(1995, 4, 26),
                F7 = new TimeSpan(5, 27, 13),
                F8 = true,
                F9 = guid,
                F10 = new byte[] { 1, 2, 3 },
                F11 = "abcdef",
                F12 = null,
                F13 = new Dict() { ID = "dictid", Name = "dictvalue" }
            };

            binder.BindAndExecute(query, e, false);

            dbquery.Parameters.Count.Should().Be(15);

            var p = dbquery.Parameters["@f1"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int16);
            p.Value.Should().Be(e.F1);

            p = dbquery.Parameters["@f2"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int32);
            p.Value.Should().Be(e.F2);

            p = dbquery.Parameters["@f3"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int64);
            p.Value.Should().Be(e.F3);

            p = dbquery.Parameters["@f4"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Double);
            p.Value.Should().Be(e.F4);

            p = dbquery.Parameters["@f5"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Decimal);
            p.Value.Should().Be(e.F5);

            p = dbquery.Parameters["@f6"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.DateTime);
            p.Value.Should().Be(e.F6);

            p = dbquery.Parameters["@f7"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Time);
            p.Value.Should().Be(e.F7);

            p = dbquery.Parameters["@f8"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be("1");

            p = dbquery.Parameters["@f9"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be("82f1a6ad-83f8-44f4-8136-eb58d20fe1d1");

            p = dbquery.Parameters["@f10"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Binary);
            p.Value.Should().Be(e.F10);

            p = dbquery.Parameters["@f11"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be(e.F11);

            p = dbquery.Parameters["@f12"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int32);
            p.Value.Should().Be(DBNull.Value);

            p = dbquery.Parameters["@f13"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.String);
            p.Value.Should().Be("dictid");

            p = dbquery.Parameters["@id"];
            p.Should().NotBeNull();
            p.Direction.Should().Be(ParameterDirection.Input);
            p.DbType.Should().Be(DbType.Int32);
            p.Value.Should().Be(123);
        }

        [Theory]
        [InlineData(DbType.String, typeof(string), 3, "a", "a")]
        [InlineData(DbType.String, typeof(string), 3, "abc", "abc")]
        [InlineData(DbType.String, typeof(string), 3, "abcdef", "abc")]
        [InlineData(DbType.Double, typeof(double), 5, 123.0, 123.0)]
        [InlineData(DbType.Double, typeof(double), 5, -123.0, -123.0)]
        [InlineData(DbType.Double, typeof(double), 5, 12345.0, 12345.0)]
        [InlineData(DbType.Double, typeof(double), 5, -12345.0, -12345.0)]
        [InlineData(DbType.Double, typeof(double), 5, 123456.0, 99999.0)]
        [InlineData(DbType.Double, typeof(double), 5, -123456.0, -99999.0)]
        [InlineData(DbType.Double, typeof(int), 5, -123456, -99999)]
        [InlineData(DbType.Int16, typeof(short), 3, 123, 123)]
        [InlineData(DbType.Int16, typeof(short), 3, -123, -123)]
        [InlineData(DbType.Int16, typeof(short), 3, 1234, 999)]
        [InlineData(DbType.Int16, typeof(short), 3, -12345, -999)]
        [InlineData(DbType.Int32, typeof(int), 6, 123, 123)]
        [InlineData(DbType.Int32, typeof(int), 6, -123, -123)]
        [InlineData(DbType.Int32, typeof(int), 6, 123456, 123456)]
        [InlineData(DbType.Int32, typeof(int), 6, -123456, -123456)]
        [InlineData(DbType.Int32, typeof(int), 6, 1234567, 999999)]
        [InlineData(DbType.Int32, typeof(int), 6, -1234567, -999999)]
        [InlineData(DbType.Int64, typeof(long), 6, 123, 123)]
        [InlineData(DbType.Int64, typeof(long), 6, -123, -123)]
        [InlineData(DbType.Int64, typeof(long), 6, 123456, 123456)]
        [InlineData(DbType.Int64, typeof(long), 6, -123456, -123456)]
        [InlineData(DbType.Int64, typeof(long), 6, 1234567, 999999)]
        [InlineData(DbType.Int64, typeof(long), 6, -1234567, -999999)]
        [InlineData(DbType.Decimal, typeof(decimal), 5, 123.0, 123.0)]
        [InlineData(DbType.Decimal, typeof(decimal), 5, -123.0, -123.0)]
        [InlineData(DbType.Decimal, typeof(decimal), 5, 12345.0, 12345.0)]
        [InlineData(DbType.Decimal, typeof(decimal), 5, -12345.0, -12345.0)]
        [InlineData(DbType.Decimal, typeof(decimal), 5, 123456.0, 99999.0)]
        [InlineData(DbType.Decimal, typeof(double), 5, -123456.0, -99999.0)]
        [InlineData(DbType.Decimal, typeof(int), 5, 123456, 99999)]
        [InlineData(DbType.Date, typeof(DateTime), 0, "2010-11-22 05:04:03", "2010-11-22 05:04:03")]
        [InlineData(DbType.Date, typeof(DateTime), 0, "4010-11-22 05:04:03", "3000-12-31 00:00:00")]
        [InlineData(DbType.Date, typeof(DateTime), 0, "1010-11-22 05:04:03", "1700-01-01 00:00:00")]
        public void TruncateRuleControllerTest(DbType dbtype, Type parameterType, int size, object value, object expectedValue)
        {
            value = TestValue.Translate(parameterType, value);
            expectedValue = TestValue.Translate(parameterType, expectedValue);

            var controller = new DefaultUpdateQueryTruncationController()
            {
                MaximumDate = new DateTime(3000, 12, 31),
                MinimumDate = new DateTime(1700, 1, 1),
            };

            controller.Truncate(dbtype, size, value).Should().Be(expectedValue);
        }

        [Fact]
        public void TruncateRuleCalled()
        {
            var td = AllEntities.Get<Entity1>().TableDescriptor;
            var binder = new UpdateQueryToTypeBinder(typeof(Entity1));
            binder.AutoBind(td);

            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");

            var truncateCalls = new List<Tuple<DbType, int, object>>();
            var mockTruncate = new Mock<IUpdateQueryTruncationController>();
            mockTruncate.Setup(c => c.Truncate(It.IsAny<DbType>(), It.IsAny<int>(), It.IsAny<object>()))
                .Callback<DbType, int, object>((t, s, o) => truncateCalls.Add(new Tuple<DbType, int, object>(t, s, o)))
                .Returns<DbType, int, object>((t, s, o) =>
                {
                    if (o is string str && str.Length > s)
                        return str.Substring(0, s);
                    return o;
                });

            UpdateQueryTruncationRules.Instance.EnableTruncation(dbconnection.ConnectionString, mockTruncate.Object);
            using var delay = new DelayedAction(() => UpdateQueryTruncationRules.Instance.DisableTruncation(dbconnection.ConnectionString));

            var e = new Entity1()
            {
                ID = 123,
                F1 = short.MaxValue,
                F2 = int.MaxValue,
                F3 = long.MaxValue,
                F4 = 1.234,
                F5 = 4.567m,
                F6 = new DateTime(1995, 4, 26),
                F7 = new TimeSpan(5, 27, 13),
                F8 = true,
                F9 = Guid.Empty,
                F10 = new byte[] { 1, 2, 3 },
                F11 = "very long string is here",
                F12 = null,
                F13 = new Dict() { ID = "dictid", Name = "dictvalue" }
            };

            binder.BindAndExecute(query, e, false);

#pragma warning disable IDE0038 // Use pattern matching
            truncateCalls.Should().HaveElementMatching(e => e.Item1 == DbType.String && e.Item2 == 10 && e.Item3 is string && (string)e.Item3 == "very long string is here");
#pragma warning restore IDE0038 // Use pattern matching
            query.GetParamValue<string>("f11").Should().Be("very long ");
        }
    }
}

