using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    public interface IMongoConditionalQueryWhereTarget
    {
        void EndWhereGroup(MongoConditionalQueryWhereGroup group);
    }

    public sealed class MongoConditionalQueryWhereGroup : IDisposable
    {
        private IMongoConditionalQueryWhereTarget mQuery;
        internal MongoConditionalQueryWhereGroup(IMongoConditionalQueryWhereTarget query)
        {
            mQuery = query;
        }

        public void Dispose()
        {
            mQuery?.EndWhereGroup(this);
            mQuery = null;
        }
    }

    public class MongoQuerySingleConditionBuilder
    {
        private readonly BsonFilterExpressionBuilder mFilterBuilder;
        private readonly MongoQuery mQuery;
        private string mPath;
        private object mValue = null;
        private CmpOp? mOp;
        private readonly LogOp mLogOp;

        public bool IsEmpty => mFilterBuilder.IsEmpty;

        internal MongoQuerySingleConditionBuilder(MongoQuery query, BsonFilterExpressionBuilder filterBuilder, LogOp logOp = LogOp.And)
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

    public static class MongoQuerySingleConditionBuilderExtension
    {
        public static MongoQuerySingleConditionBuilder Eq(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Eq);
        public static MongoQuerySingleConditionBuilder Neq(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Neq);
        public static MongoQuerySingleConditionBuilder Ls(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Ls);
        public static MongoQuerySingleConditionBuilder Le(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Le);
        public static MongoQuerySingleConditionBuilder Gt(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Gt);
        public static MongoQuerySingleConditionBuilder Ge(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Ge);

        public static MongoQuerySingleConditionBuilder Eq(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Eq).Value(value);
        public static MongoQuerySingleConditionBuilder Neq(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Neq).Value(value);
        public static MongoQuerySingleConditionBuilder Ls(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Ls).Value(value);
        public static MongoQuerySingleConditionBuilder Le(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Le).Value(value);
        public static MongoQuerySingleConditionBuilder Gt(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Gt).Value(value);
        public static MongoQuerySingleConditionBuilder Ge(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Ge).Value(value);

        public static MongoQuerySingleConditionBuilder Like(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Like);
        public static MongoQuerySingleConditionBuilder Like(this MongoQuerySingleConditionBuilder builder, string mask) => builder.Is(CmpOp.Like).Value(mask);
        public static MongoQuerySingleConditionBuilder In(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.In);
        public static MongoQuerySingleConditionBuilder NotIn(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.NotIn);
        public static MongoQuerySingleConditionBuilder In(this MongoQuerySingleConditionBuilder builder, params object[] values) => builder.Is(CmpOp.In).Value(values);
        public static MongoQuerySingleConditionBuilder NotIn(this MongoQuerySingleConditionBuilder builder, params object[] values) => builder.Is(CmpOp.NotIn).Value(values);

        public static MongoQuerySingleConditionBuilder IsNull(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.IsNull);
        public static MongoQuerySingleConditionBuilder NotNull(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.NotNull);
    }

    public class MongoQueryCondition : IMongoConditionalQueryWhereTarget
    {
        private readonly BsonFilterExpressionBuilder mFilterBuilder;
        private readonly MongoQuery mQuery;

        internal MongoQueryCondition(MongoQuery query, BsonFilterExpressionBuilder filterBuilder)
        {
            mQuery = query;
            mFilterBuilder = filterBuilder;
        }

        public MongoQuerySingleConditionBuilder Add(LogOp logOp) => new MongoQuerySingleConditionBuilder(mQuery, mFilterBuilder, logOp);

        public IDisposable AddGroup(LogOp logOp)
        {
            mFilterBuilder.BeginGroup(logOp);
            return new MongoConditionalQueryWhereGroup(this);
        }

        void IMongoConditionalQueryWhereTarget.EndWhereGroup(MongoConditionalQueryWhereGroup group)
        {
            mFilterBuilder.EndGroup();
        }
    }

    public static class MongoQueryConditionExtension
    {
        public static MongoQuerySingleConditionBuilder And(this MongoQueryCondition where) => where.Add(LogOp.And);
        public static MongoQuerySingleConditionBuilder Or(this MongoQueryCondition where) => where.Add(LogOp.Or);
        public static MongoQuerySingleConditionBuilder Property(this MongoQueryCondition where, string property) => where.Add(LogOp.And).Property(property);
    }

    public abstract class MongoQueryWithCondition : MongoQuery
    {
        protected BsonFilterExpressionBuilder FilterBuilder { get; private set; }

        public MongoQueryCondition Where { get; }

        protected MongoQueryWithCondition(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
            FilterBuilder = new BsonFilterExpressionBuilder();
            Where = new MongoQueryCondition(this, FilterBuilder);
        }

        protected void ResetFilter()
        {
            FilterBuilder = new BsonFilterExpressionBuilder();
        }

        [Obsolete("Use Where property instead")]
        public void AddWhereFilter(LogOp logOp, string path, CmpOp cmpOp, object value = null) => FilterBuilder.Add(logOp, TranslatePath(path), cmpOp, value);

        [Obsolete("Use Where property instead")]
        public void AddWhereFilter(string path, CmpOp cmpOp, object value = null) => FilterBuilder.Add(TranslatePath(path), cmpOp, value);

        [Obsolete("Use Where property instead")]
        public IDisposable AddWhereGroup(LogOp logOp)
        {
            FilterBuilder.BeginGroup(logOp);
            return new MongoConditionalQueryWhereGroup(Where);
        }

        [Obsolete("Use Where property instead")]
        public IDisposable AddWhereGroup()
        {
            FilterBuilder.BeginGroup();
            return new MongoConditionalQueryWhereGroup(Where);
        }
    }
}
