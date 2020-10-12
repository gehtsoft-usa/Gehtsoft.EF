using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class SwitchRunner : StatementRunner<SwitchStatement>
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

        internal SwitchRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
        }

        internal SwitchRunner(SqlCodeDomBuilder builder, SqlDbConnection connection)
        {
            mBuilder = builder;
            mConnection = connection;
        }

        internal override object Run(SwitchStatement switchStatement)
        {
            object result = null;
            bool forceRunAll = false;
            foreach (ConditionalStatementsRun item in switchStatement.ConditionalRuns)
            {
                bool runCurrent = false;
                if (!forceRunAll)
                {
                    SqlConstant resultConstant = CalculateExpression(item.ConditionalExpression);
                    if (resultConstant == null || resultConstant.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                    {
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Runtime error while SWITCH execution"));
                    }
                    runCurrent = (bool)resultConstant.Value;
                }
                if (runCurrent || forceRunAll)
                {
                    result = mBuilder.Run(mConnection, item.Statements, true);
                    if (item.Statements.Leave)
                        break;
                    forceRunAll = true;
                }
            }
            return result;
        }
    }
}
