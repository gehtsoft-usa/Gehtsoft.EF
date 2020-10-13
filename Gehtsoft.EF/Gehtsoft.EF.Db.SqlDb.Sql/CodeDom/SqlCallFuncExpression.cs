using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlCallFuncExpression : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        private SqlBaseExpressionCollection mParameters = new SqlBaseExpressionCollection();
        private string mName = string.Empty;

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Call;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal string Name
        {
            get
            {
                return mName;
            }
        }

        internal SqlBaseExpressionCollection Parameters
        {
            get
            {
                return mParameters;
            }
        }

        internal SqlCallFuncExpression(ResultTypes resultType, string name, SqlBaseExpressionCollection parameters)
        {
            mResultType = resultType;
            mName = name;
            mParameters = parameters;
        }
    }
}
