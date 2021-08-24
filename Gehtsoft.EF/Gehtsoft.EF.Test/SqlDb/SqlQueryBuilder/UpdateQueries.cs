using System.Data;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public class UpdateQueries
    {
        private static TableDescriptor StageTable(bool autoIncrementPk, string tableName = "tableName")
        {
            var table = new TableDescriptor()
            {
                Name = tableName
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "id",
                DbType = DbType.Int32,
                PrimaryKey = true,
                Autoincrement = autoIncrementPk,
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f1",
                DbType = DbType.Int32,
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f2",
                DbType = DbType.String,
            });
            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f3",
                DbType = DbType.DateTime,
            });
            return table;
        }

        [Fact]
        public void Insert_Values_NoAutoIncrement()
        {
            var table = StageTable(false);
            using var connection = new DummySqlConnection();
            var builder = connection.GetInsertQueryBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

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
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetInsertQueryBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

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
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetInsertQueryBuilder(table, true);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

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
        public void Insert_Select_NoAutoIncrement()
        {
            var table = StageTable(false);
            var table1 = StageTable(false, "tableName1");
            using var connection = new DummySqlConnection();

            var builder1 = connection.GetSelectQueryBuilder(table1);
            builder1.AddToResultset(table1["id"]);
            builder1.AddToResultset(table1["f1"]);
            builder1.AddToResultset(table1["f2"]);
            builder1.AddToResultset(table1["f3"]);
            builder1.Where.Property(table1["id"]).Eq().Value(1);

            var builder = connection.GetInsertSelectQueryBuilder(table, builder1);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

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

            var select = stmt.SelectNode("/*", 3);

            select.Should().HaveSymbol("SELECT");

            var from = stmt.SelectNode("/*", 3)
                .SelectNode("/TABLE_EXPRESSION/FROM_CLAUSE/TABLE_REFERENCE_LIST/TABLE_PRIMARY");

            from.Should().Exist();

            var alias = from.SelectNode("/IDENTIFIER").Value;

            from.SelectNode("/TABLE_NAME/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("tableName1");

            var rs = stmt.SelectNode("/*", 3).Select("/SELECT_LIST/SELECT_SUBLIST/EXPR_ALIAS/FIELD").ToArray();
            rs.Should().HaveCount(4);
            rs[0].Should().ContainMatching("/*[1]", n => n.Value == alias);
            rs[0].Should().ContainMatching("/*[2]", n => n.Value == "id");
            rs[1].Should().ContainMatching("/*[1]", n => n.Value == alias);
            rs[1].Should().ContainMatching("/*[2]", n => n.Value == "f1");
            rs[2].Should().ContainMatching("/*[1]", n => n.Value == alias);
            rs[2].Should().ContainMatching("/*[2]", n => n.Value == "f2");
            rs[3].Should().ContainMatching("/*[1]", n => n.Value == alias);
            rs[3].Should().ContainMatching("/*[2]", n => n.Value == "f3");

            var where = stmt.SelectNode("/*", 3)
                .SelectNode("/TABLE_EXPRESSION/WHERE_CLAUSE");

            var whereOp = where.SelectNode("/*", 1);
            whereOp.Should().HaveSymbol("EQ_OP");

            var arg1 = whereOp.SelectNode("/*", 1);
            arg1.Should()
                .HaveSymbol("FIELD")
                .And.ContainMatching("/IDENTIFIER[1]", n => n.Value == alias)
                .And.ContainMatching("/IDENTIFIER[2]", n => n.Value == "id");

            var arg2 = whereOp.SelectNode("/*", 2);
            arg2.Should()
                .HaveSymbol("INT")
                .And.HaveValue("1");

            where.Should().Exist();
        }

        [Fact]
        public void Insert_Select_AutoIncrement()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();

            var builder1 = connection.GetSelectQueryBuilder(table);
            builder1.AddToResultset(table["f1"]);
            builder1.AddToResultset(table["f2"]);
            builder1.AddToResultset(table["f3"]);

            var builder = connection.GetInsertSelectQueryBuilder(table, builder1);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

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
                .Should().HaveSymbol("SELECT");
        }

        [Fact]
        public void UpdateQuery_AllColumns_ById()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetUpdateQueryBuilder(table);
            builder.AddUpdateAllColumns();
            builder.UpdateById();

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/UPDATE")
                .Should().HaveCount(1);

            ast.SelectNode("/UPDATE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN")
               .Should().HaveCount(3);

            var list = ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN").ToArray();

            list[0].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f1");
            list[0].SelectNode("/PARAM/IDENTIFIER[1]").Should().HaveValue("f1");

            list[1].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f2");
            list[1].SelectNode("/PARAM/IDENTIFIER[1]").Should().HaveValue("f2");

            list[2].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f3");
            list[2].SelectNode("/PARAM/IDENTIFIER[1]").Should().HaveValue("f3");

            var where = ast.SelectNode("/UPDATE/WHERE_CLAUSE");

            var whereOp = where.SelectNode("*", 1);
            whereOp.Should().HaveSymbol("EQ_OP");
            whereOp.SelectNode("*", 1).Should().HaveSymbol("FIELD");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 1).Should().HaveValue("tableName");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 2).Should().HaveValue("id");

            whereOp.SelectNode("*", 2).Should().HaveSymbol("PARAM");
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().HaveValue("id");
        }

        [Fact]
        public void Update_ByCondition()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetUpdateQueryBuilder(table);
            builder.Where.Property(table["f1"]).Le().Parameter("p1");

            builder.AddUpdateColumn(table["f2"]);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/UPDATE")
                .Should().HaveCount(1);

            ast.SelectNode("/UPDATE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN")
               .Should().HaveCount(1);

            var list = ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN").ToArray();

            list[0].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f2");
            list[0].SelectNode("/PARAM/IDENTIFIER[1]").Should().HaveValue("f2");

            var where = ast.SelectNode("/UPDATE/WHERE_CLAUSE");

            var whereOp = where.SelectNode("*", 1);
            whereOp.Should().HaveSymbol("LE_OP");
            whereOp.SelectNode("*", 1).Should().HaveSymbol("FIELD");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 1).Should().HaveValue("tableName");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 2).Should().HaveValue("f1");

            whereOp.SelectNode("*", 2).Should().HaveSymbol("PARAM");
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().HaveValue("p1");
        }

        [Fact]
        public void Update_UsingExpression()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetUpdateQueryBuilder(table);
            builder.Where.Property(table["f1"]).Le().Parameter("p1");

            builder.AddUpdateColumnExpression(table["f2"], $"{table["f2"].Name} * 1.5");

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/UPDATE")
                .Should().HaveCount(1);

            ast.SelectNode("/UPDATE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN")
               .Should().HaveCount(1);

            var list = ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN").ToArray();

            list[0].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f2");

            var expr = list[0].SelectNode("/*[2]");
            expr.Should().HaveSymbol("MUL_OP");
            expr.SelectNode("*", 1)
                .Should().HaveSymbol("FIELD")
                .And.ContainMatching("/IDENTIFIER", m => m.Value == "f2");

            expr.SelectNode("*", 2)
                .Should().HaveSymbol("REAL")
                .And.HaveValue("1.5");

            var where = ast.SelectNode("/UPDATE/WHERE_CLAUSE");

            var whereOp = where.SelectNode("*", 1);
            whereOp.Should().HaveSymbol("LE_OP");
            whereOp.SelectNode("*", 1).Should().HaveSymbol("FIELD");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 1).Should().HaveValue("tableName");
            whereOp.SelectNode("*", 1).SelectNode("IDENTIFIER", 2).Should().HaveValue("f1");

            whereOp.SelectNode("*", 2).Should().HaveSymbol("PARAM");
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().HaveValue("p1");
        }

        [Fact]
        public void Update_UsingSelect_NoWhere()
        {
            var table = StageTable(true);
            var table1 = StageTable(true, "tableName1");
            using var connection = new DummySqlConnection();
            var updateBuilder = connection.GetUpdateQueryBuilder(table);

            var selectBuilder = connection.GetSelectQueryBuilder(table1);
            selectBuilder.AddToResultset(table1["f1"]);
            selectBuilder.Where
                .Property(table1["id"])
                .Eq()
                .Reference(updateBuilder.GetReference(table["id"]));

            updateBuilder.AddUpdateColumnSubquery(table["f2"], selectBuilder);

            updateBuilder.PrepareQuery();
            var ast = updateBuilder.Query.ParseSql();

            ast.Select("/UPDATE")
                .Should().HaveCount(1);

            ast.SelectNode("/UPDATE/TABLE_NAME/IDENTIFIER")
               .Should().Exist()
               .And.HaveValue("tableName");

            ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN")
               .Should().HaveCount(1);

            var list = ast.Select("/UPDATE/UPDATE_LIST/UPDATE_ASSIGN").ToArray();

            list[0].SelectNode("/FIELD/IDENTIFIER[1]").Should().HaveValue("f2");

            var subquery = list[0].SelectNode("/*[2]");
            
            subquery.Should().HaveSymbol("SELECT");

            var from = subquery
                .SelectNode("/TABLE_EXPRESSION/FROM_CLAUSE/TABLE_REFERENCE_LIST/TABLE_PRIMARY");

            from.Should().Exist();

            var alias = from.SelectNode("/IDENTIFIER").Value;

            from.SelectNode("/TABLE_NAME/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("tableName1");

            var rs = subquery.Select("/SELECT_LIST/SELECT_SUBLIST/EXPR_ALIAS/FIELD").ToArray();
            rs.Should().HaveCount(1);
            rs[0].Should().ContainMatching("/*[1]", n => n.Value == alias);
            rs[0].Should().ContainMatching("/*[2]", n => n.Value == "f1");

            var where = subquery
                .SelectNode("/TABLE_EXPRESSION/WHERE_CLAUSE");

            var whereOp = where.SelectNode("/*", 1);
            whereOp.Should().HaveSymbol("EQ_OP");

            var arg1 = whereOp.SelectNode("/*", 1);
            arg1.Should()
                .HaveSymbol("FIELD")
                .And.ContainMatching("/IDENTIFIER[1]", n => n.Value == alias)
                .And.ContainMatching("/IDENTIFIER[2]", n => n.Value == "id");

            var arg2 = whereOp.SelectNode("/*", 2);
            arg2.Should()
                .HaveSymbol("FIELD")
                .And.ContainMatching("/IDENTIFIER[1]", n => n.Value == "tableName")
                .And.ContainMatching("/IDENTIFIER[2]", n => n.Value == "id");

            ast.SelectNode("/UPDATE/WHERE_CLAUSE").Should().NotExist();
        }

        [Fact]
        public void Delete_ById()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetDeleteQueryBuilder(table);
            builder.DeleteById();

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

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
            whereOp.SelectNode("*", 2).SelectNode("IDENTIFIER").Should().HaveValue("id");
        }

        [Fact]
        public void Delete_ByCondition()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetDeleteQueryBuilder(table);
            builder.Where.Property(table["f1"]).Le().Parameter("p1");

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

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
    }
}
