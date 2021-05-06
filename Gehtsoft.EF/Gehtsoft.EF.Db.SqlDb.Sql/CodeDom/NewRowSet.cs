using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class NewRowSet : SqlBaseExpression
    {
        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.NewRowSet;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.RowSet;
            }
        }
    }
}
