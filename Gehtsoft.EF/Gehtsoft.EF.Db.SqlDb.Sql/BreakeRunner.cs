using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.Statement;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class BreakRunner : StatementRunner<BreakStatement>
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

        internal BreakRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory, StatementSetEnvironment statements)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
            mStatements = statements;
        }

        internal BreakRunner(SqlCodeDomBuilder builder, SqlDbConnection connection, StatementSetEnvironment statements)
        {
            mBuilder = builder;
            mConnection = connection;
            mStatements = statements;
        }

        public override object Run(BreakStatement continueStatement)
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
