﻿using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class GetRow : SqlBaseExpression
    {
        public SqlBaseExpression RowSetParameter { get; }
        public SqlBaseExpression IndexParameter { get; }

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetRow;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Row;
            }
        }

        internal GetRow(Statement parentStatement, ASTNode fieldNode, string source)
        {
            ASTNode expressionNode;
            expressionNode = fieldNode.Children[0];
            RowSetParameter = SqlExpressionParser.ParseExpression(parentStatement, expressionNode, source);
            if (RowSetParameter.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"No ROWSET parameter in GET_ROW function call"));
            }
            if (!Statement.IsCalculable(RowSetParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Not calculable parameter in GET_ROW function call"));
            }

            expressionNode = fieldNode.Children[1];
            IndexParameter = SqlExpressionParser.ParseExpression(parentStatement, expressionNode, source);
            if (IndexParameter.ResultType != ResultTypes.Integer)
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"No index parameter in GET_ROW function call"));
            }
            if (!Statement.IsCalculable(IndexParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Not calculable index parameter in GET_ROW function call"));
            }
        }

        internal GetRow(SqlBaseExpression rowSetParameter, SqlBaseExpression indexParameter)
        {
            RowSetParameter = rowSetParameter;
            if (RowSetParameter.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"No ROWSET parameter in GET_ROW function call"));
            }
            if (!Statement.IsCalculable(RowSetParameter))
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"Not calculable parameter in GET_ROW function call"));
            }
            IndexParameter = indexParameter;
            if (IndexParameter.ResultType != ResultTypes.Integer)
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"No index parameter in GET_ROW function call"));
            }
            if (!Statement.IsCalculable(IndexParameter))
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"Not calculable index parameter in GET_ROW function call"));
            }
        }

        public virtual bool Equals(GetRow other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.RowSetParameter.Equals(other.RowSetParameter) && this.IndexParameter.Equals(other.IndexParameter));
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is GetRow item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}