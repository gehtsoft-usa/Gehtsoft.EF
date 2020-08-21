using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Context
{
    internal class ContextQuery : IEntityQuery
    {
        protected EntityQuery Query { get; private set; }

        public ContextQuery(EntityQuery query)
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
            return Query.Execute();
        }

        public Task<int> ExecuteAsync(CancellationToken? token)
        {
            return Query.ExecuteAsync(token);
        }
    }

    internal class ContextModifyQuery : ContextQuery, IModifyEntityQuery
    {
        protected ModifyEntityQuery ModifyQuery { get; private set; }

        public ContextModifyQuery(ModifyEntityQuery query) : base(query)
        {
            ModifyQuery = query;
        }

        public void Execute(object v)
        {
            ModifyQuery.Execute(v);
        }

        public Task ExecuteAsync(object v, CancellationToken? token)
        {
            return ModifyQuery.ExecuteAsync(v, token);
        }
    }

    internal class ContextCondition : IContextFilterCondition
    {
        protected SingleEntityQueryConditionBuilder Builder { get; private set; }

        public ContextCondition(SingleEntityQueryConditionBuilder builder)
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
        protected EntityQueryConditionBuilder Builder { get; private set; }

        public ContextFilter(EntityQueryConditionBuilder builder)
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

        public ContextQueryWithCondition(ConditionEntityQueryBase query) : base(query)
        {
            Where = new ContextFilter(query.Where);
        }
    }

    internal class ContextOrder : IContextOrder
    {
        protected SelectEntitiesQuery SelectQuery { get; private set; }

        public ContextOrder(SelectEntitiesQuery query)
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
        protected SelectEntitiesQuery SelectQuery { get; private set; }

        public IContextOrder Order { get; private set; }

        public IContextFilter Where { get; private set; }

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

        public ContextSelect(SelectEntitiesQuery query) : base(query)
        {
            SelectQuery = query;
            Order = new ContextOrder(query);
            Where = new ContextFilter(query.Where);
        }

        public object ReadOne()
        {
            return SelectQuery.ReadOne();
        }

        public Task<object> ReadOneAsync(CancellationToken? token = null)
        {
            return SelectQuery.ReadOneAsync(token);
        }
    }

    internal class ContextCount : ContextQuery, IContextCount
    {
        protected SelectEntitiesCountQuery SelectQuery { get; private set; }

        public IContextFilter Where { get; private set; }

        public ContextCount(SelectEntitiesCountQuery query) : base(query)
        {
            SelectQuery = query;
            Where = new ContextFilter(query.Where);
        }

        public int GetCount()
        {
            SelectQuery.Execute();
            return SelectQuery.RowCount;
        }

        public async Task<int> GetCountAsync(CancellationToken? token = null)
        {
            await SelectQuery.ExecuteAsync(token);
            return SelectQuery.RowCount;
        }
    }
}