using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Equivalency;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder;
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
            public int Id { get; protected set; }

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
            public int Id { get; set; }

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

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("id", "f1", "f2", "f3")
                .And.HaveInsertValues("id", "f1", "f2", "f3");
        }

        [Fact]
        public void Insert_Values_NoAutoIncrement_Execute()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetInsertEntityQuery<Entity2>();
            var command = query.Query.Command as DummyDbCommand;
            command.ExecuteNonQueryReturnValue = 1;

            var e = new Entity2()
            {
                Id = 1,
                F1 = 10,
                F2 = "text",
                F3 = DateTime.Now
            };

            query.Execute(e);

            command.Parameters["@id"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.Id);

            command.Parameters["@f1"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F1);

            command.Parameters["@f2"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F2);

            command.Parameters["@f3"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F3);
        }

        [Fact]
        public void Insert_Values_AutoIncrement()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetInsertEntityQuery<Entity1>();
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("f1", "f2", "f3")
                .And.HaveInsertValues("f1", "f2", "f3");
        }

        [Fact]
        public void Insert_Values_AutoIncrement_Execute()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetInsertEntityQuery<Entity1>();
            var command = query.Query.Command as DummyDbCommand;

            var result = new DummyDbDataReaderResult
            {
                Columns = new DummyDbDataReaderColumnCollection() { new DummyDbDataReaderColumn("", DbType.Int32) },
                Data = new DummyDbDataReaderColumnDataRows() { new DummyDbDataReaderColumnDataCollection(15) }
            };
            command.ReturnReader = new DummyDbDataReader() { result };

            var e = new Entity1()
            {
                F1 = 10,
                F2 = "text",
                F3 = DateTime.Now
            };

            query.Execute(e);

            command.Parameters["@f1"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F1);

            command.Parameters["@f2"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F2);

            command.Parameters["@f3"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F3);

            e.Id.Should().Be(15);
        }

        [Fact]
        public void Insert_Values_AutoIncrement_Ignore()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetInsertEntityQuery<Entity1>(true);
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Should().HaveInsert("tableName")
                .And.HaveInsertFields("id", "f1", "f2", "f3")
                .And.HaveInsertValues("id", "f1", "f2", "f3");
        }

        [Fact]
        public void Update_AllColumns_ById()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetUpdateEntityQuery<Entity1>();
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Should().HaveUpdate("tableName")
                .And.HaveUpdateAssignCount(3);

            var update = ast.UpdateStatement();

            foreach (var (index, name) in new[] { (0, "f1"), (1, "f2"), (2, "f3") })
            {
                var assign = update.UpdateAssign(index);
                assign.AssignTarget().Should().HaveFieldName(name);
                assign.AssignValue().Should().BeParamExpression();
            }

            update.WhereCondition().Should()
                .BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "id"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression());
        }

        [Fact]
        public void Delete_ById()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetDeleteEntityQuery<Entity1>();
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Should().HaveDelete("tableName");

            ast.DeleteStatement().WhereCondition().Should()
                .BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "id"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression());
        }

        [Fact]
        public void Delete_ById_Execute()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetDeleteEntityQuery<Entity2>();
            var command = query.Query.Command as DummyDbCommand;
            command.ExecuteNonQueryReturnValue = 1;

            var e = new Entity2()
            {
                Id = 1,
                F1 = 10,
                F2 = "text",
                F3 = DateTime.Now
            };

            query.Execute(e);

            command.Parameters.Count.Should().Be(1);

            command.Parameters["@id"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.Id);
        }

        [Fact]
        public void Delete_ByCondition()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetMultiDeleteEntityQuery<Entity1>();
            query.Where.Property("F1").Le().Parameter("p1");
            query.PrepareQuery();
            var ast = query.Builder.Query.ParseSql();

            ast.Should().HaveDelete("tableName");

            ast.DeleteStatement().WhereCondition().Should()
                .BeOpExpression("LE_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "f1"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression().And.HaveParamName("p1"));
        }

        [Fact]
        public void Update_AllColumns_ById_Execute()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetUpdateEntityQuery<Entity2>();
            var command = query.Query.Command as DummyDbCommand;
            command.ExecuteNonQueryReturnValue = 1;

            var e = new Entity2()
            {
                Id = 1,
                F1 = 10,
                F2 = "text",
                F3 = DateTime.Now
            };

            query.Execute(e);

            command.Parameters["@id"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.Id);

            command.Parameters["@f1"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F1);

            command.Parameters["@f2"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F2);

            command.Parameters["@f3"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F3);
        }

        [Fact]
        public async Task Update_AllColumns_ById_Execute_Async()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetUpdateEntityQuery<Entity2>();
            var command = query.Query.Command as DummyDbCommand;
            command.ExecuteNonQueryReturnValue = 1;

            var e = new Entity2()
            {
                Id = 1,
                F1 = 10,
                F2 = "text",
                F3 = DateTime.Now
            };

            await query.ExecuteAsync(e);

            command.Parameters["@id"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.Id);

            command.Parameters["@f1"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F1);

            command.Parameters["@f2"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F2);

            command.Parameters["@f3"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(e.F3);
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

            ast.Should().HaveUpdate("tableName")
                .And.HaveUpdateAssignCount(1);

            var update = ast.UpdateStatement();

            var assign = update.UpdateAssign(0);
            assign.AssignTarget().Should().HaveFieldName("f2");
            assign.AssignValue().Should().BeParamExpression();

            query.GetParamValue<string>(assign.AssignValue().ExprParamName()).Should().Be("abcd");

            update.WhereCondition().Should()
                .BeOpExpression("LE_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression("tableName", "f1"))
                .And.ItsParameter(1, p => p.Should().BeParamExpression().And.HaveParamName("p1"));
        }

        [Fact]
        public void Update_ByCondition_Execute()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetMultiUpdateEntityQuery<Entity2>();

            query.Where.Property("F1").Gt().Parameter("p1");
            query.AddUpdateColumn("F2", "text");

            var command = query.Query.Command as DummyDbCommand;
            command.ExecuteNonQueryReturnValue = 1;

            query.BindParam("p1", 20);

            query.Execute();

            command.Parameters["@p1"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(20);

            command.Parameters["@F2"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be("text");
        }

        [Fact]
        public async Task Update_ByCondition_Execute_Async()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetMultiUpdateEntityQuery<Entity2>();

            query.Where.Property("F1").Gt().Parameter("p1");
            query.AddUpdateColumn("F2", "text");

            var command = query.Query.Command as DummyDbCommand;
            command.ExecuteNonQueryReturnValue = 1;

            query.BindParam("p1", 20);

            await query.ExecuteAsync();

            command.Parameters["@p1"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be(20);

            command.Parameters["@F2"]
                .Should().NotBeNull()
                .And.Subject.As<DbParameter>()
                    .Value.Should().Be("text");
        }
    }
}

