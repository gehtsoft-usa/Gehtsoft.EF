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
    public class SwitchParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public SwitchParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void SwitchParseSuccess()
        {
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();
            StatementSetEnvironment result = environment.Parse("test",
                "SET q=3, m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH"
            );

            SetStatement set = new SetStatement(environment, new SetItemCollection()
            {
                new SetItem("q", new SqlConstant(3, SqlBaseExpression.ResultTypes.Integer)),
                new SetItem("m", new SqlConstant(0, SqlBaseExpression.ResultTypes.Integer))
            });

            StatementSetEnvironment target = new StatementSetEnvironment() { set };
            environment.TopEnvironment = target;

            SwitchStatement swtch = new SwitchStatement(environment, new ConditionalStatementsRunCollection()
            {
                new ConditionalStatementsRun(new SqlBinaryExpression(new GlobalParameter("?q", SqlBaseExpression.ResultTypes.Integer),
                                SqlBinaryExpression.OperationType.Eq,
                                new SqlConstant(2, SqlBaseExpression.ResultTypes.Integer)),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(environment, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(2, SqlBaseExpression.ResultTypes.Integer))
                                                }),
                                    new BreakStatement()
                                }),
                new ConditionalStatementsRun(new SqlBinaryExpression(new GlobalParameter("?q", SqlBaseExpression.ResultTypes.Integer),
                                SqlBinaryExpression.OperationType.Eq,
                                new SqlConstant(3, SqlBaseExpression.ResultTypes.Integer)),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(environment, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(3, SqlBaseExpression.ResultTypes.Integer))
                                                }),
                                    new BreakStatement()
                                }),
                new ConditionalStatementsRun(new SqlBinaryExpression(new GlobalParameter("?q", SqlBaseExpression.ResultTypes.Integer),
                                SqlBinaryExpression.OperationType.Eq,
                                new SqlConstant(4, SqlBaseExpression.ResultTypes.Integer)),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(environment, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(4, SqlBaseExpression.ResultTypes.Integer))
                                                }),
                                    new BreakStatement()
                                }),
                new ConditionalStatementsRun(new SqlConstant(true, SqlBaseExpression.ResultTypes.Boolean),
                                new StatementSetEnvironment()
                                {
                                    new SetStatement(environment, new SetItemCollection()
                                                {
                                                    new SetItem("m", new SqlConstant(1, SqlBaseExpression.ResultTypes.Integer))
                                                })
                                }),
            });
            target.Add(swtch);

            result.Equals(target).Should().BeTrue();

        }


        [Fact]
        public void SwitchParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET q=3, m=0" +
                "SWITCH ?q " +
                "   CASE '2' :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET q='3', m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "SET m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH"
                )
            );
        }
    }
}
