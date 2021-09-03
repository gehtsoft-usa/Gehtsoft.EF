using System;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class UpdateQueries
    {
        [Entity(Scope = "update_queries", Table = "tableName")]
        public class Entity1
        {
            [AutoId(Field = "id")]
            public int Id { get; }

            [EntityProperty(Field = "f1")]
            public int F1 { get; set; }
            [EntityProperty(Field = "f2")]
            public string F2 { get; set; }

            [EntityProperty(Field = "f3")]
            public DateTime F3 { get; set; }
        }

        [Entity(Scope = "update_queries", Table = "tableName")]
        public class Entity2
        {
            [PrimaryKey(Field = "id")]
            public int Id { get; }

            [EntityProperty(Field = "f1")]
            public int F1 { get; set; }
            [EntityProperty(Field = "f2")]
            public string F2 { get; set; }

            [EntityProperty(Field = "f3")]
            public DateTime F3 { get; set; }
        }

        [Fact]
        public void Insert_Values_NoAutoIncrement()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetInsertEntityQuery<Entity2>();
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Select("/INSERT")
                .Should().HaveCount(1);

            var stmt = ast.SelectNode("/INSERT");

            stmt.SelectNode("/*", 1)
                .Should().HaveSymbol("TABLE_NAME")
                .And.Subject.SelectNode("/*", 1)
                    .Should().HaveSymbol("IDENTIFIER")
                    .And.HaveValue("tableName");

            stmt.SelectNode("/*", 2)
                .Should().HaveSymbol("FIELDS");

            var f = stmt.Select("/*[2]/FIELD").ToArray();
            f.Should().HaveCount(4);

            f[0].Should().ContainMatching("/*", n => n.Value == "id");
            f[1].Should().ContainMatching("/*", n => n.Value == "f1");
            f[2].Should().ContainMatching("/*", n => n.Value == "f2");
            f[3].Should().ContainMatching("/*", n => n.Value == "f3");

            stmt.SelectNode("/*", 3)
                .Should().HaveSymbol("INSERT_VALUES_LIST");

            f = stmt.Select("/*[3]/INSERT_VALUES/INSERT_VALUE/PARAM").ToArray();

            f.Should().HaveCount(4);

            f[0].Should().ContainMatching("/*", n => n.Value == "id");
            f[1].Should().ContainMatching("/*", n => n.Value == "f1");
            f[2].Should().ContainMatching("/*", n => n.Value == "f2");
            f[3].Should().ContainMatching("/*", n => n.Value == "f3");
        }

        [Fact]
        public void Insert_Values_AutoIncrement()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetInsertEntityQuery<Entity1>();
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Select("/INSERT")
                .Should().HaveCount(1);

            var stmt = ast.SelectNode("/INSERT");

            stmt.SelectNode("/*", 1)
                .Should().HaveSymbol("TABLE_NAME")
                .And.Subject.SelectNode("/*", 1)
                    .Should().HaveSymbol("IDENTIFIER")
                    .And.HaveValue("tableName");

            stmt.SelectNode("/*", 2)
                .Should().HaveSymbol("FIELDS");

            var f = stmt.Select("/*[2]/FIELD").ToArray();
            f.Should().HaveCount(3);

            f[0].Should().ContainMatching("/*", n => n.Value == "f1");
            f[1].Should().ContainMatching("/*", n => n.Value == "f2");
            f[2].Should().ContainMatching("/*", n => n.Value == "f3");

            stmt.SelectNode("/*", 3)
                .Should().HaveSymbol("INSERT_VALUES_LIST");

            f = stmt.Select("/*[3]/INSERT_VALUES/INSERT_VALUE/PARAM").ToArray();

            f.Should().HaveCount(3);

            f[0].Should().ContainMatching("/*", n => n.Value == "f1");
            f[1].Should().ContainMatching("/*", n => n.Value == "f2");
            f[2].Should().ContainMatching("/*", n => n.Value == "f3");
        }

        [Fact]
        public void Insert_Values_AutoIncrement_Ignore()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetInsertEntityQuery<Entity1>(true);
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Select("/INSERT")
                .Should().HaveCount(1);

            var stmt = ast.SelectNode("/INSERT");

            stmt.SelectNode("/*", 1)
                .Should().HaveSymbol("TABLE_NAME")
                .And.Subject.SelectNode("/*", 1)
                    .Should().HaveSymbol("IDENTIFIER")
                    .And.HaveValue("tableName");

            stmt.SelectNode("/*", 2)
                .Should().HaveSymbol("FIELDS");

            var f = stmt.Select("/*[2]/FIELD").ToArray();
            f.Should().HaveCount(4);

            f[0].Should().ContainMatching("/*", n => n.Value == "id");
            f[1].Should().ContainMatching("/*", n => n.Value == "f1");
            f[2].Should().ContainMatching("/*", n => n.Value == "f2");
            f[3].Should().ContainMatching("/*", n => n.Value == "f3");

            stmt.SelectNode("/*", 3)
                .Should().HaveSymbol("INSERT_VALUES_LIST");

            f = stmt.Select("/*[3]/INSERT_VALUES/INSERT_VALUE/PARAM").ToArray();

            f.Should().HaveCount(4);

            f[0].Should().ContainMatching("/*", n => n.Value == "id");
            f[1].Should().ContainMatching("/*", n => n.Value == "f1");
            f[2].Should().ContainMatching("/*", n => n.Value == "f2");
            f[3].Should().ContainMatching("/*", n => n.Value == "f3");
        }

        [Fact]
        public void UpdateQuery_AllColumns_ById()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetUpdateEntityQuery<Entity1>();
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Select("/UPDATE")
                .Should().HaveCount(1);

            ast.SelectNode("/UPDATE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN")
               .Should().HaveCount(3);

            var list = ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN").ToArray();

            list[0].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f1");
            list[0].SelectNode("/PARAM/IDENTIFIER[1]").Should().Exist();

            list[1].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f2");
            list[1].SelectNode("/PARAM/IDENTIFIER[1]").Should().Exist();

            list[2].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f3");
            list[2].SelectNode("/PARAM/IDENTIFIER[1]").Should().Exist();

            var where = ast.SelectNode("/UPDATE/WHERE_CLAUSE");

            var whereOp = where.SelectNode("*", 1);
            whereOp.Should().HaveSymbol("EQ_OP");
            whereOp.SelectNode("*", 1).Should().HaveSymbol("FIELD");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 1).Should().HaveValue("tableName");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 2).Should().HaveValue("id");

            whereOp.SelectNode("*", 2).Should().HaveSymbol("PARAM");
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().Exist();
        }

        [Fact]
        public void Delete_ById()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetDeleteEntityQuery<Entity1>();
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Select("/DELETE")
                .Should().HaveCount(1);

            ast.SelectNode("/DELETE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            var where = ast.SelectNode("/DELETE/WHERE_CLAUSE");

            var whereOp = where.SelectNode("*", 1);
            whereOp.Should().HaveSymbol("EQ_OP");
            whereOp.SelectNode("*", 1).Should().HaveSymbol("FIELD");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 1).Should().HaveValue("tableName");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 2).Should().HaveValue("id");

            whereOp.SelectNode("*", 2).Should().HaveSymbol("PARAM");
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().Exist();
        }

        [Fact]
        public void Delete_ByCondition()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetMultiDeleteEntityQuery<Entity1>();
            query.Where.Property("F1").Le().Parameter("p1");
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Select("/DELETE")
                .Should().HaveCount(1);

            ast.SelectNode("/DELETE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            var where = ast.SelectNode("/DELETE/WHERE_CLAUSE");

            var whereOp = where.SelectNode("*", 1);
            whereOp.Should().HaveSymbol("LE_OP");
            whereOp.SelectNode("*", 1).Should().HaveSymbol("FIELD");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 1).Should().HaveValue("tableName");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 2).Should().HaveValue("f1");

            whereOp.SelectNode("*", 2).Should().HaveSymbol("PARAM");
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().HaveValue("p1");
        }

        [Fact]
        public void Update_ByCondition()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetMultiUpdateEntityQuery<Entity1>();
            query.Where.Property("F1").Le().Parameter("p1");
            query.AddUpdateColumn<string>("F2", "abcd");
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Select("/UPDATE")
                .Should().HaveCount(1);

            ast.SelectNode("/UPDATE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN")
               .Should().HaveCount(1);

            var list = ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN").ToArray();

            list[0].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f2");
            list[0].SelectNode("/PARAM/IDENTIFIER[1]").Should().Exist();

            query.GetParamValue<string>(list[0].SelectNode("/PARAM/IDENTIFIER[1]").Value).Should().Be("abcd");

            var where = ast.SelectNode("/UPDATE/WHERE_CLAUSE");

            var whereOp = where.SelectNode("*", 1);
            whereOp.Should().HaveSymbol("LE_OP");
            whereOp.SelectNode("*", 1).Should().HaveSymbol("FIELD");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 1).Should().HaveValue("tableName");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 2).Should().HaveValue("f1");

            whereOp.SelectNode("*", 2).Should().HaveSymbol("PARAM");
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().HaveValue("p1");
        }
    }
}

