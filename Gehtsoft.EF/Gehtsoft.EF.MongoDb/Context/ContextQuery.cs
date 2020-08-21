using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb.Context
{
    internal class ContextQuery : IEntityQuery
    {
        protected MongoQuery Query { get; private set; }

        public ContextQuery(MongoQuery query)
        {
            Query = query;
        }

        ~ContextQuery()
        {
            Dispose();
        }

        public void Dispose()
        {
            Query?.Dispose();
            Query = null;
            GC.SuppressFinalize(this);
        }

        public int Execute()
        {
            Query.Execute();
            return 0;
        }

        public async Task<int> ExecuteAsync(CancellationToken? token)
        {
            await Query.ExecuteAsync(token);
            return 0;
        }
    }

    internal class ContextModifyQuery : ContextQuery, IModifyEntityQuery
    {
        public ContextModifyQuery(MongoQuery query) : base(query)
        {
        }

        public void Execute(object v)
        {
            Query.Execute(v);
        }

        public async Task ExecuteAsync(object v, CancellationToken? token)
        {
            if (token == null)
                await Query.ExecuteAsync(v);
            else
                await Query.ExecuteAsync(v, token.Value);
        }
    }

    internal class ContextCondition : IContextFilterCondition
    {
        protected MongoQuerySingleConditionBuilder Builder { get; private set; }

        public ContextCondition(MongoQuerySingleConditionBuilder builder)
        {
            Builder = builder;
        }

        public IContextFilterCondition Property(string name)
        {
            Builder.Property(name);
            return this;
        }

        public IContextFilterCondition Is(CmpOp op)
        {
            Builder.Is(op);
            return this;
        }

        public IContextFilterCondition Value(object value)
        {
            Builder.Value(value);
            return this;
        }
    }

    internal class ContextFilter : IContextFilter
    {
        protected MongoQueryCondition Builder { get; private set; }

        public ContextFilter(MongoQueryCondition builder)
        {
            Builder = builder;
        }

        public IDisposable AddGroup(LogOp logOp = LogOp.And)
        {
            return Builder.AddGroup(logOp);
        }

        public IContextFilterCondition Add(LogOp op)
        {
            return new ContextCondition(Builder.Add(op));
        }
    }

    internal class ContextQueryWithCondition : ContextQuery, IContextQueryWithCondition
    {
        public IContextFilter Where { get; private set; }

        public ContextQueryWithCondition(MongoQueryWithCondition query) : base(query)
        {
            Where = new ContextFilter(query.Where);
        }
    }

    internal class ContextOrder : IContextOrder
    {
        protected MongoSelectQuery SelectQuery { get; private set; }

        public ContextOrder(MongoSelectQuery query)
        {
            SelectQuery = query;
        }

        public IContextOrder Add(string name, SortDir sortDir = SortDir.Asc)
        {
            SelectQuery.AddOrderBy(name, sortDir);
            return this;
        }
    }

    internal class ContextSelect : ContextQuery, IContextSelect
    {
        protected MongoSelectQuery SelectQuery { get; private set; }

        public IContextOrder Order { get; private set; }

        public IContextFilter Where { get; private set; }

        protected Type EntityType { get; private set; }

        public int? Take
        {
            get => SelectQuery.Limit;
            set => SelectQuery.Limit = value ?? Int32.MaxValue;
        }

        public int? Skip
        {
            get => SelectQuery.Skip;
            set => SelectQuery.Skip = value ?? 0;
        }

        public ContextSelect(MongoSelectQuery query, Type type) : base(query)
        {
            SelectQuery = query;
            Order = new ContextOrder(query);
            Where = new ContextFilter(query.Where);
            EntityType = type;
        }

        public object ReadOne()
        {
            if (!SelectQuery.ReadNext())
                return null;
            return SelectQuery.GetEntity(EntityType);
        }

        public Task<object> ReadOneAsync(CancellationToken? token = null)
        {
            if (token != null && token.Value.IsCancellationRequested)
                throw new OperationCanceledException();
            return Task.FromResult(ReadOne());
        }
    }

    internal class ContextCount : ContextQuery, IContextCount
    {
        protected MongoCountQuery SelectQuery { get; private set; }

        public IContextFilter Where { get; private set; }

        public ContextCount(MongoCountQuery query) : base(query)
        {
            SelectQuery = query;
            Where = new ContextFilter(query.Where);
        }

        public int GetCount()
        {
            Execute();
            return (int)SelectQuery.RowCount;
        }

        public async Task<int> GetCountAsync(CancellationToken? token = null)
        {
            await ExecuteAsync(token);
            return (int)SelectQuery.RowCount;
        }
    }
}