using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class IfRunner : StatementRunner<IfStatement>
    {
        private SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection = null;
        private readonly ISqlDbConnectionFactory mConnectionFactory = null;

        protected override SqlDbConnection Connection
        {
            get
            {
                return mConnection;
            }
        }

        protected override SqlCodeDomBuilder CodeDomBuilder
        {
            get
            {
                return mBuilder;
            }
        }

        internal IfRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
        }

        internal IfRunner(SqlCodeDomBuilder builder, SqlDbConnection connection)
        {
            mBuilder = builder;
            mConnection = connection;
        }

        internal override object Run(IfStatement ifStatement)
        {
            object result = null;
            foreach (ConditionalStatementsRun item in ifStatement.ConditionalRuns)
            {
                SqlConstant resultConstant = CalculateExpression(item.ConditionalExpression);
                if (resultConstant == null || resultConstant.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Runtime error while IF execution"));
                }
                bool condition = (bool)resultConstant.Value;
                if(condition)
                {
                    result = mBuilder.Run(mConnection, item.Statements, true);
                    break;
                }
            }
            return result;
        }
    }
}
