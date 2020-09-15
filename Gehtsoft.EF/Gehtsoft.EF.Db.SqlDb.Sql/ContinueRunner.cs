using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.Statement;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class ContinueRunner : StatementRunner<ContinueStatement>
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

        internal ContinueRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory, StatementSetEnvironment statements)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
            mStatements = statements;
        }

        internal ContinueRunner(SqlCodeDomBuilder builder, SqlDbConnection connection, StatementSetEnvironment statements)
        {
            mBuilder = builder;
            mConnection = connection;
            mStatements = statements;
        }

        public override object Run(ContinueStatement continueStatement)
        {
            object result = null;
            IStatementSetEnvironment current = mStatements;
            bool foundLoop = false;
            while (current != null)
            {
                current.Leave = true;
                if (current.ParentStatement != null && current.ParentStatement.Type == StatementType.Loop)
                {
                    foundLoop = true;
                    current.Continue = true;
                    break;
                }
                current = current.ParentEnvironment;
            }
            if (!foundLoop)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Runtime error: not found LOOP"));
            }
            return result;
        }
    }
}
