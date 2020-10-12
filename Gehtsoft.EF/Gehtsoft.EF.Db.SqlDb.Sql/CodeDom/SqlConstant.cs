using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlConstant : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        private object mValue = null;

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Constant;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal object Value
        {
            get
            {
                return mValue;
            }
        }

        internal SqlConstant(object value, ResultTypes type)
        {
            mValue = value;
            mResultType = type;
        }

        internal virtual bool Equals(SqlConstant other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            bool sameTypes = ResultType.Equals(other.ResultType);
            if (!sameTypes)
                return false;

            return Value.Equals(other.Value);
        }

        internal override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlConstant item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
