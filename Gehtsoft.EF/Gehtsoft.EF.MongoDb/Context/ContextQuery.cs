using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        [ExcludeFromCodeCoverage]
        ~ContextQuery()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [ExcludeFromCodeCoverage]
        protected virtual void Dispose(bool disposing)
        {
            Query?.Dispose();
            Query = null;
        }

        public int Execute()
        {
            Query.Execute();
            return 0;
        }

        public async Task<int> ExecuteAsync(CancellationToken? token = null)
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

        public void Execute(object entity)
        {
            Query.Execute(entity);
        }

        public async Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            if (token == null)
                await Query.ExecuteAsync(entity);
            else
                await Query.ExecuteAsync(entity, token.Value);
        }
    }

    internal class ContextCondition : IContextFilterCondition
    {
        protected MongoQuerySingleConditionBuilder Builder { get; }

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
        protected MongoQueryCondition Builder { get; }

        public ContextFilter(MongoQueryCondition builder)
        {
            Builder = builder;
        }

        public IDisposable AddGroup(LogOp logOp = LogOp.And)
        {
            return Builder.AddGroup(logOp);
        }

        public IContextFilterCondition Add(LogOp op = LogOp.And)
        {
            return new ContextCondition(Builder.Add(op));
        }
    }

    internal class ContextQueryWithCondition : ContextQuery, IContextQueryWithCondition
    {
        public IContextFilter Where { get; }

        public ContextQueryWithCondition(MongoQueryWithCondition query) : base(query)
        {
            Where = new ContextFilter(query.Where);
        }
    }

    internal class ContextOrder : IContextOrder
    {
        protected MongoSelectQuery SelectQuery { get; }

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
        protected MongoSelectQuery SelectQuery { get; }

        public IContextOrder Order { get; }

        public IContextFilter Where { get; }

        protected Type EntityType { get; }

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
        protected MongoCountQuery SelectQuery { get; }

        public IContextFilter Where { get; }

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