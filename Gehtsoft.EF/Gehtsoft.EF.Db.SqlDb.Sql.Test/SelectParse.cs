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
    public class SelectParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public SelectParse()
        {
            //ISqlDbConnectionFactory connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=d:\testsql.db"); ;
            //Snapshot snapshot = new Snapshot();
            //snapshot.CreateAsync(connectionFactory.GetConnection()).ConfigureAwait(true).GetAwaiter().GetResult();
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void SelectSimple()
        {
            SqlStatementCollection result = DomBuilder.Parse("test",
                "SELECT OrderID, OrderDate, ShipName FROM Order WHERE OrderID > 100 AND (TRIM(TRAILING ShipAddress) <> 'street' OR OrderDate > DATE '2011-01-01')");

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables),
                new SqlWhereClause()
                );

            fromTables.Add(new SqlPrimaryTable(select, "Order"));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderID")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderDate")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "ShipName")));
            select.WhereClause.RootExpression = new SqlBinaryExpression(
                new SqlBinaryExpression(
                    new SqlField(select, "OrderID"),
                    SqlBinaryExpression.OperationType.Gt,
                    new SqlConstant(100, SqlBaseExpression.ResultTypes.Integer)
                ),
                SqlBinaryExpression.OperationType.And,
                new SqlBinaryExpression(
                    new SqlBinaryExpression(
                        new SqlCallFuncExpression(SqlBaseExpression.ResultTypes.String, "RTRIM",
                            new SqlBaseExpressionCollection() { new SqlField(select, "ShipAddress") }),
                        SqlBinaryExpression.OperationType.Neq,
                        new SqlConstant("street", SqlBaseExpression.ResultTypes.String)
                    ),
                    SqlBinaryExpression.OperationType.Or,
                    new SqlBinaryExpression(
                        new SqlField(select, "OrderDate"),
                        SqlBinaryExpression.OperationType.Gt,
                        new SqlConstant(DateTime.ParseExact("2011-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture), SqlBaseExpression.ResultTypes.Date)
                    )
                )
            );

            SqlStatementCollection target = new SqlStatementCollection() { select };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void SelectAggrFunc()
        {
            SqlStatementCollection result = DomBuilder.Parse("test", "SELECT COUNT(*), MAX(Quantity) FROM OrderDetail");

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables)
            );

            fromTables.Add(new SqlPrimaryTable(select, "OrderDetail"));
            selectList.Add(new SqlExpressionAlias(select, new SqlAggrFunc("COUNT", null)));
            selectList.Add(new SqlExpressionAlias(select, new SqlAggrFunc("MAX", new SqlField(select, "Quantity"))));

            SqlStatementCollection target = new SqlStatementCollection() { select };

            result.Equals(target).Should().BeTrue();
        }

        [Fact]
        public void JoinedSelect()
        {
            SqlStatementCollection result = DomBuilder.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = ID " +
                "INNER JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "INNER JOIN Employee ON Order.Employee = Employee.EmployeeID"
                );

            SqlExpressionAliasCollection selectList = new SqlExpressionAliasCollection();
            SqlTableSpecificationCollection fromTables = new SqlTableSpecificationCollection();
            SqlSelectStatement select = new SqlSelectStatement(DomBuilder,
                new SqlSelectList(selectList),
                new SqlFromClause(fromTables)
            );

            fromTables.Add(
                new SqlQualifiedJoinedTable(
                    new SqlQualifiedJoinedTable(
                        new SqlQualifiedJoinedTable(
                            new SqlPrimaryTable(select, "OrderDetail"),
                            new SqlPrimaryTable(select, "Order"), "INNER",
                            new SqlBinaryExpression(
                                new SqlField(select, "Order", "OrderDetail"),
                                SqlBinaryExpression.OperationType.Eq,
                                new SqlField("ID", typeof(int))
                            )
                        ),
                        new SqlPrimaryTable(select, "Customer"), "INNER",
                        new SqlBinaryExpression(
                            new SqlField(select, "Customer", "Order"),
                            SqlBinaryExpression.OperationType.Eq,
                            new SqlField(select, "CustomerID", "Customer")
                        )
                    ),
                    new SqlPrimaryTable(select, "Employee"), "INNER",
                    new SqlBinaryExpression(
                        new SqlField(select, "Employee", "Order"),
                        SqlBinaryExpression.OperationType.Eq,
                        new SqlField(select, "EmployeeID", "Employee")
                    )
                )
            );

            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderID"), "ID"));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "Quantity")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "OrderDate", "Order")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "CompanyName", "Customer")));
            selectList.Add(new SqlExpressionAlias(select, new SqlField(select, "FirstName", "Employee")));

            SqlStatementCollection target = new SqlStatementCollection() { select };

            result.Equals(target).Should().BeTrue();

        }
    }
}
