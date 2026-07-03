using System.Data;
using System.Linq;
using AwesomeAssertions;
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

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("id", "f1", "f2", "f3")
                .And.HaveInsertValues("id", "f1", "f2", "f3");
        }

        [Fact]
        public void Insert_Values_AutoIncrement()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetInsertQueryBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("f1", "f2", "f3")
                .And.HaveInsertValues("f1", "f2", "f3");
        }

        [Fact]
        public void Insert_Values_AutoIncrement_Ignore()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();
            var builder = connection.GetInsertQueryBuilder(table, true);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("id", "f1", "f2", "f3")
                .And.HaveInsertValues("id", "f1", "f2", "f3");
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

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("id", "f1", "f2", "f3");

            var select = ast.InsertStatement().InsertSelect();
            select.Should().Exist();

            var alias = select.Table(0).TableAlias().Value;
            select.Table(0).Should().HaveTableName("tableName1");

            select.Should()
                .HaveResultsetSize(4)
                .And.HaveResultsetItemExpression(0, e => e.Should().BeFieldExpression(alias, "id"))
                .And.HaveResultsetItemExpression(1, e => e.Should().BeFieldExpression(alias, "f1"))
                .And.HaveResultsetItemExpression(2, e => e.Should().BeFieldExpression(alias, "f2"))
                .And.HaveResultsetItemExpression(3, e => e.Should().BeFieldExpression(alias, "f3"));

            select.SelectWhere().ClauseCondition().Should()
                .BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression(alias, "id"))
                .And.ItsParameter(1, p => p.Should().BeConstant(1));
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

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("f1", "f2", "f3");

            ast.InsertStatement().InsertSelect().Should().Exist();
        }

        [Fact]
        public void Insert_Values_ColumnFilter()
        {
            var table = StageTable(false);
            using var connection = new DummySqlConnection();
            var builder = connection.GetInsertQueryBuilder(table);
            builder.IncludeOnly(new string[] { "f1", "f3" });

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("f1", "f3")
                .And.HaveInsertValues("f1", "f3");
        }

        [Fact]
        public void Insert_Select_ColumnFilter()
        {
            var table = StageTable(true);
            using var connection = new DummySqlConnection();

            var builder1 = connection.GetSelectQueryBuilder(table);
            builder1.AddToResultset(table["f1"]);
            builder1.AddToResultset(table["f3"]);

            var builder = connection.GetInsertSelectQueryBuilder(table, builder1);
            builder.IncludeOnly(new string[] { "f1", "f3" });

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("f1", "f3");

            ast.InsertStatement().InsertSelect().Should().Exist();
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

            ast.Should().HaveUpdate("tableName")
                .And.HaveUpdateAssignCount(3);

            var update = ast.UpdateStatement();

            foreach (var (index, name) in new[] { (0, "f1"), (1, "f2"), (2, "f3") })
            {
                var assign = update.UpdateAssign(index);
                assign.AssignTarget().Should().HaveFieldName(name);
                assign.AssignValue().Should().BeParamExpression().And.HaveParamName(name);
            }

            update.WhereCondition().Should()
                .BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "id"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression().And.HaveParamName("id"));
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

            ast.Should().HaveUpdate("tableName")
                .And.HaveUpdateAssignCount(1);

            var update = ast.UpdateStatement();

            var assign = update.UpdateAssign(0);
            assign.AssignTarget().Should().HaveFieldName("f2");
            assign.AssignValue().Should().BeParamExpression().And.HaveParamName("f2");

            update.WhereCondition().Should()
                .BeOpExpression("LE_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "f1"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression().And.HaveParamName("p1"));
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

            ast.Should().HaveUpdate("tableName")
                .And.HaveUpdateAssignCount(1);

            var update = ast.UpdateStatement();

            var assign = update.UpdateAssign(0);
            assign.AssignTarget().Should().HaveFieldName("f2");
            assign.AssignValue().Should()
                .BeOpExpression("MUL_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression().And.HaveFieldName("f2"))
                .And.ItsParameter(1, p => p.Should().BeConstant(1.5));

            update.WhereCondition().Should()
                .BeOpExpression("LE_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "f1"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression().And.HaveParamName("p1"));
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

            ast.Should().HaveUpdate("tableName")
                .And.HaveUpdateAssignCount(1);

            var update = ast.UpdateStatement();

            var assign = update.UpdateAssign(0);
            assign.AssignTarget().Should().HaveFieldName("f2");

            var subquery = assign.AssignValue();
            subquery.Should().Exist();

            var alias = subquery.Table(0).TableAlias().Value;
            subquery.Table(0).Should().HaveTableName("tableName1");

            subquery.Should()
                .HaveResultsetSize(1)
                .And.HaveResultsetItemExpression(0, e => e.Should().BeFieldExpression(alias, "f1"));

            subquery.SelectWhere().ClauseCondition().Should()
                .BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression(alias, "id"))
                .And.ItsParameter(1, p => p.Should().BeFieldExpression("tableName", "id"));

            update.Should().HaveNoWhere();
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

            ast.Should().HaveDelete("tableName");

            ast.DeleteStatement().WhereCondition().Should()
                .BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "id"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression().And.HaveParamName("id"));
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

            ast.Should().HaveDelete("tableName");

            ast.DeleteStatement().WhereCondition().Should()
                .BeOpExpression("LE_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "f1"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression().And.HaveParamName("p1"));
        }
    }
}
