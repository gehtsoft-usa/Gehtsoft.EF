using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Query
{
    public class Fields
    {
        public Fields()
        {
        }

        public enum TestEnum
        {
            V0 = 0,
            V1 = 1,
            V2 = 2,
            V3 = 3
        }

        private static DummyDbDataReaderResult CreateResult()
        {
            var r = new DummyDbDataReaderResult()
            {
                Columns = new DummyDbDataReaderColumnCollection()
                {
                    { "f1", DbType.Int16 },
                    { "f2", DbType.Int32 },
                    { "f3", DbType.Int64 },
                    { "f4", DbType.Double },
                    { "f5", DbType.Decimal },
                    { "f6", DbType.Date },
                    { "f7", DbType.DateTime },
                    { "f8", DbType.Time },
                    { "f9", DbType.Boolean },
                    { "f10", DbType.Guid },
                    { "f11", DbType.Binary },
                    { "f12", DbType.String },
                },

                Data = new DummyDbDataReaderColumnDataRows()
                {
                    { (short)1, (int)2, (long)3, (double)1.23, (decimal)4.56, new DateTime(2020, 5, 22), new DateTime(2021, 7, 23, 11, 54, 12), new TimeSpan(1, 22, 33), "1", "c25f12a3-36fb-4263-be31-773f675d9aa9", new byte[] { 1, 2, 3 }, "abcd" },
                    { DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value },
                    { null, null, null, null, null, null, null, null, null, null, null, null }
                }
            };
            return r;
        }

        private static DummyDbDataReader CreateReader()
        {
            return new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection()
                {
                    CreateResult()
                }
            };
        }

        [Theory]
        [InlineData(1, "f1", typeof(short), 1)]
        [InlineData(1, "f1", typeof(int), 1)]
        [InlineData(1, "f1", typeof(double), 1)]
        [InlineData(1, "f1", typeof(string), "1")]
        [InlineData(1, "f1", typeof(TestEnum), TestEnum.V1)]
        [InlineData(2, "f1", typeof(short), 0)]
        [InlineData(2, "f1", typeof(short?), null)]

        [InlineData(1, "f2", typeof(int), 2)]
        [InlineData(1, "f2", typeof(double), 2.0)]
        [InlineData(1, "f2", typeof(decimal), 2)]
        [InlineData(1, "f2", typeof(TestEnum), TestEnum.V2)]
        [InlineData(2, "f2", typeof(int), 0)]
        [InlineData(2, "f2", typeof(TestEnum), TestEnum.V0)]
        [InlineData(2, "f2", typeof(int?), null)]
        [InlineData(2, "f2", typeof(TestEnum?), null)]
        [InlineData(3, "f2", typeof(int), 0)]
        [InlineData(3, "f2", typeof(int?), null)]

        [InlineData(1, "f3", typeof(long), 3)]
        [InlineData(1, "f3", typeof(double), 3.0)]
        [InlineData(1, "f3", typeof(TestEnum), TestEnum.V3)]
        [InlineData(2, "f3", typeof(long), 0)]
        [InlineData(2, "f3", typeof(long?), null)]

        [InlineData(1, "f4", typeof(double), 1.23)]
        [InlineData(1, "f4", typeof(decimal), 1.23)]
        [InlineData(2, "f4", typeof(double), 0)]
        [InlineData(2, "f4", typeof(double?), null)]

        [InlineData(1, "f5", typeof(double), 4.56)]
        [InlineData(1, "f5", typeof(decimal), 4.56)]
        [InlineData(2, "f5", typeof(decimal), 0)]
        [InlineData(2, "f5", typeof(decimal?), null)]

        [InlineData(1, "f6", typeof(DateTime), "2020-05-22")]
        [InlineData(2, "f6", typeof(DateTime), 0)]
        [InlineData(2, "f6", typeof(DateTime?), null)]

        [InlineData(1, "f7", typeof(DateTime), "2021-07-23 11:54:12")]
        [InlineData(2, "f7", typeof(DateTime), 0)]
        [InlineData(2, "f7", typeof(DateTime?), null)]

        [InlineData(1, "f8", typeof(TimeSpan), "01:22:33")]
        [InlineData(2, "f8", typeof(TimeSpan), 0)]
        [InlineData(2, "f8", typeof(TimeSpan?), null)]

        [InlineData(1, "f9", typeof(bool), true)]
        [InlineData(2, "f9", typeof(bool), false)]
        [InlineData(2, "f9", typeof(bool?), null)]
        [InlineData(3, "f9", typeof(bool?), null)]

        [InlineData(1, "f10", typeof(Guid), "c25f12a3-36fb-4263-be31-773f675d9aa9")]
        [InlineData(2, "f10", typeof(Guid), "00000000-0000-0000-0000-000000000000")]
        [InlineData(2, "f10", typeof(Guid?), null)]
        [InlineData(3, "f10", typeof(Guid), "00000000-0000-0000-0000-000000000000")]
        [InlineData(3, "f10", typeof(Guid?), null)]

        [InlineData(1, "f11", typeof(byte[]), "010203")]
        [InlineData(2, "f11", typeof(byte[]), null)]
        [InlineData(3, "f11", typeof(byte[]), null)]

        [InlineData(1, "f12", typeof(string), "abcd")]
        [InlineData(2, "f12", typeof(string), null)]
        [InlineData(3, "f12", typeof(string), null)]

        public void Read(int row, string column, Type dataType, object expectedValue)
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();

            expectedValue = TestValue.Translate(dataType, expectedValue);

            query.ExecuteReader();
            for (int i = 0; i < row; i++)
                query.ReadNext().Should().BeTrue();

            query.GetValue(column, dataType).Should().BeEquivalentTo(expectedValue);
        }

        [Fact]
        public void FieldCount()
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();
            query.FieldCount.Should().Be(12);
        }

        [Theory]
        [InlineData(0, "f1", typeof(short))]
        [InlineData(1, "f2", typeof(int))]
        [InlineData(2, "f3", typeof(long))]
        [InlineData(3, "f4", typeof(double))]
        [InlineData(4, "f5", typeof(decimal))]
        [InlineData(5, "f6", typeof(DateTime))]
        [InlineData(6, "f7", typeof(DateTime))]
        [InlineData(7, "f8", typeof(TimeSpan))]
        [InlineData(8, "f9", typeof(bool))]
        [InlineData(9, "f10", typeof(Guid))]
        [InlineData(10, "f11", typeof(byte[]))]
        [InlineData(11, "f12", typeof(string))]
        public void Field(int index, string name, Type dataType)
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();

            var field = query.Field(index);
            field.Name.Should().Be(name);
            field.DataType.Should().Be(dataType);
            field.Index.Should().Be(index);

            var field1 = query.Field(name);
            field1.Should().BeSameAs(field);
        }

        [Theory]
        [InlineData("f1", false, 0)]
        [InlineData("f1", true, 0)]
        [InlineData("F1", true, 0)]
        [InlineData("F1", false, -1)]

        public void FindField(string name, bool ignoreCase, int expected)
        {
            using var dbconnection = new DummyDbConnection();
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            dbquery.ReturnReader = CreateReader();
            query.ExecuteReader();
            query.FindField(name, ignoreCase).Should().Be(expected);
        }
    }
}

