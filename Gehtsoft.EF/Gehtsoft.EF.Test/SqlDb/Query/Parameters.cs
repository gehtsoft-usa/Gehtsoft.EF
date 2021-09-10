using System;
using System.Data;
using FluentAssertions;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Query
{
    public class Parameters
    {
        [Theory]
        [InlineData("p1", DbType.Int32, typeof(int), 123, null, 123)]
        [InlineData("p2", DbType.Double, typeof(double), 1.23, null, 1.23)]
        [InlineData("p3", DbType.Decimal, typeof(decimal), "1.23", null, "1.23")]
        [InlineData("p3", DbType.String, typeof(string), "abcd", null, "abcd")]
        [InlineData("p3", DbType.DateTime, typeof(DateTime), "2010-11-21 11:23", null, "2010-11-21 11:23")]
        [InlineData("p3", DbType.Binary, typeof(byte[]), "123456", null, "123456")]
        [InlineData("p5", DbType.String, typeof(bool), true, typeof(string), "1")]
        [InlineData("p5", DbType.String, typeof(Guid), "e5ccb875-4add-4832-9524-4ce68cf6d8bd", typeof(string), "e5ccb875-4add-4832-9524-4ce68cf6d8bd")]
        public void BindInputParamter(string name, DbType type, Type valueType, object value, Type expectedValueType, object expectedValue)
        {
            using var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;

            value = TestValue.Translate(valueType, value);
            expectedValue = TestValue.Translate(expectedValueType ?? valueType, expectedValue);

            query.BindParam(name, valueType, value);

            query.ExecuteNoData();

            dbquery.Parameters.Count.Should().Be(1);
            dbquery.Parameters[0].ParameterName.Should().Be(efconnection.GetLanguageSpecifics().ParameterPrefix + name);
            dbquery.Parameters[0].DbType.Should().Be(type);
            dbquery.Parameters[0].Value.Should().BeEquivalentTo(expectedValue);
            dbquery.Parameters[0].Direction.Should().Be(ParameterDirection.Input);

            query.GetParamValue(name).Should().BeEquivalentTo(expectedValue);
            query.GetParamValue(name, valueType).Should().BeEquivalentTo(value);
        }

        [Theory]
        [InlineData("p1", DbType.Int32, typeof(int), 0)]
        [InlineData("p1", DbType.String, typeof(string), null)]
        [InlineData("p1", DbType.DateTime, typeof(DateTime), 0)]
        public void BindNull(string name, DbType type, Type expectedValueType, object expectedValue)
        {
            using var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;

            expectedValue = TestValue.Translate(expectedValueType, expectedValue);

            query.BindNull(name, type);

            query.ExecuteNoData();

            dbquery.Parameters.Count.Should().Be(1);
            dbquery.Parameters[0].ParameterName.Should().Be(efconnection.GetLanguageSpecifics().ParameterPrefix + name);
            dbquery.Parameters[0].DbType.Should().Be(type);
            dbquery.Parameters[0].Value.Should().Be(DBNull.Value);
            dbquery.Parameters[0].Direction.Should().Be(ParameterDirection.Input);

            query.GetParamValue(name).Should().Be(null);
            query.GetParamValue(name, expectedValueType).Should().BeEquivalentTo(expectedValue);
        }

        [Fact]
        public void BindFK()
        {
            using var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;
            var pp = efconnection.GetLanguageSpecifics().ParameterPrefix;

            Category category = new Category()
            {
                CategoryID = 123,
                CategoryName = "567"
            };

            query.BindParam("p1", category);
            query.BindParam<Category>("p2", null);
            query.ExecuteNoData();

            dbquery.Parameters[pp + "p1"].Direction.Should().Be(ParameterDirection.Input);
            dbquery.Parameters[pp + "p1"].DbType.Should().Be(DbType.Int32);
            dbquery.Parameters[pp + "p1"].Value = 123;

            dbquery.Parameters[pp + "p2"].Direction.Should().Be(ParameterDirection.Input);
            dbquery.Parameters[pp + "p2"].DbType.Should().Be(DbType.Int32);
            dbquery.Parameters[pp + "p2"].Value = DBNull.Value;
        }

        [Fact]
        public void BindOutput()
        {
            using var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");
            var dbquery = query.Command as DummyDbCommand;

            query.BindOutputParam("p1", DbType.Int32);
            query.BindOutputParam<string>("p2");
            query.ExecuteNoData();
            var pp = efconnection.GetLanguageSpecifics().ParameterPrefix;
            dbquery.Parameters[pp + "p1"].Direction.Should().Be(ParameterDirection.Output);
            dbquery.Parameters[pp + "p1"].Value = 123;
            dbquery.Parameters[pp + "p2"].Value = DBNull.Value;

            query.GetParamValue("p1").Should().Be(123);
            query.GetParamValue<int>("p1").Should().Be(123);
            query.GetParamValue("p2").Should().Be(null);
        }

        [Fact]
        public void BindOnce()
        {
            using var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query = efconnection.GetQuery("command");

            query.BindParam<int>("p1", 123);
            query.BindParam<string>("p2", "abcdef");
            query.BindOutput("p3", DbType.Boolean);

            query.ParametersCount.Should().Be(3);

            query.BindParam<int>("p1", 456);
            query.BindParam<string>("p2", "defijk");
            query.BindOutput("p3", DbType.Int32);

            query.ParametersCount.Should().Be(3);
        }

        [Fact]
        public void CopyParams()
        {
            using var dbconnection = new DummyDbConnection() { ConnectionString = "dummyConnectionString" };
            using var efconnection = new DummySqlConnection(dbconnection);
            using var query1 = efconnection.GetQuery("command");
            using var query2 = efconnection.GetQuery("command");

            query1.BindParam<int>("p1", 123);
            query1.BindParam<string>("p2", "abcdef");
            query1.BindOutputParam<bool>("p3");

            query2.CopyParametersFrom(query1);

            var dbquery = query2.Command as DummyDbCommand;
            query2.ExecuteNoData();

            dbquery.Parameters.Count.Should().Be(3);

            var pp = efconnection.GetLanguageSpecifics().ParameterPrefix;

            dbquery.Parameters[0].ParameterName.Should().Be(pp + "p1");
            dbquery.Parameters[0].DbType.Should().Be(DbType.Int32);
            dbquery.Parameters[0].Value.Should().Be(123);
            dbquery.Parameters[0].Direction.Should().Be(ParameterDirection.Input);

            dbquery.Parameters[1].ParameterName.Should().Be(pp + "p2");
            dbquery.Parameters[1].DbType.Should().Be(DbType.String);
            dbquery.Parameters[1].Value.Should().Be("abcdef");
            dbquery.Parameters[1].Direction.Should().Be(ParameterDirection.Input);

            dbquery.Parameters[2].ParameterName.Should().Be(pp + "p3");
            dbquery.Parameters[2].DbType.Should().Be(DbType.String);
            dbquery.Parameters[2].Direction.Should().Be(ParameterDirection.Output);
        }
    }
}
