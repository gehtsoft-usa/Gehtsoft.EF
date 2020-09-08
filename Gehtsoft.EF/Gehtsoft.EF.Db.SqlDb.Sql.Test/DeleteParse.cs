using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Xunit;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlStatement;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public class DeleteParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public DeleteParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void DeleteParse1()
        {
            SqlStatementCollection result = DomBuilder.Parse("test",
                "DELETE FROM OrderDetail " +
                "WHERE Order IN " +
                "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
            );

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables),
                new SqlWhereClause()
            );

            fromTables.Add(new SqlPrimaryTable(select, "Order"));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderID")));
            select.WhereClause.RootExpression =
                new SqlBinaryExpression(
                    new SqlField(select, "ShipCountry"),
                    SqlBinaryExpression.OperationType.Eq,
                    new SqlConstant("UK", SqlBaseExpression.ResultTypes.String)
            );

            SqlDeleteStatement update = new SqlDeleteStatement(DomBuilder, "OrderDetail", new SqlWhereClause());
            update.WhereClause.RootExpression =
                new SqlInExpression(
                    new SqlField(update, "Order"), SqlInExpression.OperationType.In, select
            );

            SqlStatementCollection target = new SqlStatementCollection() { update };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void DeleteParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "DELETE FROM OrderD " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "DELETE FROM OrderDetail " +
                    "WHERE Order IN " +
                    "(SELECT ShipCountry FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "DELETE FROM OrderDetail " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Orderi WHERE ShipC = 'UK')"
                )
            );
        }


        [Fact]
        public void DeleteParse2()
        {
            SqlStatementCollection result = DomBuilder.Parse("test",
                "DELETE FROM Order"
            );

            SqlDeleteStatement update = new SqlDeleteStatement(DomBuilder, "Order");

            SqlStatementCollection target = new SqlStatementCollection() { update };

            result.Equals(target).Should().BeTrue();
        }
    }
}
