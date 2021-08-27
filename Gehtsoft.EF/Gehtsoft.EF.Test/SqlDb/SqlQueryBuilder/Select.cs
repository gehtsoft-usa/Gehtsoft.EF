using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils;
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

            select.Should()
                .HaveResultsetSize(1)
                .And.HaveResultsetItemExpression(0, rsi =>
                    rsi.Should()
                        .BeFieldExpression()
                        .And.HaveFieldAlias(categoryAlias)
                        .And.HaveFieldName(category[0].Name));
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

            select.Should().HaveDistinctClause();
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

            select.Should().HaveNoDistinctClause();
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

            select.Should()
                .HaveResultsetSize(1)
                .And.HaveResultsetItemExpression(0, rsi =>
                    rsi.Should()
                        .BeFieldExpression()
                        .And.HaveFieldAlias(categoryAlias)
                        .And.HaveFieldName(category[1].Name))
                .And.HaveResultsetItemAlias(0, "ma");
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

            select.Should()
                .HaveResultsetSize(1)
                .And.HaveResultsetItemExpression(0, rsi =>
                    rsi.Should()
                        .BeFieldExpression()
                        .And.HaveFieldAlias(categoryAlias)
                        .And.HaveFieldName(category[0].Name))
                .And.HaveResultsetItemAlias(0, "myalias");
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

            select.Should()
                .HaveResultsetSize(1)
                .And.HaveResultsetItemExpression(0, rsi => rsi.Should().BeCountAllCall())
                .And.HaveResultsetItemAlias(0, "count");
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

            select.Should()
               .HaveResultsetSize(1)
               .And.HaveResultsetItemExpression(0, rsi =>
                            rsi.Should()
                                .BeCallExpression(expectedFunction)
                                .And.ItsParameter(0, p =>
                                    p.Should().BeFieldExpression()
                                        .And.HaveFieldAlias(query.Entities[0].Alias)
                                        .And.HaveFieldName(category[0].Name)))
               .And.HaveResultsetItemAlias(0, "agg");
        }

        [Fact]
        public void Resultset_AddAll_OneTable()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            query.AddToResultset(category);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(category.Count);

            for (int i = 0; i < category.Count; i++)
            {
                select.Should().HaveResultsetItemExpression(i, e =>
                        e.Should().BeFieldExpression()
                         .And.HaveFieldAlias(query.Entities[0].Alias)
                         .And.HaveFieldName(category[i].Name));
            }
        }

        [Fact]
        public void TableDescription()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            query.AddExpressionToResultset("a+b", DbType.String, false, "custom");
            query.AddToResultset(AggFn.Max, category[nameof(Category.CategoryName)], "max");
            query.AddToResultset(AggFn.Sum, category[nameof(Category.CategoryID)], "sum");
            query.AddToResultset(category);

            query.PrepareQuery();

            var td = query.QueryTableDescriptor;
            td.Should().HaveCount(category.Count + 3);

            td[0].Name.Should().Be("custom");
            td[0].DbType.Should().Be(DbType.String);

            td[1].Name.Should().Be("max");
            td[1].DbType.Should().Be(category[nameof(Category.CategoryName)].DbType);

            td[2].Name.Should().Be("sum");
            td[2].DbType.Should().Be(category[nameof(Category.CategoryID)].DbType);

            for (int i = 0; i < category.Count; i++)
            {
                td[i + 3].Name.Should().Be(category[i].Name);
                td[i + 3].DbType.Should().Be(category[i].DbType);
            }
        }

        [Fact]
        public void Resultset_AddAll_TwoTables()
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

            select.Should().HaveResultsetSize(category.Count + product.Count);

            for (int i = 0; i < category.Count; i++)
            {
                select.Should().HaveResultsetItemExpression(i, e =>
                        e.Should().BeFieldExpression()
                                  .And.HaveFieldAlias(query.Entities[0].Alias)
                                  .And.HaveFieldName(category[i].Name));
            }

            for (int i = 0, s = category.Count; i < product.Count; i++)
            {
                select.Should().HaveResultsetItemExpression(s + i, e =>
                        e.Should().BeFieldExpression()
                                  .And.HaveFieldAlias(query.Entities[1].Alias)
                                  .And.HaveFieldName(product[i].Name));
            }
        }

        [Fact]
        public void Resultset_AddAll_TwoSameTables()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            var secondEntry = query.AddTable(category, TableJoinType.None);

            query.AddToResultset(category);
            query.AddToResultset(category, secondEntry);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(category.Count * 2);

            for (int i = 0; i < category.Count; i++)
            {
                select.Should().HaveResultsetItemExpression(i, e =>
                        e.Should().BeFieldExpression()
                                  .And.HaveFieldAlias(query.Entities[0].Alias)
                                  .And.HaveFieldName(category[i].Name));
            }

            for (int i = 0, s = category.Count; i < category.Count; i++)
            {
                select.Should().HaveResultsetItemExpression(s + i, e =>
                        e.Should().BeFieldExpression()
                                  .And.HaveFieldAlias(query.Entities[1].Alias)
                                  .And.HaveFieldName(category[i].Name));
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

            select.Should().HaveResultsetSize(2)

                .And.HaveResultsetItemExpression(0, e =>
                        e.Should().BeFieldExpression()
                            .And.HaveFieldAlias(query.Entities[0].Alias)
                            .And.HaveFieldName(category[0].Name))

                .And.HaveResultsetItemExpression(1, e =>
                        e.Should().BeFieldExpression()
                            .And.HaveFieldAlias(query.Entities[1].Alias)
                            .And.HaveFieldName(product[0].Name));
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

            select.Should().HaveResultsetSize(3)

               .And.HaveResultsetItemExpression(0, e =>
                       e.Should().BeFieldExpression()
                           .And.HaveFieldAlias(query.Entities[0].Alias)
                           .And.HaveFieldName(category[0].Name))

               .And.HaveResultsetItemExpression(1, e =>
                       e.Should().BeFieldExpression()
                           .And.HaveFieldAlias(query.Entities[1].Alias)
                           .And.HaveFieldName(product[0].Name))

               .And.HaveResultsetItemExpression(2, e =>
                       e.Should().BeFieldExpression()
                           .And.HaveFieldAlias(query.Entities[2].Alias)
                           .And.HaveFieldName(product[1].Name));
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

            select.Table(0).Should()
                .HaveTableName(category.Name)
                .And.NotBeJoin();
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

            select.Table(0).Should()
                .HaveTableName(category.Name)
                .And.NotBeJoin();

            select.Table(1).Should()
                .HaveTableName(product.Name)
                .And.NotBeJoin();

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

        [Fact]
        public void From_ComplexAutoAttachLogic()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;
            var supplier = AllEntities.Get<Supplier>().TableDescriptor;
            var order = AllEntities.Get<Order>().TableDescriptor;
            var orderDetail = AllEntities.Get<OrderDetail>().TableDescriptor;

            var query = connection.GetSelectQueryBuilder(order);
            var joinOfDetail = query.AddTable(orderDetail, true);
            var joinOfProduct = query.AddTable(product, true);
            var joinOfSupplier = query.AddTable(supplier, true);
            var joinOfCategory = query.AddTable(category, true);

            joinOfDetail.JoinType.Should().Be(TableJoinType.Inner);
            joinOfDetail.ConnectedToTable.Should().Be(query.Entities[0]);
            joinOfDetail.ConnectedToField.Should().Be(order.PrimaryKey);

            joinOfProduct.JoinType.Should().Be(TableJoinType.Inner);
            joinOfProduct.ConnectedToTable.Should().Be(joinOfDetail);
            joinOfProduct.ConnectedToField.Should().Be(orderDetail[nameof(OrderDetail.Product)]);

            joinOfSupplier.JoinType.Should().Be(TableJoinType.Inner);
            joinOfSupplier.ConnectedToTable.Should().Be(joinOfProduct);
            joinOfSupplier.ConnectedToField.Should().Be(product[nameof(Product.Supplier)]);

            joinOfCategory.JoinType.Should().Be(TableJoinType.Inner);
            joinOfCategory.ConnectedToTable.Should().Be(joinOfProduct);
            joinOfCategory.ConnectedToField.Should().Be(product[nameof(Product.Category)]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(5);
        }

        [Theory]
        [InlineData(false, "INNER")]
        [InlineData(true, "LEFT")]
        public void From_TwoTables_Autojoin_AttachDictionary(bool nullableReference, string expectedJoinType)
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;

            bool savedNullable = product[nameof(Product.Category)].Nullable;
            product[nameof(Product.Category)].Nullable = nullableReference;
            using var delayed = new DelayedAction(() => product[nameof(Product.Category)].Nullable = savedNullable);

            var query = connection.GetSelectQueryBuilder(product);
            query.AddTable(category, true);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Table(0).Should()
                .HaveTableName(product.Name)
                .And.NotBeJoin();

            select.Table(1).Should()
                .HaveTableName(category.Name)
                .And.BeJoin("JOIN_TYPE_" + expectedJoinType);

            var expr = select.Table(1).TableJoinCondition();

            expr.Should().BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p =>
                    p.Should().BeFieldExpression()
                        .And.HaveFieldAlias(query.Entities[1].Alias)
                        .And.HaveFieldName(category.PrimaryKey.Name))
                .And.ItsParameter(1, p =>
                    p.Should().BeFieldExpression()
                        .And.HaveFieldAlias(query.Entities[0].Alias)
                        .And.HaveFieldName(product[nameof(Product.Category)].Name));
        }

        [Theory]
        [InlineData(false, "INNER")]
        [InlineData(true, "RIGHT")]
        public void From_TwoTables_Autojoin_AttachDataToDictionary(bool nullableReference, string expectedJoinType)
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;

            bool savedNullable = product[nameof(Product.Category)].Nullable;
            product[nameof(Product.Category)].Nullable = nullableReference;
            using var delayed = new DelayedAction(() => product[nameof(Product.Category)].Nullable = savedNullable);

            var query = connection.GetSelectQueryBuilder(category);
            query.AddTable(product, true);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Table(0).Should()
                .HaveTableName(category.Name)
                .And.NotBeJoin();

            select.Table(1).Should()
                .HaveTableName(product.Name)
                .And.BeJoin("JOIN_TYPE_" + expectedJoinType);

            var expr = select.Table(1).TableJoinCondition();

            expr.Should().BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p =>
                    p.Should().BeFieldExpression(query.Entities[1].Alias, product[nameof(Product.Category)].Name))
                .And.ItsParameter(1, p =>
                    p.Should().BeFieldExpression(query.Entities[0].Alias, category.PrimaryKey.Name));
        }

        [Fact]
        public void From_TwoTables_Join_Manually()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.OuterJoinSupportedSpec = true;

            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;

            var query = connection.GetSelectQueryBuilder(product);

            var obe = query.Entities[0];

            var qbe = query.AddTable(category, false);

            qbe.JoinType.Should().Be(TableJoinType.None);

            qbe.JoinType = TableJoinType.Outer;
            qbe.On
                .Property(product[nameof(Product.ProductName)])
                .Eq().Property(category[nameof(Category.CategoryName)])
                .And().Property(category[nameof(Category.CategoryID)])
                .Gt().Value(10);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Table(0)
                .Should().HaveTableName(product.Name)
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName(category.Name)
                .And.BeJoin("JOIN_TYPE_FULL");

            var onExpr = select.Table(1).TableJoinCondition();

            onExpr.Should()
                .BeOpExpression("AND_OP")
                .And.ItsParameter(0, p =>
                {
                    p.Should().BeOpExpression("EQ_OP")
                        .And.ItsParameter(0, p => p.Should().BeFieldExpression(obe.Alias, product[nameof(Product.ProductName)].Name))
                        .And.ItsParameter(1, p => p.Should().BeFieldExpression(qbe.Alias, category[nameof(Category.CategoryName)].Name));
                })
                .And.ItsParameter(1, p =>
                {
                    p.Should().BeOpExpression("GT_OP")
                        .And.ItsParameter(0, p => p.Should().BeFieldExpression(qbe.Alias, category[nameof(Category.CategoryID)].Name))
                        .And.ItsParameter(1, p => p.Should().BeConstant(10));
                });
        }

        [Fact]
        public void LimitOffset_None()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.PrepareQuery();

            var select = query.Query.ParseSql().SelectStatement();

            select.Should()
                .HaveNoLimitClause()
                .And.HaveNoOffsetClause();
        }

        [Fact]
        public void LimitOffset_Limit()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.Limit = 5;
            query.PrepareQuery();

            var select = query.Query.ParseSql().SelectStatement();

            select.Should()
                .HaveLimitClause(5)
                .And.HaveNoOffsetClause();
        }

        [Fact]
        public void LimitOffset_Offset()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.Skip = 5;
            query.PrepareQuery();

            var select = query.Query.ParseSql().SelectStatement();
            select.SelectLimitClause().Should().BeNull();

            select.Should()
                .HaveNoLimitClause()
                .And.HaveOffsetClause(5);
        }

        [Fact]
        public void LimitOffset_Both()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.Skip = 1;
            query.Limit = 5;
            query.PrepareQuery();

            var select = query.Query.ParseSql().SelectStatement();
            select.Should()
                .HaveLimitClause(5)
                .And.HaveOffsetClause(1);
        }

        [Fact]
        public void Where_NoWhere()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();
            select.Should().HaveNoWhereClause();
        }

        [Fact]
        public void Where_Simple()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.Where.Raw("TRUE");
            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();
            select.Should().HaveWhereClause();
            select.SelectWhere().ClauseCondition().Should().BeConstant(true);
        }

        [Fact]
        public void Where_Subquery()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);

            var subquery = connection.GetSelectQueryBuilder(product);
            subquery.AddToResultset(product.PrimaryKey);
            subquery.Where
                .Property(product[nameof(Product.Category)])
                .Eq()
                .Reference(query.GetReference(category[nameof(Category.CategoryID)]));

            query.AddToResultset(category.PrimaryKey);
            query.AddToResultset(category[nameof(Category.CategoryName)]);
            query.Where.Exists().Query(subquery);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(2)
                .And.HaveResultsetItemExpression(0, e => e.Should().BeFieldExpression().And.HaveFieldName(category.PrimaryKey.Name))
                .And.HaveResultsetItemExpression(1, e => e.Should().BeFieldExpression().And.HaveFieldName(category[nameof(Category.CategoryName)].Name));

            select.AllTables().Should().HaveCount(1);
            select.Table(0)
                .Should().HaveTableName(category.Name)
                .And.HaveTableAlias(query.Entities[0].Alias);

            var where = select.SelectWhere().ClauseCondition();

            where.Should().BeOpExpression("EXISTS_OP")
                .And.ItsParameter(0, p => p.Should().HaveSymbol("SELECT"));

            var subselect = where.ExprOpArg(0);

            subselect.AllTables().Should().HaveCount(1);

            subselect.AllTables().Should().HaveCount(1);
            subselect.Table(0)
                .Should().HaveTableName(product.Name)
                .And.HaveTableAlias(subquery.Entities[0].Alias);

            subselect.Should()
                .HaveResultsetSize(1)
                .And.HaveResultsetItemExpression(0, e => e.Should().BeFieldExpression().And.HaveFieldName(product.PrimaryKey.Name));

            subselect.Should().HaveWhereClause();

            where = subselect.SelectWhere().ClauseCondition();

            where.Should().BeOpExpression("EQ_OP")
                .And.ItsParameter(0, p => p.Should().BeFieldExpression(subquery.Entities[0].Alias, product[nameof(Product.Category)].Name))
                .And.ItsParameter(1, p => p.Should().BeFieldExpression(query.Entities[0].Alias, category.PrimaryKey.Name));
        }

        [Fact]
        public void Having_NoHaving()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();
            select.Should().HaveNoHavingClause();
        }

        [Fact]
        public void Having_Simple()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddToResultset(category[0]);
            query.Having.Raw("TRUE");
            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();
            select.Should().HaveHavingClause();
            select.SelectHaving().ClauseCondition().Should().BeConstant(true);
        }

        [Fact]
        public void Having_Coundition()
        {
            using var connection = new DummySqlConnection();
            var category = AllEntities.Get<Category>().TableDescriptor;
            var product = AllEntities.Get<Product>().TableDescriptor;

            var query = connection.GetSelectQueryBuilder(category);
            query.AddTable(product);

            query.AddToResultset(product[nameof(Product.Category)]);
            query.AddToResultset(category[nameof(Category.CategoryName)]);
            query.AddToResultset(AggFn.Count);

            query.AddGroupBy(product[nameof(Product.Category)]);

            query.Having.And().Count().Gt().Value(10);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveHavingClause();
            var expr = select.SelectHaving().ClauseCondition();

            expr.Should().BeOpExpression("GT_OP")
                .And.ItsParameter(0, p => p.Should().BeCountAllCall())
                .And.ItsParameter(1, p => p.Should().BeConstant(10));
        }

        [Fact]
        public void OrderBy_NoOrder()
        {
            using var connection = new DummySqlConnection();
            var product = AllEntities.Get<Product>().TableDescriptor;
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddTable(product);

            query.AddToResultset(product[nameof(Product.ProductID)]);
            query.AddToResultset(product[nameof(Product.ProductName)]);
            query.AddToResultset(category[nameof(Category.CategoryName)]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveNoSortOrder();
        }

        [Fact]
        public void OrderBy()
        {
            using var connection = new DummySqlConnection();
            var product = AllEntities.Get<Product>().TableDescriptor;
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddTable(product);

            query.AddToResultset(product[nameof(Product.ProductID)]);
            query.AddToResultset(product[nameof(Product.ProductName)]);
            query.AddToResultset(category[nameof(Category.CategoryName)]);

            query.AddOrderBy(product[nameof(Product.ProductID)]);
            query.AddOrderBy(product[nameof(Product.ProductName)], SortDir.Asc);
            query.AddOrderBy(category[nameof(Category.CategoryName)], SortDir.Desc);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveSortOrder(3);
            select.Should().HaveSortOrder(0, query.Entities[1].Alias, product[nameof(Product.ProductID)].Name, "ASC");
            select.Should().HaveSortOrder(1, query.Entities[1].Alias, product[nameof(Product.ProductName)].Name, "ASC");
            select.Should().HaveSortOrder(2, query.Entities[0].Alias, category[nameof(Category.CategoryName)].Name, "DESC");
        }

        [Fact]
        public void GroupBy_NoGroups()
        {
            using var connection = new DummySqlConnection();
            var product = AllEntities.Get<Product>().TableDescriptor;
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(category);
            query.AddTable(product);

            query.AddToResultset(product[nameof(Product.ProductID)]);
            query.AddToResultset(product[nameof(Product.ProductName)]);

            query.PrepareQuery();
            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveNoGroupBy();
        }

        [Fact]
        public void GroupBy_Simple()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.AllNonAggregatesInGroupBySpec = false;

            var product = AllEntities.Get<Product>().TableDescriptor;
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(product);
            query.AddTable(category);

            query.AddToResultset(product[nameof(Product.ProductID)]);
            query.AddToResultset(product[nameof(Product.ProductName)]);
            query.AddToResultset(product[nameof(Product.Supplier)]);
            query.AddToResultset(product[nameof(Product.Category)]);
            query.AddToResultset(AggFn.Count);
            query.AddToResultset(AggFn.Sum, product[nameof(Product.UnitsOnOrder)]);

            query.AddGroupBy(product[nameof(Product.ProductID)]);

            query.PrepareQuery();

            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveGroupBy(1);
            select.Should().HaveGroupBy(0, query.Entities[0].Alias, product[nameof(Product.ProductID)].Name);
        }

        [Fact]
        public void GroupBy_AllNonAggregatesMode()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.AllNonAggregatesInGroupBySpec = true;

            var product = AllEntities.Get<Product>().TableDescriptor;
            var category = AllEntities.Get<Category>().TableDescriptor;
            var query = connection.GetSelectQueryBuilder(product);
            query.AddTable(category);

            query.AddToResultset(product[nameof(Product.ProductID)]);
            query.AddToResultset(product[nameof(Product.ProductName)]);
            query.AddToResultset(product[nameof(Product.Supplier)]);
            query.AddToResultset(category[nameof(Category.CategoryName)]);
            query.AddToResultset(AggFn.Count);
            query.AddToResultset(AggFn.Sum, product[nameof(Product.UnitsOnOrder)]);

            query.AddGroupBy(product[nameof(Product.ProductName)]);

            query.PrepareQuery();

            var select = query.Query.ParseSql().SelectStatement();

            select.Should().HaveGroupBy(4);
            select.Should().HaveGroupBy(0, query.Entities[0].Alias, product[nameof(Product.ProductName)].Name);
            select.Should().HaveGroupBy(1, query.Entities[0].Alias, product[nameof(Product.ProductID)].Name);
            select.Should().HaveGroupBy(2, query.Entities[0].Alias, product[nameof(Product.Supplier)].Name);
            select.Should().HaveGroupBy(3, query.Entities[1].Alias, category[nameof(Category.CategoryName)].Name);
        }

        public class TestHierarchicalSelectBuilder : HierarchicalSelectQueryBuilder
        {
            public TestHierarchicalSelectBuilder(Db.SqlDb.SqlDbLanguageSpecifics specifics, TableDescriptor table, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameterName) : base(specifics, table, parentReferenceColumn, rootParameterName)
            {
            }

            public bool PrepareQueryCalled { get; set; }

            public override void PrepareQuery()
            {
                PrepareQueryCalled = true;
            }
        }

        [Fact]
        public void HierachicalSelectCore_IdOnly()
        {
            using var connection = new DummySqlConnection();
            var employee = AllEntities.Get<Employee>().TableDescriptor;

            var builder = new TestHierarchicalSelectBuilder(connection.GetLanguageSpecifics(), employee, employee[nameof(Employee.ReportsTo)], "rootid")
            {
                IdOnlyMode = true
            };

            var resultDescriptor = builder.QueryTableDescriptor;

            builder.PrepareQueryCalled.Should().BeTrue();

            resultDescriptor.Should().HaveCount(1);
            resultDescriptor[0].Name.Should().Be("id");
            resultDescriptor[0].DbType.Should().Be(employee.PrimaryKey.DbType);
        }

        [Fact]
        public void HierachicalSelectCore_Complete()
        {
            using var connection = new DummySqlConnection();
            var employee = AllEntities.Get<Employee>().TableDescriptor;

            var builder = new TestHierarchicalSelectBuilder(connection.GetLanguageSpecifics(), employee, employee[nameof(Employee.ReportsTo)], "rootid")
            {
                IdOnlyMode = false
            };

            var resultDescriptor = builder.QueryTableDescriptor;

            builder.PrepareQueryCalled.Should().BeTrue();

            resultDescriptor.Should().HaveCount(3);
            resultDescriptor[0].Name.Should().Be("id");
            resultDescriptor[0].DbType.Should().Be(employee.PrimaryKey.DbType);
            resultDescriptor[1].Name.Should().Be("parent");
            resultDescriptor[1].DbType.Should().Be(employee.PrimaryKey.DbType);
            resultDescriptor[2].Name.Should().Be("level");
            resultDescriptor[2].DbType.Should().Be(DbType.Int32);
        }
    }
}

