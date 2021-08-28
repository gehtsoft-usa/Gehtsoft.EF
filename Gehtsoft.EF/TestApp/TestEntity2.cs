using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.Tools.TypeUtils;
using Microsoft.Win32;
using MySqlConnector.Logging;
using NUnit.Framework;
using NUnit.Framework.Internal.Commands;

namespace TestApp
{
    public static class TestSqlInjections
    {
        [Entity(Table = "tgoodsqi")]
        public class Good
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Name { get; set; }

            public Good()
            {
            }
        }

        private static void Create(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetCreateEntityQuery(type))
                query.Execute();
        }

        private static void Drop(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetDropEntityQuery(type))
                query.Execute();
        }

        public static void Do(SqlDbConnection connection)
        {
            Drop(connection, typeof(Good));
            Create(connection, typeof(Good));

            var goodDescriptor = AllEntities.Inst[typeof(Good)].TableDescriptor;

            ((Action)(() => connection.GetQuery($"select * from {goodDescriptor.Name} where good = ';  -- '"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.GetQuery($"select * from {goodDescriptor.Name} where good = \";  -- \""))).Should().Throw<ArgumentException>();

            //check delete query
            {
                var builder = connection.GetDeleteQueryBuilder(goodDescriptor);
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("'"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("\""))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["ID"]).Eq().Value(1))).Should().NotThrow<ArgumentException>();
            }

            //check update query
            {
                var builder = connection.GetUpdateQueryBuilder(goodDescriptor);

                ((Action)(() => builder.AddUpdateColumn(goodDescriptor["Name"], "'; -- '"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("'"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("\""))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["ID"]).Eq().Value(1))).Should().NotThrow<ArgumentException>();
            }

            //check select query
            {
                var builder = connection.GetSelectQueryBuilder(goodDescriptor);
                ((Action)(() => builder.AddExpressionToResultset("'; --", DbType.String))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.AddExpressionToResultset("; --", DbType.String))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.AddToResultset(goodDescriptor["Name"], "';--"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.AddToResultset(goodDescriptor["Name"], ";--"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Having.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
            }

            using (var query = connection.GetGenericSelectEntityQuery<Good>())
            {
                ((Action)(() => query.AddExpressionToResultset("'; --", DbType.String, "hack"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddExpressionToResultset("; --", DbType.String, "hack"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddExpressionToResultset("Name", DbType.String, "hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddToResultset("Name", "hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddToResultset(AggFn.Avg, "Name", "hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.Where.Property("Name").Eq().Raw("hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.Where.Property("Name").Eq().Raw("'hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddOrderByExpr("Name;"))).Should().Throw<ArgumentException>();
            }
        }
    }
}