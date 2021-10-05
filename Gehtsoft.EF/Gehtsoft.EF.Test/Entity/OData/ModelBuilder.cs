using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.OData
{
    public class ModelBuilder : IClassFixture<ModelBuilder.Fixture>
    {
        public class Fixture
        {
            public EdmModelBuilder Builder { get; }

            public Fixture()
            {
                var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Order).Assembly }, "northwind", false);
                var builder = new EdmModelBuilder();
                builder.Build(entities, "northwind");
                Builder = builder;
            }
        }

        private readonly Fixture mFixture;

        public ModelBuilder(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Fact]
        public void HaveModel()
        {
            mFixture.Builder.Model.Should().NotBeNull();
        }

        [Fact]
        public void HaveNamespace()
        {
            mFixture.Builder.Model.DeclaredNamespaces.Should().HaveCount(1);
            mFixture.Builder.Model.DeclaredNamespaces.Should().Contain("northwind");
        }

        [Theory]
        [InlineData(typeof(Customer))]
        [InlineData(typeof(Employee))]
        [InlineData(typeof(Territory))]
        [InlineData(typeof(EmployeeTerritory))]
        [InlineData(typeof(Category))]
        [InlineData(typeof(Product))]
        [InlineData(typeof(Shipper))]
        [InlineData(typeof(Order))]
        [InlineData(typeof(OrderDetail))]
        [InlineData(typeof(Supplier))]
        public void HaveElements(Type type)
        {
            mFixture.Builder.Model.SchemaElements
                .Should().Contain(e => e.Name == type.Name + "_Type");
        }

        [Theory]
        [InlineData(nameof(Customer), typeof(Customer))]
        [InlineData(nameof(Employee), typeof(Employee))]
        [InlineData(nameof(Territory), typeof(Territory))]
        [InlineData(nameof(EmployeeTerritory), typeof(EmployeeTerritory))]
        [InlineData(nameof(Category), typeof(Category))]
        [InlineData(nameof(Product), typeof(Product))]
        [InlineData(nameof(Shipper), typeof(Shipper))]
        [InlineData(nameof(Order), typeof(Order))]
        [InlineData(nameof(OrderDetail), typeof(OrderDetail))]
        [InlineData(nameof(Supplier), typeof(Supplier))]
        public void FindTypeDefinition(string name, Type type)
        {
            var t = mFixture.Builder.Model
                .FindType($"northwind.{name}_Type");

            t.Should()
                .NotBeNull()
                .And.BeAssignableTo<IEdmEntityType>();

            var et = t.As<IEdmEntityType>();
            var entity = AllEntities.Get(type);
            foreach (var property in entity.TableDescriptor)
            {
                et.FindProperty(property.ID)
                    .Should().NotBeNull();
            }
        }

        [Fact]
        public void Parse_Entity()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Employee", UriKind.Relative));
            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();
            path.Should().HaveCount(1);
            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Employee_Type)");
        }

        [Fact]
        public void Parse_Entity_Wrong()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Emploiee", UriKind.Relative));
            ((Action)(() => odataUriParser.ParseUri())).Should().Throw<ODataUnrecognizedPathException>();
        }

        [Fact]
        public void Parse_EntityCount()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Employee/$count", UriKind.Relative));
            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();
            path.Should().HaveCount(2);
            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Employee_Type)");

            path[1].EdmType.TypeKind.Should().Be(EdmTypeKind.Primitive);
            path[1].Identifier.Should().Be("$count");
        }

        [Fact]
        public void Parse_EntityById()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Employee(1)", UriKind.Relative));
            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();
            path.Should().HaveCount(2);
            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Employee_Type)");

            path[1].EdmType.TypeKind.Should().Be(EdmTypeKind.Entity);
            path[1].Should().BeOfType<KeySegment>();
            path[1].As<KeySegment>()
                .Keys.Should().HaveCount(1);

            var key = path[1].As<KeySegment>().Keys.First();
            key.Key.Should().Be("EmployeeID");
            key.Value.Should().Be(1);
        }

        [Fact]
        public void Parse_EntityField()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Employee?$select=LastName", UriKind.Relative));
            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();
            path.Should().HaveCount(1);
            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Employee_Type)");

            odataUri.SelectAndExpand.Should().NotBeNull();
            odataUri.SelectAndExpand.SelectedItems.Should().HaveCount(1);
            odataUri
                .SelectAndExpand
                .SelectedItems.Should().HaveCount(1);

            var res = odataUri
                .SelectAndExpand
                .SelectedItems.First().As<PathSelectItem>().SelectedPath.ToArray();

            res[0].Should()
                .BeOfType<PropertySegment>();

            res[0].As<PropertySegment>().Identifier.Should().Be("LastName");
        }

        [Fact]
        public void Parse_Property_Wrong()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Employee$select=ListName", UriKind.Relative));
            ((Action)(() => odataUriParser.ParseUri())).Should().Throw<ODataUnrecognizedPathException>();
        }

        [Fact]
        public void Parse_Join_ManyToOne()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Product(1)/Category", UriKind.Relative));

            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();

            path.Should().HaveCount(3);
            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Product_Type)");

            path[1].EdmType.TypeKind.Should().Be(EdmTypeKind.Entity);
            path[1].EdmType.FullTypeName().Should().Be("northwind.Product_Type");
            path[1].Should().BeOfType<KeySegment>();
            path[1].As<KeySegment>()
                .Keys.Should().HaveCount(1);

            var key = path[1].As<KeySegment>().Keys.First();
            key.Key.Should().Be("ProductID");
            key.Value.Should().Be(1);

            path[2].EdmType.TypeKind.Should().Be(EdmTypeKind.Entity);
            path[2].EdmType.FullTypeName().Should().Be("northwind.Category_Type");
        }

        [Fact]
        public void Parse_Join_OneToMany()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Category(1)/Product", UriKind.Relative));

            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();

            path.Should().HaveCount(3);
            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Category_Type)");

            path[1].EdmType.TypeKind.Should().Be(EdmTypeKind.Entity);
            path[1].EdmType.FullTypeName().Should().Be("northwind.Category_Type");
            path[1].Should().BeOfType<KeySegment>();
            path[1].As<KeySegment>()
                .Keys.Should().HaveCount(1);

            var key = path[1].As<KeySegment>().Keys.First();
            key.Key.Should().Be("CategoryID");
            key.Value.Should().Be(1);

            path[2].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[2].EdmType.FullTypeName().Should().Be("Collection(northwind.Product_Type)");
        }

        [Fact]
        public void Parse_Join_ManyToOne_Then_One_ToMany()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Product(1)/Category/Product", UriKind.Relative));

            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();

            path.Should().HaveCount(4);

            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Product_Type)");

            path[1].EdmType.TypeKind.Should().Be(EdmTypeKind.Entity);
            path[1].EdmType.FullTypeName().Should().Be("northwind.Product_Type");
            path[1].Should().BeOfType<KeySegment>();
            path[1].As<KeySegment>()
                .Keys.Should().HaveCount(1);

            var key = path[1].As<KeySegment>().Keys.First();
            key.Key.Should().Be("ProductID");
            key.Value.Should().Be(1);

            path[2].EdmType.TypeKind.Should().Be(EdmTypeKind.Entity);
            path[2].EdmType.FullTypeName().Should().Be("northwind.Category_Type");

            path[3].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[3].EdmType.FullTypeName().Should().Be("Collection(northwind.Product_Type)");
        }

        [Fact]
        public void Parse_Join_Incorrect()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Product(1)/Employee", UriKind.Relative));
            ((Action)(() => odataUriParser.ParseUri())).Should().Throw<ODataUnrecognizedPathException>();
        }

        [Fact]
        public void Parse_Expand()
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri("/Product?$expand=Category", UriKind.Relative));

            var odataUri = odataUriParser.ParseUri();
            odataUri.Should().NotBeNull();
            var path = odataUri.Path.ToArray();

            path.Should().HaveCount(1);
            path[0].EdmType.TypeKind.Should().Be(EdmTypeKind.Collection);
            path[0].EdmType.FullTypeName().Should().Be("Collection(northwind.Product_Type)");

            odataUri.SelectAndExpand.Should().NotBeNull();
            odataUri.SelectAndExpand.SelectedItems.Should().HaveCount(1);
            odataUri
                .SelectAndExpand
                .SelectedItems.Should().HaveCount(1);

            var expand = odataUri
                    .SelectAndExpand
                    .SelectedItems.First();
            expand.Should()
                .BeOfType<ExpandedNavigationSelectItem>()
                .And.Subject.As<ExpandedNavigationSelectItem>()
                    .NavigationSource.Name.Should().Be("Category");
        }

        [Theory]
        [InlineData("eq", "EQ_OP")]
        [InlineData("ne", "NEQ_OP")]
        [InlineData("gt", "GT_OP")]
        [InlineData("lt", "LS_OP")]
        [InlineData("ge", "GE_OP")]
        [InlineData("le", "LE_OP")]
        public void Query_Filter_CmpOp(string odataOp, string sqlOp)
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri($"/Product?$filter=ProductID {odataOp} 1", UriKind.Relative));
            using var connection = new DummySqlConnection();

            var odataQuery = new ODataToQuery(mFixture.Builder, odataUriParser.ParseUri(), connection);
            var builder = odataQuery.BuildQuery();
            builder.PrepareQuery();
            var alias = (builder as QueryWithWhereBuilder).Entities[0].Alias;

            var select = builder.Query.ParseSql().SelectStatement();
            select.Should().HaveWhereClause();

            var condition = select.SelectWhere().ClauseCondition();

            condition.Should().BeBinaryOp(sqlOp);
            condition.ExprOpArg(0)
                .Should().BeFieldExpression(alias, "productID");

            condition.ExprOpArg(1)
                .Should().BeParamExpression();

            odataQuery.BindParams
                .Should()
                .HaveElementMatching(condition.ExprOpArg(1).ExprParamName(), v => v is int i && i == 1);
        }

        [Theory]
        [InlineData("and", "AND_OP")]
        [InlineData("or", "OR_OP")]
        public void Query_Filter_LogOp(string odataOp, string sqlOp)
        {
            var odataUriParser = new ODataUriParser(mFixture.Builder.Model, new Uri($"/Product?$filter=ProductID gt 1 {odataOp} ProductID lt 5", UriKind.Relative));
            using var connection = new DummySqlConnection();

            var odataQuery = new ODataToQuery(mFixture.Builder, odataUriParser.ParseUri(), connection);
            var builder = odataQuery.BuildQuery();
            builder.PrepareQuery();
            var alias = (builder as QueryWithWhereBuilder).Entities[0].Alias;

            var select = builder.Query.ParseSql().SelectStatement();
            select.Should().HaveWhereClause();

            var condition = select.SelectWhere().ClauseCondition();

            condition.Should().BeBinaryOp(sqlOp);

            condition.ExprOpArg(0)
                .Should().BeBinaryOp("GT_OP");

            condition.ExprOpArg(1)
                .Should().BeBinaryOp("LS_OP");
        }
    }
}


