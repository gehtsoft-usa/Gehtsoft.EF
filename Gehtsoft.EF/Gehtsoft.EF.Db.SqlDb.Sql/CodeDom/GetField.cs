﻿using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class GetField : SqlBaseExpression
    {
        private readonly ResultTypes mResultType = ResultTypes.Unknown;
        internal SqlBaseExpression RowParameter { get; }
        internal SqlBaseExpression NameParameter { get; }

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetField;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal GetField(Statement parentStatement, ASTNode fieldNode, string source)
        {
            ASTNode expressionNode = fieldNode.Children[0];
            RowParameter = SqlExpressionParser.ParseExpression(parentStatement, expressionNode, source);
            if (RowParameter.ResultType != ResultTypes.Row)
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "No ROW parameter in GET_FIELD function call"));
            }
            if (!Statement.IsCalculable(RowParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "Not calculable parameter in GET_FIELD function call"));
            }

            expressionNode = fieldNode.Children[1];
            NameParameter = SqlExpressionParser.ParseExpression(parentStatement, expressionNode, source);
            if (NameParameter.ResultType != ResultTypes.String)
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "No valid Name parameter in GET_FIELD function call"));
            }
            if (!Statement.IsCalculable(NameParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "Not calculable index parameter in GET_ROW function call"));
            }

            mResultType = Statement.GetResultTypeByName(fieldNode.Children[2].Value);
        }

        internal GetField(SqlBaseExpression rowParameter, SqlBaseExpression nameParameter, ResultTypes resultType)
        {
            mResultType = resultType;
            RowParameter = rowParameter;
            if (RowParameter.ResultType != ResultTypes.Row)
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    "No ROW parameter in GET_FIELD function call"));
            }
            if (!Statement.IsCalculable(RowParameter))
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    "Not calculable parameter in GET_FIELD function call"));
            }
            NameParameter = nameParameter;
            if (NameParameter.ResultType != ResultTypes.String)
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    "No valid Name parameter in GET_FIELD function call"));
            }
            if (!Statement.IsCalculable(NameParameter))
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    "Not calculable Name parameter in GET_FIELD function call"));
            }
        }
    }
}
