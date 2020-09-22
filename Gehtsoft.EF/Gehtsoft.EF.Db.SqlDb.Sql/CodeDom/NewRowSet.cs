using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class NewRowSet : SqlBaseExpression
    {

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.NewRowSet;
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
