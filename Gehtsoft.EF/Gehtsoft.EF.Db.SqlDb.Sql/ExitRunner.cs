using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class ExitRunner : StatementRunner<ExitStatement>
    {
        private SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection = null;
        private readonly ISqlDbConnectionFactory mConnectionFactory = null;
        private StatementSetEnvironment mStatements;

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

        internal ExitRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory, StatementSetEnvironment statements)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
            mStatements = statements;
        }

        internal ExitRunner(SqlCodeDomBuilder builder, SqlDbConnection connection, StatementSetEnvironment statements)
        {
            mBuilder = builder;
            mConnection = connection;
            mStatements = statements;
        }

        internal override object Run(ExitStatement setStatement)
        {
            object exitValue = null;
            SqlBaseExpression sourceExpression = setStatement.ExitExpression;
            if (sourceExpression != null)
            {
                SqlConstant resultConstant = CalculateExpression(sourceExpression);
                if (resultConstant == null)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Runtime error while EXIT execution (maybe unfound global variable)"));
                }
                exitValue = resultConstant.Value;
            }
            IStatementSetEnvironment current = mStatements;
            while (current != null)
            {
                if (exitValue != null)
                {
                    current.LastStatementResult = exitValue;
                }
                else if(current.LastStatementResult != null)
                {
                    exitValue = current.LastStatementResult;
                }
                current.Leave = true;
                current = current.ParentEnvironment;
            }
            return null;
        }
    }
}
