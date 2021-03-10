﻿using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  class GlobalParameter : SqlBaseExpression
    {
        internal  string Name { get; }
        private Statement mParentStatement = null;
        private ResultTypes? mResultType = null;
        private SqlConstant mInnerExpression = null;

        internal  override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GlobalParameter;
            }
        }

        internal void ResetResultType()
        {
            mResultType = null;
        }
        internal  override ResultTypes ResultType
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
        internal  SqlConstant InnerExpression
        {
            get
            {
                if (mInnerExpression != null) return mInnerExpression;
                if (mParentStatement == null) return null;
                return mParentStatement.CodeDomBuilder.FindGlobalParameter(Name);
            }
        }

        internal void SetInnerExpression(SqlConstant innerExpression)
        {
            ResetResultType();
            mInnerExpression = innerExpression;
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
    }
}
