using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlConstant : SqlBaseExpression
    {
        private readonly ResultTypes mResultType;

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

        internal object Value { get; }

        internal SqlConstant(object value, ResultTypes type)
        {
            Value = value;
            mResultType = type;
        }
    }
}
