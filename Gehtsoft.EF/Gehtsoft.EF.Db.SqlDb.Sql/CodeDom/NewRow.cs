using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  class NewRow : SqlBaseExpression
    {

        internal  override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.NewRow;
            }
        }
        internal  override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Row;
            }
        }
    }
}
