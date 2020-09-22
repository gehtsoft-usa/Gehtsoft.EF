using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class NewRow : SqlBaseExpression
    {

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.NewRow;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Row;
            }
        }
    }
}
