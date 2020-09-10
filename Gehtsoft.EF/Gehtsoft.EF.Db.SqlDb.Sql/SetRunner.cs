using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class SetRunner : StatementRunner<SetStatement>
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

        internal SetRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
        }

        internal SetRunner(SqlCodeDomBuilder builder, SqlDbConnection connection)
        {
            mBuilder = builder;
            mConnection = connection;
        }

        public override object Run(SetStatement setStatement)
        {
            List<object> result = new List<object>();

            foreach(SetItem item in setStatement.SetItems)
            {
                string name = item.Name;
                SqlBaseExpression sourceExpression = item.Expression;
                SqlConstant resultConstant = CalculateExpression(sourceExpression);
                if (resultConstant == null)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Runtime error while SET execution"));
                }
                mBuilder.UpdateGlobalParameter($"?{name}", resultConstant);
            }

            return result;
        }
    }
}
