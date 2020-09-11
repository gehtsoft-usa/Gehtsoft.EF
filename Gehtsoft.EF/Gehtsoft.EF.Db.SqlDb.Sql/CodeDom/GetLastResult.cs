using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class GetLastResult : SqlBaseExpression
    {

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetLastResult;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.RowSet;
            }
        }
    }
}
