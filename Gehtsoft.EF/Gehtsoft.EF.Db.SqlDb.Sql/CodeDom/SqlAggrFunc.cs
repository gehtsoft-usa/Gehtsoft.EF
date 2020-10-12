using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  class SqlAggrFunc : SqlBaseExpression
    {
        private ResultTypes mResultType = ResultTypes.Unknown;
        internal  string Name { get; } = null;
        internal  SqlField Field { get; } = null;

        internal  override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.AggrFuncCall;
            }
        }
        internal  override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal SqlAggrFunc(string name, SqlField field, ResultTypes? resultType = null)
        {
            Name = name;
            Field = field;
            if(resultType.HasValue)
            {
                mResultType = resultType.Value;
            } else if(Field != null)
            {
                mResultType = Field.ResultType;
            }
        }

        internal  virtual bool Equals(SqlAggrFunc other)
        {
            if (other == null)
                return false;
            if(Name != other.Name)
                return false;
            return Field == null ? (other.Field == null) : Field.Equals(other.Field);
        }

        internal override bool Equals(SqlBaseExpression obj)
        {
            if (obj is SqlAggrFunc item)
                return Equals(item);
            return base.Equals(obj);
        }

    }
}
