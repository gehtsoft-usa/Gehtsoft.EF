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
    public class UpdateParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public UpdateParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void UpdateParse1()
        {
            StatementCollection result = DomBuilder.Parse("test",
                "UPDATE OrderDetail " +
                "SET Discount= Discount * 1.1, UnitPrice = UnitPrice * 1.03 " +
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

            SqlUpdateAssignCollection updateAssigns = new SqlUpdateAssignCollection();

            SqlUpdateStatement update = new SqlUpdateStatement(DomBuilder, "OrderDetail", updateAssigns, new SqlWhereClause());
            update.WhereClause.RootExpression =
                new SqlInExpression(
                    new SqlField(update, "Order"), SqlInExpression.OperationType.In, select
            );

            updateAssigns.Add(new SqlUpdateAssign(
                new SqlField(update, "Discount"),
                    new SqlBinaryExpression(new SqlField(update, "Discount"), SqlBinaryExpression.OperationType.Mult,
                    new SqlConstant(1.1, SqlBaseExpression.ResultTypes.Double)
                    )
                )
            );
            updateAssigns.Add(new SqlUpdateAssign(
                new SqlField(update, "UnitPrice"),
                    new SqlBinaryExpression(new SqlField(update, "UnitPrice"), SqlBinaryExpression.OperationType.Mult,
                    new SqlConstant(1.03, SqlBaseExpression.ResultTypes.Double)
                    )
                )
            );

            StatementCollection target = new StatementCollection() { update };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void UpdateParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "UPDATE OrderD " +
                    "SET Discount= Discount * 1.1, UnitPrice = UnitPrice * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "UPDATE OrderDetail " +
                    "SET Disc = Discount * 1.1, UnitPrice = Unit * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "UPDATE OrderDetailing " +
                    "SET Discount= Discount * 1.1, UnitPrice = UnitPrice * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                    "UPDATE OrderDetail " +
                    "SET Discount= Discount * 1.1, UnitPrice = UnitPrice * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Orderi WHERE ShipC = 'UK')"
                )
            );
        }


        [Fact]
        public void UpdateParse2()
        {
            StatementCollection result = DomBuilder.Parse("test",
                "UPDATE Order " +
                "SET Freight =(SELECT MAX(Freight) FROM Order) " +
                "WHERE ShipCountry = 'UK'"
            );

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables)
            );

            fromTables.Add(new SqlPrimaryTable(select, "Order"));
            selectList.Add(new SqlExpressionAlias(select, new SqlAggrFunc("MAX", new SqlField(select, "Freight"))));

            SqlUpdateAssignCollection updateAssigns = new SqlUpdateAssignCollection();

            SqlUpdateStatement update = new SqlUpdateStatement(DomBuilder, "Order", updateAssigns, new SqlWhereClause());
            update.WhereClause.RootExpression =
                new SqlBinaryExpression(
                    new SqlField(update, "ShipCountry"),
                    SqlBinaryExpression.OperationType.Eq,
                    new SqlConstant("UK", SqlBaseExpression.ResultTypes.String)
            );

            updateAssigns.Add(new SqlUpdateAssign( new SqlField(update, "Freight"), select));

            StatementCollection target = new StatementCollection() { update };

            result.Equals(target).Should().BeTrue();
        }
    }
}
