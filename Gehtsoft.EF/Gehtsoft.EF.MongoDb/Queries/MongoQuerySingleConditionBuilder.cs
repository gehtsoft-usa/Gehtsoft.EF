using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The builder for a single condition.
    /// 
    /// Use <see cref="MongoQueryCondition"/> to get an instance of this object.
    /// 
    /// Use <see cref="MongoQuerySingleConditionBuilderExtension"/> to create a simpler and easier to read definitions.
    ///
    /// Note that condition is highly limited comparing to SQL databases:
    /// * The first argument is always a property.
    /// * The second argument is always a value.
    /// * No functions could be applied on the property.
    /// </summary>
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

        /// <summary>
        /// Sets the comparison operation.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public MongoQuerySingleConditionBuilder Is(CmpOp op)
        {
            if (mPath == null)
                throw new InvalidOperationException("Property must be set first");
            mOp = op;
            if (op == CmpOp.IsNull || op == CmpOp.NotNull)
                Push();
            return this;
        }

        /// <summary>
        /// Sets the property name.
        /// </summary>
        /// <param name="path">See [link=mongopath]Paths[/link] article for details</param>
        /// <returns></returns>
        public MongoQuerySingleConditionBuilder Property(string path)
        {
            if (!string.IsNullOrEmpty(mPath) || mOp != null)
                throw new InvalidOperationException("The property can be only the first argument of a mongo DB filter expression");
            mPath = mQuery.TranslatePath(path);
            return this;
        }

        /// <summary>
        /// Sets the value to compare with.
        ///
        /// Note: Do not specify a value for `IsNull` and `NotNull` operations.
        ///
        /// Note: For `Like` operation the mask may be either a traditional SQL-style mask with `%` and `_` patterns or
        /// a regular expression, enclosed into `/` (slash) character. E.g. `"a%"` and `"/a.*/"` has the same meaning.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
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
