using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlCallFuncExpression : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        private SqlBaseExpressionCollection mParameters = new SqlBaseExpressionCollection();
        private string mName = string.Empty;

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Call;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        public string Name
        {
            get
            {
                return mName;
            }
        }

        public SqlBaseExpressionCollection Parameters
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

        public virtual bool Equals(SqlCallFuncExpression other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.Name.Equals(other.Name) && this.Parameters.Equals(other.Parameters));
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlCallFuncExpression item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
