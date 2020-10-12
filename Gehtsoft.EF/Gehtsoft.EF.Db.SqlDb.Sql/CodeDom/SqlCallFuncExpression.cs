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

        internal virtual bool Equals(SqlCallFuncExpression other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.Name.Equals(other.Name) && this.Parameters.Equals(other.Parameters));
        }

        internal override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlCallFuncExpression item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
