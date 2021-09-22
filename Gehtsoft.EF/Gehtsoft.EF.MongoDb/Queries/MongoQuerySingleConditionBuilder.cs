using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoQuerySingleConditionBuilder
    {
        private readonly BsonFilterExpressionBuilder mFilterBuilder;
        private readonly IMongoPathResolver mQuery;
        private string mPath;
        private object mValue = null;
        private CmpOp? mOp;
        private readonly LogOp mLogOp;

        public bool IsEmpty => mFilterBuilder.IsEmpty;

        internal MongoQuerySingleConditionBuilder(IMongoPathResolver query, BsonFilterExpressionBuilder filterBuilder, LogOp logOp = LogOp.And)
        {
            mQuery = query;
            mFilterBuilder = filterBuilder;
            mLogOp = logOp;
        }

        public MongoQuerySingleConditionBuilder Is(CmpOp op)
        {
            if (mPath == null)
                throw new InvalidOperationException("Property must be set first");
            mOp = op;
            if (op == CmpOp.IsNull || op == CmpOp.NotNull)
                Push();
            return this;
        }

        public MongoQuerySingleConditionBuilder Property(string path)
        {
            if (!string.IsNullOrEmpty(mPath) || mOp != null)
                throw new InvalidOperationException("The property can be only the first argument of a mongo DB filter expression");
            mPath = mQuery.TranslatePath(path);
            return this;
        }

        public MongoQuerySingleConditionBuilder Value(object value)
        {
            if (mPath == null || mOp == null)
                throw new InvalidOperationException("Property and operation must be set first");
            if (mOp == CmpOp.IsNull || mOp == CmpOp.NotNull)
                throw new InvalidOperationException("Operation does not require a value to compare with");
            mValue = value;
            Push();
            return this;
        }

        protected void Push()
        {
            mFilterBuilder.Add(mLogOp, mPath, (CmpOp)mOp, mValue);
        }
    }
}
