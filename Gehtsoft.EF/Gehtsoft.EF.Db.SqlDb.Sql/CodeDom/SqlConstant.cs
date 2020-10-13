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
    }
}
