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
    public class IfParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public IfParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void IfParseSuccess()
        {
            StatementSetEnvironment result = DomBuilder.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF"
            );

            SetStatement set = new SetStatement(DomBuilder, new SetItemCollection()
            {
                new SetItem("q", new SqlConstant(3, SqlBaseExpression.ResultTypes.Integer)),
                new SetItem("m", new SqlConstant(0, SqlBaseExpression.ResultTypes.Integer))
            });

            IfStatement ifElse = new IfStatement(DomBuilder, new IfItemCollection()
            {
                new IfItem(new SqlBinaryExpression(new GlobalParameter("?q", SqlBaseExpression.ResultTypes.Integer),
                                SqlBinaryExpression.OperationType.Eq,
                                new SqlConstant(2, SqlBaseExpression.ResultTypes.Integer)),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(DomBuilder, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(2, SqlBaseExpression.ResultTypes.Integer))
                                                })
                                }),
                new IfItem(new SqlBinaryExpression(new GlobalParameter("?q", SqlBaseExpression.ResultTypes.Integer),
                                SqlBinaryExpression.OperationType.Eq,
                                new SqlConstant(3, SqlBaseExpression.ResultTypes.Integer)),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(DomBuilder, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(3, SqlBaseExpression.ResultTypes.Integer))
                                                })
                                }),
                new IfItem(new SqlBinaryExpression(new GlobalParameter("?q", SqlBaseExpression.ResultTypes.Integer),
                                SqlBinaryExpression.OperationType.Eq,
                                new SqlConstant(4, SqlBaseExpression.ResultTypes.Integer)),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(DomBuilder, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(4, SqlBaseExpression.ResultTypes.Integer))
                                                })
                                }),
                new IfItem(new SqlConstant(true, SqlBaseExpression.ResultTypes.Boolean),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(DomBuilder, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(1, SqlBaseExpression.ResultTypes.Integer))
                                                })
                                }),
            });

            StatementSetEnvironment target = new StatementSetEnvironment() { set, ifElse };

            result.Equals(target).Should().BeTrue();

        }


        [Fact]
        public void IfParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSE" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF"
                )
            );
        }
    }
}
