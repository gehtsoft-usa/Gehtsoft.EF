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
        private readonly ResultTypes mResultType;

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

        internal string Name { get; }

        internal SqlBaseExpressionCollection Parameters { get; }

        internal SqlCallFuncExpression(ResultTypes resultType, string name, SqlBaseExpressionCollection parameters)
        {
            mResultType = resultType;
            Name = name;
            Parameters = parameters;
        }
    }
}
