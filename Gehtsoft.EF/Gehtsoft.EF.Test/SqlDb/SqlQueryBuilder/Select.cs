using System.Runtime.CompilerServices;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{

    public class Select
    {
        [Fact]
        public void Resultset_PlainColumn()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            var categoryAlias = select.Table(0).TableAlias().Value;

            select.Resultset()
                .Should().HaveCount(1);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprIsField()
                .Should().BeTrue();
            
            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldAlias().Should().Be(categoryAlias);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldName().Should().Be(category[0].Name);
        }

        [Fact]
        public void Resultset_Distinct()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.Distinct = true;

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.SetQuantifiers().Should().HaveElementMatching(e => e.Symbol == "DISTINCT");
        }

        [Fact]
        public void Resultset_Distinct_Off()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.Distinct = false;

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.SetQuantifiers().Should().HaveNoElementMatching(e => e.Symbol == "DISTINCT");
        }

        [Fact]
        public void Resultset_RawExpression()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddExpressionToResultset($"{query.Entities[0].Alias}.{category[1].Name}", category[0].DbType, false, "ma");

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            var categoryAlias = select.Table(0).Identifier().Value;

            select.Resultset()
                .Should().HaveCount(1);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprIsField()
                .Should().BeTrue();

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldAlias()
                .Should().Be(categoryAlias);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldName()
                .Should().Be(category[1].Name);

            select.ResultsetItem(0)
                .ResultsetItemAlias()
                .Should().Be("ma");
        }

        [Fact]
        public void Resultset_PlainColumnAndAlias()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0], "myalias");

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            var categoryAlias = select.Table(0).TableAlias().Value;

            select.ResultsetItem(0)
               .ResultsetItemExpression()
               .ExprIsField()
               .Should().BeTrue();

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldAlias()
                .Should().Be(categoryAlias);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldName()
                .Should().Be(category[0].Name);

            select.ResultsetItem(0)
                .ResultsetItemAlias()
                .Should().Be("myalias");
        }

        [Fact]
        public void Resultset_AggFn_Count()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            query.AddToResultset(AggFn.Count, "count");

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Resultset()
                .Should().HaveCount(1);

            select.ResultsetItem(0)
                .ResultsetExpr()
                .ExprIsCountAll()
                .Should().BeTrue();

            select.ResultsetItem(0)
                .ResultsetItemAlias()
                .Should().Be("count");
        }

        [Theory]
        [InlineData(AggFn.Sum, "SUM")]
        [InlineData(AggFn.Avg, "AVG")]
        [InlineData(AggFn.Min, "MIN")]
        [InlineData(AggFn.Max, "MAX")]
        public void Resultset_AggFn(AggFn fn, string expectedFunction)
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            query.AddToResultset(fn, category[0], "agg");

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Resultset()
                .Should().HaveCount(1);

            select.ResultsetItem(0)
                .ResultsetExpr()
                .ExprIsFnCall()
                .Should().BeTrue();

            select.ResultsetItem(0)
                .ResultsetExpr()
                .ExprFnCallName()
                .Should().Be(expectedFunction);

            select.ResultsetItem(0)
               .ResultsetItemAlias()
               .Should().Be("agg");

            var fnArg = select.ResultsetItem(0)
                .ResultsetExpr()
                .ExprExprFnCallArg(0);

            fnArg.Should().NotBeNull();

            fnArg.ExprIsField().Should().BeTrue();

            fnArg.ExprFieldAlias()
                .Should().Be(query.Entities[0].Alias);

            fnArg.ExprFieldName()
                .Should().Be(category[0].Name);
        }

        [Fact]
        public void Resultset_AddAllRows_OneTable()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            query.AddToResultset(category);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Resultset().Should()
                .HaveCount(category.Count);

            for (int i = 0; i < category.Count; i++)
            {
                select.ResultsetItem(i)
                    .ResultsetItemExpression()
                    .ExprFieldAlias()
                    .Should().Be(query.Entities[0].Alias);

                select.ResultsetItem(i)
                    .ResultsetItemExpression()
                    .ExprFieldName()
                    .Should().Be(category[i].Name);
            }
        }

        [Fact]
        public void Resultset_AddAllRows_TwoTables()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            query.AddTable(product, TableJoinType.None);

            query.AddToResultset(category);
            query.AddToResultset(product);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Resultset().Should()
                .HaveCount(category.Count + product.Count);

            for (int i = 0; i < category.Count; i++)
            {
                select.ResultsetItem(i)
                    .ResultsetItemExpression()
                    .ExprFieldAlias()
                    .Should().Be(query.Entities[0].Alias);

                select.ResultsetItem(i)
                    .ResultsetItemExpression()
                    .ExprFieldName()
                    .Should().Be(category[i].Name);
            }

            for (int i = 0, s = category.Count; i < product.Count; i++)
            {
                select.ResultsetItem(s + i)
                    .ResultsetItemExpression()
                    .ExprFieldAlias()
                    .Should().Be(query.Entities[1].Alias);

                select.ResultsetItem(s + i)
                    .ResultsetItemExpression()
                    .ExprFieldName()
                    .Should().Be(product[i].Name);
            }
        }

        [Fact]
        public void Resultset_AddAllRows_TwoSameTables()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            var secondEntry = query.AddTable(category, TableJoinType.None);

            query.AddToResultset(category);
            query.AddToResultset(category, secondEntry);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Resultset().Should()
                .HaveCount(category.Count * 2);

            for (int i = 0; i < category.Count; i++)
            {
                select.ResultsetItem(i)
                    .ResultsetItemExpression()
                    .ExprFieldAlias()
                    .Should().Be(query.Entities[0].Alias);

                select.ResultsetItem(i)
                    .ResultsetItemExpression()
                    .ExprFieldName()
                    .Should().Be(category[i].Name);
            }

            for (int i = 0, s = category.Count; i < category.Count; i++)
            {
                select.ResultsetItem(s + i)
                     .ResultsetItemExpression()
                     .ExprFieldAlias()
                     .Should().Be(query.Entities[1].Alias);

                select.ResultsetItem(i)
                    .ResultsetItemExpression()
                    .ExprFieldName()
                    .Should().Be(category[i].Name);
            }
        }

        [Fact]
        public void Resultset_PlainColumn_TwoTables()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            var product = AllEntities.Get<Product>().TableDescriptor;

            query.AddTable(product, TableJoinType.None);

            query.AddToResultset(category[0]);
            query.AddToResultset(product[0]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Resultset()
                .Should().HaveCount(2);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldAlias()
                .Should().Be(query.Entities[0].Alias);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldName()
                .Should().Be(category[0].Name);

            select.ResultsetItem(1)
                .ResultsetItemExpression()
                .ExprFieldAlias()
                .Should().Be(query.Entities[1].Alias);

            select.ResultsetItem(1)
                .ResultsetItemExpression()
                .ExprFieldName()
                .Should().Be(product[0].Name);
        }

        [Fact]
        public void Resultset_PlainColumn_TableUsedTwice()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            var product = AllEntities.Get<Product>().TableDescriptor;

            query.AddTable(product, TableJoinType.None);
            query.AddTable(product, TableJoinType.None);

            query.AddToResultset(category[0]);
            query.AddToResultset(product[0]);
            query.AddToResultset(product[1], query.Entities[2]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Resultset()
                .Should().HaveCount(3);

            select.ResultsetItem(0)
               .ResultsetItemExpression()
               .ExprFieldAlias()
               .Should().Be(query.Entities[0].Alias);

            select.ResultsetItem(0)
                .ResultsetItemExpression()
                .ExprFieldName()
                .Should().Be(category[0].Name);

            select.ResultsetItem(1)
                .ResultsetItemExpression()
                .ExprFieldAlias()
                .Should().Be(query.Entities[1].Alias);

            select.ResultsetItem(1)
                .ResultsetItemExpression()
                .ExprFieldName()
                .Should().Be(product[0].Name);

            select.ResultsetItem(2)
               .ResultsetItemExpression()
               .ExprFieldAlias()
               .Should().Be(query.Entities[2].Alias);

            select.ResultsetItem(2)
                .ResultsetItemExpression()
                .ExprFieldName()
                .Should().Be(product[1].Name);
        }

        [Fact]
        public void From_SingleTable()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0)
                .TableName()
                .Should().HaveValue(category.Name);

            select.Table(0).TableJoin()
                .Should().BeNull();
        }

        [Fact]
        public void From_TwoTables_NoJoin()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;

            var query = connection.GetSelectQueryBuilder(category);
            query.AddTable(product, TableJoinType.None);

            query.AddToResultset(category[0]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0).TableName()
                .Should().HaveValue(category.Name);

            select.Table(0).TableJoin()
                .Should().BeNull();

            select.Table(1).TableName()
               .Should().HaveValue(product.Name);

            select.Table(1).TableJoin()
                .Should().BeNull();

            var categoryAlias = select.Table(0).TableAlias().Value;
            var productAlias = select.Table(1).TableAlias().Value;

            categoryAlias.Should()
                .NotBeNullOrEmpty();

            productAlias.Should()
                .NotBeNullOrEmpty();

            categoryAlias.Should().NotBe(productAlias);

            categoryAlias.Should().Be(query.Entities[0].Alias);
            productAlias.Should().Be(query.Entities[1].Alias);
        }
    }
}

