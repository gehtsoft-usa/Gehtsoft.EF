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
    public class WhileDoParse
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public WhileDoParse()
        {
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void WhileDoParseSuccess()
        {
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();
            StatementSetEnvironment result = environment.Parse("test",
                "SET factorial=1, n=1" +
                "WHILE ?n <= 5 LOOP" +
                "   SET factorial = ?factorial * ?n" +
                "   SET n = ?n + 1" +
                "END LOOP"
            );

            SetStatement set = new SetStatement(environment, new SetItemCollection()
            {
                new SetItem("factorial", new SqlConstant(1, SqlBaseExpression.ResultTypes.Integer)),
                new SetItem("n", new SqlConstant(1, SqlBaseExpression.ResultTypes.Integer))
            });

            StatementSetEnvironment target = new StatementSetEnvironment() { set };
            environment.TopEnvironment = target;

            WhileDoStatement whiledo = new WhileDoStatement(environment,
                new SqlBinaryExpression(new GlobalParameter("?n", SqlBaseExpression.ResultTypes.Integer),
                    SqlBinaryExpression.OperationType.Le,
                    new SqlConstant(5, SqlBaseExpression.ResultTypes.Integer)),
                new StatementSetEnvironment()
                    {
                        new SetStatement(environment, new SetItemCollection()
                        {
                            new SetItem("factorial",
                                new SqlBinaryExpression(new GlobalParameter("?factorial", SqlBaseExpression.ResultTypes.Integer),
                                    SqlBinaryExpression.OperationType.Mult,
                                    new GlobalParameter("?n", SqlBaseExpression.ResultTypes.Integer)
                                )
                            )
                        }),
                        new SetStatement(environment, new SetItemCollection()
                        {
                            new SetItem("n",
                                new SqlBinaryExpression(new GlobalParameter("?n", SqlBaseExpression.ResultTypes.Integer),
                                    SqlBinaryExpression.OperationType.Plus,
                                    new SqlConstant(1, SqlBaseExpression.ResultTypes.Integer)
                                )
                            )
                        })
                    }
                );

            target.Add(whiledo);

            result.Equals(target).Should().BeTrue();
        }


        [Fact]
        public void WhileDoParseError()
        {
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "CONTINUE"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                DomBuilder.Parse("test",
                "BREAK"
                )
            );
        }
    }
}
