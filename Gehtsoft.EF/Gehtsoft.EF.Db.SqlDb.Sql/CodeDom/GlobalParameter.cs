namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class GlobalParameter : SqlBaseExpression
    {
        internal string Name { get; }
        private readonly Statement mParentStatement = null;
        private ResultTypes? mResultType = null;
        private SqlConstant mInnerExpression = null;

        internal override ExpressionTypes ExpressionType
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
        internal override ResultTypes ResultType
        {
            get
            {
                if (mResultType.HasValue)
                    return mResultType.Value;
                if (InnerExpression != null)
                    return InnerExpression.ResultType;
                return ResultTypes.Unknown;
            }
        }
        internal SqlConstant InnerExpression
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
            mResultType = resultType ?? ResultTypes.Unknown;
        }
        internal GlobalParameter(Statement parentStatement, SqlParser.GlobalParameterContext node)
        {
            Name = node.GLOBAL_PARAMETER_NAME().GetText();
            mParentStatement = parentStatement;
            if (node.parameterType() != null)
                mResultType = Statement.GetResultTypeByName(node.parameterType().GetText());
        }
        internal GlobalParameter(Statement parentStatement, SqlParser.GlobalParameterSimpleContext node)
        {
            Name = node.GLOBAL_PARAMETER_NAME().GetText();
            mParentStatement = parentStatement;
        }
    }
}
