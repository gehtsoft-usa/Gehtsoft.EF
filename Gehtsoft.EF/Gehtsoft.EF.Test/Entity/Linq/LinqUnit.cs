using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DnsClient;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Tools;
using Gehtsoft.EF.Test.Northwind;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Linq
{
    public class LinqUnit
    {
        [Entity(Scope = "linq1")]
        public class Dict
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        };

        [Entity(Scope = "linq1")]
        public class Entity
        {
            [AutoId]
            public int ID { get; set; }

            [ForeignKey]
            public Dict Reference { get; set; }

            [EntityProperty]
            public string StringValue { get; set; }

            [EntityProperty]
            public int IntValue { get; set; }

            [EntityProperty]
            public double RealValue { get; set; }

            [EntityProperty]
            public decimal DecimalValue { get; set; }

            [EntityProperty]
            public bool BooleanValue { get; set; }

            [EntityProperty]
            public DateTime DateTimeValue { get; set; }

            [EntityProperty]
            public Guid GuidValue { get; set; }

            [EntityProperty]
            public int? NullableIntValue { get; set; }

            [EntityProperty]
            public DateTime? NullableDataTime { get; set; }
        }

        [Theory]
        [InlineData(typeof(string), "abcd")]
        [InlineData(typeof(int), 123)]
        [InlineData(typeof(int?), null)]
        [InlineData(typeof(bool), true)]
        [InlineData(typeof(bool), false)]
        public void Constant(Type type, object value)
        {
            value = TestValue.Translate(type, value);

            var ex = Expression.Constant(value);
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetGenericSelectEntityQuery<Dict>();
            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Params.Should().HaveCount(1);
            r.Params[0].Value.Should().Be(value);

            r.Expression.ToString().Should().Be(r.Params[0].Name);
        }

        [Fact]
        public void Field_OneEntityQuery()
        {
            var parameter = Expression.Parameter(typeof(Dict));
            var ex = Expression.PropertyOrField(parameter, nameof(Dict.Name));

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetGenericSelectEntityQuery<Dict>();
            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Expression.ToString().Should().Be(query.GetReference(nameof(Dict.Name)).Alias);
        }

        [Fact]
        public void Field_DictionaryOfMainEntity()
        {
            var parameter = Expression.Parameter(typeof(Entity));
            var ex = Expression.PropertyOrField(Expression.PropertyOrField(parameter, nameof(Entity.Reference)), nameof(Dict.Name));

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Expression.ToString().Should().Be(query.GetReference(typeof(Dict), nameof(Dict.Name)).Alias);
        }

        [Fact]
        public void Field_ConnectedEntity()
        {
            var parameter = Expression.Parameter(typeof(Entity));
            var ex = Expression.PropertyOrField(parameter, nameof(Entity.IntValue));

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Expression.ToString().Should().Be(query.GetReference(typeof(Entity), nameof(Entity.IntValue)).Alias);
        }

        [Fact]
        public void Field_SecondOccurrenceOfEntity()
        {
            var parameter = Expression.Parameter(typeof(Entity[]));
            var ex = Expression.PropertyOrField(
                Expression.ArrayIndex(parameter, new[] { Expression.Constant(1) }),
                nameof(Entity.IntValue));

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity>();
            query.AddEntity<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Expression.ToString().Should().Be(query.GetReference(typeof(Entity), 1, nameof(Entity.IntValue)).Alias);
        }

        [Fact]
        public void Functions_Concat()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit<Entity, string>(e => e.StringValue + e.Reference.Name);

            r.Expression.ToString().Should().MatchPattern(query, "@1.stringvalue || @2.name");
        }

        [Fact]
        public void Field_NullableFieldValue_NotNull()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, bool>(e => e.NullableIntValue != null);
            r.Expression.ToString().Should().MatchPattern(query, "(@1.nullableintvalue˽IS˽NOT˽NULL)");
        }

        [Fact]
        public void Field_NullableFieldValue_IsNull()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, bool>(e => e.NullableIntValue == null);
            r.Expression.ToString().Should().MatchPattern(query, "(@1.nullableintvalue˽IS˽NULL)");
        }

        [Fact]
        public void Field_NullableFieldValue_Value()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, bool>(e => e.NullableIntValue != null && e.NullableIntValue.Value > 5);
            r.Expression.ToString().Should().MatchPattern(query, "((@1.nullableintvalue˽IS˽NOT˽NULL) AND (@1.nullableintvalue > @p))");
        }

        [Fact]
        public void Functions_ImplicitConversion()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, double>(e => Math.Abs((double)e.IntValue));
            r.Expression.ToString().Should().MatchPattern(query, "ABS((TOREAL(@1.intvalue)))");

            r = ec.Visit<Entity, int>(e => (int)e.RealValue + 1);
            r.Expression.ToString().Should().MatchPattern(query, "((TOINT(@1.realvalue)) + @p)");

            r = ec.Visit<Entity, string>(e => e.IntValue.ToString());
            r.Expression.ToString().Should().MatchPattern(query, "TOSTRING(@1.intvalue)");

            r = ec.Visit<Entity, string>(e => e.RealValue.ToString());
            r.Expression.ToString().Should().MatchPattern(query, "TOSTRING(@1.realvalue)");

            r = ec.Visit<Entity, string>(e => e.BooleanValue.ToString());
            r.Expression.ToString().Should().MatchPattern(query, "TOSTRING(@1.booleanvalue)");

            r = ec.Visit<Entity, string>(e => e.NullableIntValue.ToString());
            r.Expression.ToString().Should().MatchPattern(query, "TOSTRING(@1.nullableintvalue)");

            r = ec.Visit<Entity, string>(e => e.DateTimeValue.ToString());
            r.Expression.ToString().Should().MatchPattern(query, "TOSTRING(@1.datetimevalue)");

            r = ec.Visit<Entity, string>(e => e.NullableDataTime.ToString());
            r.Expression.ToString().Should().MatchPattern(query, "TOSTRING(@1.nullabledatatime)");
        }

        [Fact]
        public void Functions_Aggregate()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, int>(e => SqlFunction.Count());
            r.Expression.ToString().Should().MatchPattern(query, "COUNT(*)");
            r.HasAggregates.Should().BeTrue();

            r = ec.Visit<Entity, double>(e => SqlFunction.Sum<double>(e.RealValue));
            r.Expression.ToString().Should().MatchPattern(query, "SUM(@1.realvalue)");
            r.HasAggregates.Should().BeTrue();

            r = ec.Visit<Entity, double>(e => SqlFunction.Min<double>(e.RealValue));
            r.Expression.ToString().Should().MatchPattern(query, "MIN(@1.realvalue)");
            r.HasAggregates.Should().BeTrue();

            r = ec.Visit<Entity, double>(e => SqlFunction.Max<double>(e.RealValue));
            r.Expression.ToString().Should().MatchPattern(query, "MAX(@1.realvalue)");
            r.HasAggregates.Should().BeTrue();

            r = ec.Visit<Entity, double>(e => SqlFunction.Avg<double>(e.RealValue));
            r.Expression.ToString().Should().MatchPattern(query, "AVG(@1.realvalue)");
            r.HasAggregates.Should().BeTrue();
        }

        [Fact]
        public void Functions_Numeric()
        {

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, double>(e => Math.Abs(e.RealValue));
            r.Expression.ToString().Should().MatchPattern(query, "ABS(@1.realvalue)");

            r = ec.Visit<Entity, double>(e => Math.Round(e.RealValue));
            r.Expression.ToString().Should().MatchPattern(query, "ROUND(@1.realvalue, 0)");

            r = ec.Visit<Entity, double>(e => Math.Round(e.RealValue, 2));
            r.Expression.ToString().Should().MatchPattern(query, "ROUND(@1.realvalue, @p)");
            r.Params[0].Value.Should().Be(2);
        }

        [Fact]
        public void Functions_SqlFunctions()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, double>(e => SqlFunction.Abs(e.RealValue));
            r.Expression.ToString().Should().MatchPattern(query, "ABS(@1.realvalue)");

            r = ec.Visit<Entity, double>(e => SqlFunction.Round(e.RealValue, 0));
            r.Expression.ToString().Should().MatchPattern(query, "ROUND(@1.realvalue, @p)");
            r.Params[0].Value.Should().Be(0);

            r = ec.Visit<Entity, double>(e => SqlFunction.Round(e.RealValue, 2));
            r.Expression.ToString().Should().MatchPattern(query, "ROUND(@1.realvalue, @p)");
            r.Params[0].Value.Should().Be(2);

            r = ec.Visit<Entity, double>(e => SqlFunction.Round(1.2345, 2));
            r.Expression.ToString().Should().MatchPattern(query, "ROUND(@p, @p)");
            r.Params[0].Value.Should().Be(1.2345);
            r.Params[1].Value.Should().Be(2);

            r = ec.Visit<Entity, string>(e => SqlFunction.Trim(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "TRIM(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => SqlFunction.TrimLeft(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "LTRIM(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => SqlFunction.TrimRight(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "RTRIM(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => SqlFunction.Upper(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "UPPER(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => SqlFunction.Lower(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "LOWER(@1.stringvalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.Length(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "LENGTH(@1.stringvalue)");

            r = ec.Visit<Entity, int>(e => e.StringValue.Length);
            r.Expression.ToString().Should().MatchPattern(query, "LENGTH(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => SqlFunction.Lower("abc"));
            r.Expression.ToString().Should().MatchPattern(query, "LOWER(@p)");
            r.Params[0].Value.Should().Be("abc");

            r = ec.Visit<Entity, string>(e => SqlFunction.Left(e.StringValue, 5));
            r.Expression.ToString().Should().MatchPattern(query, "LEFT(@1.stringvalue, @p)");
            r.Params[0].Value.Should().Be(5);

            r = ec.Visit<Entity, bool>(e => SqlFunction.Like(e.StringValue, "abc%"));
            r.Expression.ToString().Should().MatchPattern(query, "@1.stringvalue LIKE @p");
            r.Params[0].Value.Should().Be("abc%");

            r = ec.Visit<Entity, string>(e => SqlFunction.ToString(e.IntValue));
            r.Expression.ToString().Should().MatchPattern(query, "TOSTRING(@1.intvalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.ToInteger(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "TOINT(@1.stringvalue)");

            r = ec.Visit<Entity, double>(e => SqlFunction.ToDouble(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "TOREAL(@1.stringvalue)");

            r = ec.Visit<Entity, DateTime>(e => SqlFunction.ToDate(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "TODATE(@1.stringvalue)");

            r = ec.Visit<Entity, DateTime>(e => SqlFunction.ToTimestamp(e.StringValue));
            r.Expression.ToString().Should().MatchPattern(query, "TODATETIME(@1.stringvalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.Year(e.DateTimeValue));
            r.Expression.ToString().Should().MatchPattern(query, "YEAR(@1.datetimevalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.Month(e.DateTimeValue));
            r.Expression.ToString().Should().MatchPattern(query, "MONTH(@1.datetimevalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.Day(e.DateTimeValue));
            r.Expression.ToString().Should().MatchPattern(query, "DAY(@1.datetimevalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.Hour(e.DateTimeValue));
            r.Expression.ToString().Should().MatchPattern(query, "HOUR(@1.datetimevalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.Minute(e.DateTimeValue));
            r.Expression.ToString().Should().MatchPattern(query, "MINUTE(@1.datetimevalue)");

            r = ec.Visit<Entity, int>(e => SqlFunction.Second(e.DateTimeValue));
            r.Expression.ToString().Should().MatchPattern(query, "SECOND(@1.datetimevalue)");

            r = ec.Visit<Entity, string>(e => SqlFunction.Concat(e.StringValue, e.Reference.Name));
            r.Expression.ToString().Should().MatchPattern(query, "@1.stringvalue || @2.name");
        }

        [Fact]
        public void Functions_SqlFunctions_In()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();
            using var subquery = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            subquery.AddToResultset(nameof(Dict.ID));
            subquery.Where.Property(nameof(Dict.Name)).Eq().Reference(query.GetReference(typeof(Dict), nameof(Dict.Name)));


            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Dict, bool>(d => SqlFunction.In(d, subquery));
            r.Expression.ToString().Should().MatchPattern(query, " @2.id IN (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");

            r = ec.Visit<Dict, bool>(d => SqlFunction.In(d, subquery.SelectBuilder));
            r.Expression.ToString().Should().MatchPattern(query, " @2.id IN (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");
        }

        [Fact]
        public void Functions_SqlFunctions_NotIn()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();
            using var subquery = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            subquery.AddToResultset(nameof(Dict.ID));
            subquery.Where.Property(nameof(Dict.Name)).Eq().Reference(query.GetReference(typeof(Dict), nameof(Dict.Name)));


            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Dict, bool>(d => SqlFunction.NotIn(d, subquery));
            r.Expression.ToString().Should().MatchPattern(query, " @2.id NOT IN (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");

            r = ec.Visit<Dict, bool>(d => SqlFunction.NotIn(d, subquery.SelectBuilder));
            r.Expression.ToString().Should().MatchPattern(query, " @2.id NOT IN (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");
        }

        [Fact]
        public void Functions_SqlFunctions_Exist()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();
            using var subquery = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            subquery.AddToResultset(nameof(Dict.ID));
            subquery.Where.Property(nameof(Dict.Name)).Eq().Reference(query.GetReference(typeof(Dict), nameof(Dict.Name)));


            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Dict, bool>(d => SqlFunction.Exists(subquery));
            r.Expression.ToString().Should().MatchPattern(query, " EXISTS (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");

            r = ec.Visit<Dict, bool>(d => SqlFunction.Exists(subquery.SelectBuilder));
            r.Expression.ToString().Should().MatchPattern(query, " EXISTS (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");
        }

        [Fact]
        public void Functions_SqlFunctions_QueryValue()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();
            using var subquery = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            subquery.AddToResultset(nameof(Dict.ID));
            subquery.Where.Property(nameof(Dict.Name)).Eq().Reference(query.GetReference(typeof(Dict), nameof(Dict.Name)));


            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Dict, bool>(d => SqlFunction.Value<int>(subquery) > 5);
            r.Expression.ToString().Should().MatchPattern(query, "((SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name) > @p)");
        }

        [Fact]
        public void Functions_SqlFunctions_NotExist()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();
            using var subquery = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            subquery.AddToResultset(nameof(Dict.ID));
            subquery.Where.Property(nameof(Dict.Name)).Eq().Reference(query.GetReference(typeof(Dict), nameof(Dict.Name)));


            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Dict, bool>(d => SqlFunction.NotExists(subquery));
            r.Expression.ToString().Should().MatchPattern(query, " NOT EXISTS (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");

            r = ec.Visit<Dict, bool>(d => SqlFunction.NotExists(subquery.SelectBuilder));
            r.Expression.ToString().Should().MatchPattern(query, " NOT EXISTS (SELECT @a.id FROM Dict AS @a WHERE @a.name = @2.name)");
        }

        [Fact]
        public void Functions_String()
        {

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQuery<Entity>();

            var ec = new ExpressionCompiler(query);

            var r = ec.Visit<Entity, string>(e => e.StringValue.Trim());
            r.Expression.ToString().Should().MatchPattern(query, "TRIM(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => e.StringValue.TrimStart());
            r.Expression.ToString().Should().MatchPattern(query, "LTRIM(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => e.StringValue.TrimEnd());
            r.Expression.ToString().Should().MatchPattern(query, "RTRIM(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => e.StringValue.ToUpper());
            r.Expression.ToString().Should().MatchPattern(query, "UPPER(@1.stringvalue)");

            r = ec.Visit<Entity, string>(e => e.StringValue.ToLower());
            r.Expression.ToString().Should().MatchPattern(query, "LOWER(@1.stringvalue)");

            r = ec.Visit<Entity, bool>(e => e.StringValue.StartsWith("abc"));
            r.Expression.ToString().Should().MatchPattern(query, "@1.stringvalue LIKE @p || '%'");
            r.Params[0].Value.Should().Be("abc");
        }

        [Fact]
        public void Field_SecondOccurrenceOfEntity_2()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity>();
            query.AddEntity<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit<Entity[], int>(e => e[1].IntValue);

            r.Expression.ToString().Should().Be(query.GetReference(typeof(Entity), 1, nameof(Entity.IntValue)).Alias);
        }

        [Fact]
        public void Field_PrimaryKey()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity>();
            query.AddEntity<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit<Entity, Entity>(e => e);

            r.Expression.ToString().Should().MatchPattern(query, "@2.id");
        }

        [Fact]
        public void Field_ByReference()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity>();
            query.AddEntity<Entity>();

            var reference = query.GetReference(typeof(Entity), 1, nameof(Entity.RealValue));

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit<Entity, bool>(e => SqlFunction.Value<double>(reference) > 5);

            r.Expression.ToString().Should().MatchPattern(query, "(@3.realvalue>@p)");
        }

        [Fact]
        public void Variable()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity>();
            query.AddEntity<Entity>();

            var x = 4;

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit<Entity[], int>(e => x);

            r.Params.Should().HaveCount(1);

            r.Params.Should().HaveCount(1);
            r.Params[0].Value.Should().BeAssignableTo<Expression>();

            x++;
            query.BindExpressionParameters(r);
            query.GetParamValue<int>(r.Params[0].Name).Should().Be(5);

            r.Expression.ToString().Should().Be(r.Params[0].Name);
        }

        [Fact]
        public void LocalExpression()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity>();
            query.AddEntity<Entity>();

            var x = 3;

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit<Entity, int>(e => (int)(Math.Pow(x, 2) - 1));

            r.Params.Should().HaveCount(1);
            r.Params[0].Value.Should().BeAssignableTo<Expression>();

            x = 5;
            query.BindExpressionParameters(r);

            query.GetParamValue<int>(r.Params[0].Name).Should().Be(24);

            r.Expression.ToString().Should().Be(r.Params[0].Name);
        }

        [Theory]
        [InlineData(nameof(Expression.Add), nameof(Entity.IntValue), 5, "(@a.intvalue + @p)")]
        [InlineData(nameof(Expression.Subtract), nameof(Entity.IntValue), 5, "(@a.intvalue - @p)")]
        [InlineData(nameof(Expression.Multiply), nameof(Entity.IntValue), 5, "(@a.intvalue * @p)")]
        [InlineData(nameof(Expression.Divide), nameof(Entity.IntValue), 5, "(@a.intvalue / @p)")]
        [InlineData(nameof(Expression.GreaterThan), nameof(Entity.IntValue), 5, "(@a.intvalue > @p)")]
        [InlineData(nameof(Expression.GreaterThanOrEqual), nameof(Entity.IntValue), 5, "(@a.intvalue >= @p)")]
        [InlineData(nameof(Expression.LessThan), nameof(Entity.IntValue), 5, "(@a.intvalue < @p)")]
        [InlineData(nameof(Expression.LessThanOrEqual), nameof(Entity.IntValue), 5, "(@a.intvalue <= @p)")]
        [InlineData(nameof(Expression.NotEqual), nameof(Entity.IntValue), 5, "(@a.intvalue <> @p)")]
        [InlineData(nameof(Expression.Equal), nameof(Entity.IntValue), 5, "(@a.intvalue = @p)")]
        [InlineData(nameof(Expression.AndAlso), nameof(Entity.BooleanValue), true, "(@a.booleanvalue˽AND˽@p)")]
        [InlineData(nameof(Expression.OrElse), nameof(Entity.BooleanValue), true, "(@a.booleanvalue˽OR˽@p)")]

        public void BinaryOperator(string operatorName, string propertyName, object constant, string result)
        {
            var parameter = Expression.Parameter(typeof(Entity));
            var m = typeof(Expression).GetMethod(operatorName, new Type[] { typeof(Expression), typeof(Expression) });

            var ex = m.Invoke(null, new Expression[] {
                Expression.PropertyOrField(parameter, propertyName),
                Expression.Constant(constant)
            }) as Expression;

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Expression.ToString().Should().MatchPattern(result);
        }

        [Fact]
        public void BinaryOperator_AddString()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit<Entity, string>(e => e.StringValue + e.ID.ToString());

            r.Expression.ToString().Should().MatchPattern("@a.stringvalue || TOSTRING(@a.id)");
        }

        [Theory]
        [InlineData(nameof(DateTime.Year), nameof(Entity.DateTimeValue), typeof(DateTime), "YEAR(@a.datetimevalue)")]
        [InlineData(nameof(DateTime.Month), nameof(Entity.DateTimeValue), typeof(DateTime), "MONTH(@a.datetimevalue)")]
        [InlineData(nameof(DateTime.Day), nameof(Entity.DateTimeValue), typeof(DateTime), "DAY(@a.datetimevalue)")]
        [InlineData(nameof(DateTime.Hour), nameof(Entity.DateTimeValue), typeof(DateTime), "HOUR(@a.datetimevalue)")]
        [InlineData(nameof(DateTime.Minute), nameof(Entity.DateTimeValue), typeof(DateTime), "MINUTE(@a.datetimevalue)")]
        [InlineData(nameof(DateTime.Second), nameof(Entity.DateTimeValue), typeof(DateTime), "SECOND(@a.datetimevalue)")]
        public void Property(string property, string field, Type propertyType, string result)
        {
            var parameter = Expression.Parameter(typeof(Entity));

            var ex = Expression.PropertyOrField(Expression.PropertyOrField(parameter, field), property);

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Expression.ToString().Should().MatchPattern(result);
        }

        [Theory]
        [InlineData(nameof(Expression.Negate), nameof(Entity.IntValue), "(-(@a.intvalue))")]
        [InlineData(nameof(Expression.UnaryPlus), nameof(Entity.IntValue), "(+(@a.intvalue))")]
        [InlineData(nameof(Expression.Not), nameof(Entity.BooleanValue), "(NOT˽(@a.booleanvalue))")]
        public void UnaryOperator(string operatorName, string propertyName, string result)
        {
            var parameter = Expression.Parameter(typeof(Entity));
            var m = typeof(Expression).GetMethod(operatorName, new Type[] { typeof(Expression) });

            var ex = m.Invoke(null, new Expression[] {
                Expression.PropertyOrField(parameter, propertyName),
            }) as Expression;

            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();

            var ec = new ExpressionCompiler(query);
            var r = ec.Visit(ex);

            r.Expression.ToString().Should().MatchPattern(result);
        }
    }

    public class LinqOnDB_CUD : IClassFixture<LinqOnDB_CUD.Fixture>
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.ConnectionNames(flags, mFlags);

        [Entity(Scope = "linq4", Table = "LinqDict")]
        public class Dict
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        public class Fixture : ConnectionFixtureBase
        {
            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);
                Create(connection);
            }

            private static void Drop(SqlDbConnection connection)
            {               
                using (var query = connection.GetDropEntityQuery<Dict>())
                    query.Execute();
            }

            private static void Create(SqlDbConnection connection)
            {
                using (var query = connection.GetCreateEntityQuery<Dict>())
                    query.Execute();
            }
        }

        private readonly Fixture mFixture;

        public LinqOnDB_CUD(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateEntity_Insert(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dict = connection.GetCollectionOf<Dict>();

            var d = new Dict() { Name = "d1" };
            dict.Insert(d);

            using var atEnd = new DelayedAction(() =>
            {
                using (var query = connection.GetDeleteEntityQuery<Dict>())
                    query.Execute(d);
            });

            d.ID.Should().BeGreaterThan(0);
            dict.Where(o => o.ID == d.ID).Count().Should().Be(1);
            var d1 = dict.Where(o => o.ID == d.ID).First();
            d1.Should().NotBeNull();
            d1.ID.Should().Be(d.ID);
            d1.Name.Should().Be(d.Name);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateEntity_Save(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dict = connection.GetCollectionOf<Dict>();

            var d = new Dict() { Name = "d1" };
            dict.Insert(d);
            var id = d.ID;
            d.Name = "d2";
            dict.Update(d);
            d.ID.Should().Be(id);

            using var atEnd = new DelayedAction(() =>
            {
                using (var query = connection.GetDeleteEntityQuery<Dict>())
                    query.Execute(d);
            });

            var d1 = dict.Where(o => o.ID == d.ID).First();
            d1.Should().NotBeNull();
            d1.ID.Should().Be(d.ID);
            d1.Name.Should().Be(d.Name);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateEntity_Delete(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dict = connection.GetCollectionOf<Dict>();

            var d = new Dict() { Name = "d1" };
            dict.Insert(d);

            using var atEnd = new DelayedAction(() =>
            {
                using (var query = connection.GetDeleteEntityQuery<Dict>())
                    query.Execute(d);
            });

            dict.Where(o => o.ID == d.ID).Count().Should().Be(1);
            dict.Delete(d);
            dict.Where(o => o.ID == d.ID).Count().Should().Be(0);
            dict.Where(o => o.ID == d.ID).FirstOrDefault().Should().BeNull();
        }
    }

    [Collection(nameof(NorthwindFixture))]
    public class LinqOnDB_Select
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.ConnectionNames(flags, mFlags);

        private readonly NorthwindFixture mFixture;

        public LinqOnDB_Select(NorthwindFixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Count_All(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var products = connection.GetCollectionOf<Product>();

            products.Count().Should().Be(mFixture.Snapshot.Products.Count);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Count_Where(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var products = connection.GetCollectionOf<Product>();

            products.Count(p => p.QuantityPerUnit.Length > 10)
                .Should().Be(mFixture.Snapshot.Products.Count(p => p.QuantityPerUnit.Length > 10));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Max_All(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            orders.Select(o => SqlFunction.Max(o.Quantity)).First()
                .Should().Be(mFixture.Snapshot.OrderDetails.Select(o => o.Quantity).Max());
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Avg_All(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            orders.Select(o => SqlFunction.Avg(o.Quantity)).First()
                .Should().BeApproximately(mFixture.Snapshot.OrderDetails.Select(o => o.Quantity).Average(), 1e-5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Min_InGroup(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            var totals =
                orders.GroupBy(o => o.Order.OrderID).Select(g => new { Id = g.Key, Quantity = g.Min(v => v.Quantity) }).ToList();

            totals.Count.Should().Be(mFixture.Snapshot.Orders.Count);

            foreach (var total in totals)
            {
                int id = (int)total.Id;
                var q = (double)total.Quantity;

                q.Should().Be(mFixture.Snapshot.OrderDetails.Where(o => o.Order.OrderID == id).Min(o => o.Quantity));
            }
        }
    }
}
