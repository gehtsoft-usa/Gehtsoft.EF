using System;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlParser;
using Hime.SDK.Grammars;
using Microsoft.OData;
using Xunit.Sdk;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public static class AstNodeAssertionsExtensions
    {
        public static AndConstraint<AstNodeAssertions> HaveNoOffsetClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectOffsetClause().Should().BeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveOffsetClause(this AstNodeAssertions assertions, int? value = null)
        {
            assertions.Subject.SelectOffsetClause().Should().NotBeNull();
            if (value != null)
                assertions.Subject.SelectOffsetClause().LimitOffsetValue().Should().Be(value.Value);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveNoLimitClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectLimitClause().Should().BeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveLimitClause(this AstNodeAssertions assertions, int? value = null)
        {
            assertions.Subject.SelectLimitClause().Should().NotBeNull();
            if (value != null)
                assertions.Subject.SelectLimitClause().LimitOffsetValue().Should().Be(value.Value);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeOpExpression(this AstNodeAssertions assertions, string op = null)
        {
            assertions.Subject.ExprIsOp().Should().BeTrue();
            if (op != null)
                assertions.Subject.ExprOp().Should().Be(op);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeUnaryOp(this AstNodeAssertions assertions, string op = null)
        {
            assertions.Subject.ExprOpArgCount().Should().Be(1);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeBinaryOp(this AstNodeAssertions assertions, string op = null)
        {
            assertions.Subject.ExprOpArgCount().Should().Be(2);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeCallExpression(this AstNodeAssertions assertions, string function = null)
        {
            assertions.Subject.ExprIsFnCall().Should().BeTrue();
            if (function != null)
                assertions.Subject.ExprFnCallName().Should().Be(function);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeFieldExpression(this AstNodeAssertions assertions)
        {
            assertions.Subject.ExprIsField().Should().BeTrue();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeFieldExpression(this AstNodeAssertions assertions, string alias, string name)
        {
            return assertions.Subject.Should().BeFieldExpression()
                .And.HaveFieldAlias(alias)
                .And.HaveFieldName(name);
        }

        public static AndConstraint<AstNodeAssertions> HaveFieldAlias(this AstNodeAssertions assertions, string alias = null)
        {
            assertions.Subject.ExprFieldHasAlias().Should().BeTrue();
            if (alias != null)
                assertions.Subject.ExprFieldAlias().Should().Be(alias);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveNoFieldAlias(this AstNodeAssertions assertions)
        {
            assertions.Subject.ExprFieldHasAlias().Should().BeFalse();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveFieldName(this AstNodeAssertions assertions, string name)
        {
            assertions.Subject.ExprFieldName().Should().Be(name);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeParamExpression(this AstNodeAssertions assertions)
        {
            assertions.Subject.ExprIsParam().Should().BeTrue();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveParamName(this AstNodeAssertions assertions, string name)
        {
            assertions.Subject.ExprParamName().Should().Be(name);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeConstant(this AstNodeAssertions assertions)
        {
            assertions.Subject.ExprIsConst().Should().Be(true);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeCountAllCall(this AstNodeAssertions assertions)
        {
            assertions.Subject.ExprIsCountAll().Should().Be(true);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeConstant(this AstNodeAssertions assertions, object value)
        {
            assertions.Subject.ExprIsConst().Should().Be(true);
            assertions.Subject.ExprConstValue().Should().Be(value);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> ItsParameter(this AstNodeAssertions assertions, int parameter, Action<IAstNode> validator)
        {
            IAstNode node;

            if (assertions.Subject.ExprIsOp())
                node = assertions.Subject.ExprOpArg(parameter);
            else if (assertions.Subject.ExprIsFnCall())
                node = assertions.Subject.ExprFnCallArg(parameter);
            else
                throw new XunitException(string.Format("The node {0} is expected to be an op or an call", assertions.Subject));

            if (node == null)
                throw new XunitException(string.Format("The node {0} is expected to have a parameter {1} but it does not", assertions.Subject, parameter));

            validator(node);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveResultsetSize(this AstNodeAssertions assertions, int size)
        {
            assertions.Subject.Resultset().Count().Should().Be(size);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveResultsetItemExpression(this AstNodeAssertions assertions, int item, Action<IAstNode> validator)
        {
            IAstNode node = assertions.Subject.ResultsetItem(item).ResultsetExpr();
            if (node == null)
                throw new XunitException(string.Format("The node {0} has not result item {1}", assertions.Subject, item));

            validator(node);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveResultsetItemAlias(this AstNodeAssertions assertions, int item, string alias)
        {
            assertions.Subject.ResultsetItem(item).ResultsetItemAlias().Should().Be(alias);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveDistinctClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SetQuantifiers().Should().HaveElementMatching(e => e.Symbol == "DISTINCT");
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveNoDistinctClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SetQuantifiers().Should().HaveNoElementMatching(e => e.Symbol == "DISTINCT");
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveTableName(this AstNodeAssertions assertions, string name)
        {
            assertions.Subject.TableName().Should().HaveValue(name);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveTableAlias(this AstNodeAssertions assertions, string name)
        {
            assertions.Subject.TableAlias().Should().HaveValue(name);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeJoin(this AstNodeAssertions assertions)
        {
            assertions.Subject.TableIsJoin().Should().BeTrue();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> BeJoin(this AstNodeAssertions assertions, string joinType)
        {
            assertions.Subject.TableJoinType().Should().Be(joinType);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> NotBeJoin(this AstNodeAssertions assertions)
        {
            assertions.Subject.TableIsJoin().Should().BeFalse();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveWhereClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectWhere().Should().NotBeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveNoWhereClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectWhere().Should().BeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveHavingClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectHaving().Should().NotBeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveNoHavingClause(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectHaving().Should().BeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveSortOrder(this AstNodeAssertions assertions, int? count = null)
        {
            assertions.Subject.SelectSort().Should().NotBeNull();
            if (count != null)
                assertions.Subject.SelectSort().Select("/*").Should().HaveCount(count.Value);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveNoSortOrder(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectSort().Should().BeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveSortOrder(this AstNodeAssertions assertions, int index, string alias, string name, string direction)
        {
            assertions.Subject.SelectSort().Should().NotBeNull();
            var node = assertions.Subject.SelectSort().SortOrder(index);
            node.Should().NotBeNull();
            node.SortOrderExpr().Should().BeFieldExpression(alias, name);
            node.SortOrderDirection().Should().Be(direction);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveGroupBy(this AstNodeAssertions assertions, int? count = null)
        {
            assertions.Subject.SelectGroupBy().Should().NotBeNull();
            if (count != null)
                assertions.Subject.SelectGroupBy().Select("/*").Should().HaveCount(count.Value);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveNoGroupBy(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectGroupBy().Should().BeNull();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
        public static AndConstraint<AstNodeAssertions> HaveGroupBy(this AstNodeAssertions assertions, int index, string alias, string name)
        {
            assertions.Subject.SelectGroupBy().Should().NotBeNull();
            var node = assertions.Subject.SelectGroupBy().GroupOrder(index);
            node.Should().NotBeNull();
            node.SortOrderExpr().Should().BeFieldExpression(alias, name);
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
    }
}

