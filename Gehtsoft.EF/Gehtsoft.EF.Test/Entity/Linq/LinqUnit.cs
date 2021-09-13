using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Linq
{
    public static class StringAssertionsExtension
    {
        public static AndConstraint<StringAssertions> MatchPattern(this StringAssertions s, string pattern, string because = null, params object[] args)
        {
            var re = new Regex(ProcessRegex(pattern));
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => s.Subject)
                .ForCondition(m => re.IsMatch(m))
                .FailWith("Expected string {0} match pattern {1} but it does not", s.Subject, pattern);
            return new AndConstraint<StringAssertions>(s);
        }

        private static string ProcessRegex(string mask)
        {
            StringBuilder r = new StringBuilder();
            r.Append('^');
            for (int i = 0; i < mask.Length; i++)
            {
                var c = mask[i];
                switch (c)
                {
                    case '@':
                        i++;
                        c = mask[i];
                        switch (c)
                        {
                            case '@':
                                r.Append('@');
                                break;
                            case 'a':
                                r.Append(@"entity(\d+)");
                                break;
                            case 'p':
                                r.Append(@"@leq(\d+)");
                                break;
                            default:
                                r.Append(c);
                                break;
                        }
                        break;
                    case ' ':
                        r.Append(@"\s*");
                        break;
                    case '˽':
                        r.Append(@"\s+");
                        break;
                    case '.':
                    case '(':
                    case ')':
                    case '>':
                    case '<':
                    case '/':
                    case '\\':
                    case '+':
                    case '*':
                    case '^':
                    case '$':
                        r.Append('\\').Append(c);
                        break;
                    default:
                        r.Append(c);
                        break;
                }
            }
            r.Append('$');
            return r.ToString();
        }
    }

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
        public void Field1_OneEntityQuery()
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
        public void Field2_DictionaryOfMainEntity()
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
        public void Field3_ConnectedEntity()
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
        public void Field4_SecondOccurrenceOfEntity()
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
        public void Field5_SecondOccurrenceOfEntity()
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
}
