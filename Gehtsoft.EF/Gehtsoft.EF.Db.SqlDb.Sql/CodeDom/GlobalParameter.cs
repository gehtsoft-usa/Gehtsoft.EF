﻿using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class GlobalParameter : SqlBaseExpression
    {
        public string Name { get; }
        private Statement mParentStatement = null;
        private ResultTypes? mResultType = null;

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GlobalParameter;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                if (mResultType.HasValue) return mResultType.Value;
                if (InnerExpression == null)
                {
                    mResultType = mResultType.HasValue ? mResultType.Value : ResultTypes.Unknown;
                }
                else
                {
                    mResultType = InnerExpression.ResultType;
                }
                return mResultType.Value;
            }
        }
        public SqlConstant InnerExpression
        {
            get
            {
                if (mParentStatement == null) return null;
                return mParentStatement.CodeDomBuilder.FindGlobalParameter(Name);
            }
        }
        internal GlobalParameter(string name, ResultTypes? resultType = null)
        {
            Name = name;
            mResultType = resultType.HasValue ? resultType.Value : ResultTypes.Unknown;
        }
        internal GlobalParameter(Statement parentStatement, ASTNode node)
        {
            Name = node.Children[0].Value;
            mParentStatement = parentStatement;
            if (node.Children.Count > 1)
            {
                mResultType = Statement.GetResultTypeByName(node.Children[1].Value);
            }
            else
            {
                // try find declared
                ResultTypes rType = ResultType;
            }
        }

        public virtual bool Equals(GlobalParameter other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.Name == other.Name && this.ResultType == other.ResultType);
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is GlobalParameter item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}