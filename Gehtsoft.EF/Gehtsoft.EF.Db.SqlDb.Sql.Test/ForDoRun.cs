using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Text;
using Xunit;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public class ForDoRun
    {
        private SqlCodeDomBuilder DomBuilder { get; }

        public ForDoRun()
        {
            DomBuilder = new SqlCodeDomBuilder();
        }


        [Fact]
        public void ForDo()
        {
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment();

            var func = environment.Parse("test",
                "SET factorial = 1 " +
                "FOR ?n := 0 WHILE ?n <= 5 NEXT ?n := ?n+1 LOOP " +
                "   IF ?n = 0 THEN CONTINUE; END IF " +
                "   ?factorial := ?factorial * ?n " +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = func(null);
            ((int)result).Should().Be(120);
        }
    }
}
