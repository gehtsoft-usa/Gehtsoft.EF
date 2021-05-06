using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    public interface IEntityQuery : IDisposable
    {
        int Execute();

        Task<int> ExecuteAsync(CancellationToken? token = null);
    }

    public interface IModifyEntityQuery : IDisposable
    {
        void Execute(object entity);

        Task ExecuteAsync(object entity, CancellationToken? token = null);
    }

    public interface IEntityContextTransaction : IDisposable
    {
        void Commit();

        void Rollback();
    }

    public interface IContextFilterCondition
    {
        IContextFilterCondition Property(string name);

        IContextFilterCondition Is(CmpOp op);

        IContextFilterCondition Value(object value);
    }

    public interface IContextFilter
    {
        IDisposable AddGroup(LogOp logOp = LogOp.And);

        IContextFilterCondition Add(LogOp op = LogOp.And);
    }

    public interface IContextQueryWithCondition : IEntityQuery
    {
        IContextFilter Where { get; }
    }

    public interface IContextOrder
    {
        IContextOrder Add(string name, SortDir sortDir = SortDir.Asc);
    }

    public interface IContextSelect : IContextQueryWithCondition
    {
        IContextOrder Order { get; }

        int? Take { get; set; }
        int? Skip { get; set; }

        object ReadOne();

        Task<object> ReadOneAsync(CancellationToken? token = null);
    }

    public interface IContextCount : IContextQueryWithCondition
    {
        int GetCount();

        Task<int> GetCountAsync(CancellationToken? token = null);
    }

    public static class EntityFilterBuilderExtension
    {
        public static IContextFilterCondition And(this IContextFilter builder) => builder.Add(LogOp.And);

        public static IContextFilterCondition Or(this IContextFilter builder) => builder.Add(LogOp.Or);

        public static IContextFilterCondition Property(this IContextFilter builder, string name) => builder.Add(LogOp.And).Property(name);

        public static IContextFilterCondition IsNull(this IContextFilter builder, string name) => builder.Add(LogOp.And).Is(CmpOp.IsNull).Property(name);

        public static IContextFilterCondition NotNull(this IContextFilter builder, string name) => builder.Add(LogOp.And).Is(CmpOp.NotNull).Property(name);

        public static IContextFilterCondition Is(this IContextFilter builder, CmpOp op) => builder.Add(LogOp.And).Is(op);

        public static IContextFilterCondition Eq(this IContextFilterCondition condition) => condition.Is(CmpOp.Eq);

        public static IContextFilterCondition Neq(this IContextFilterCondition condition) => condition.Is(CmpOp.Neq);

        public static IContextFilterCondition Gt(this IContextFilterCondition condition) => condition.Is(CmpOp.Gt);

        public static IContextFilterCondition Ge(this IContextFilterCondition condition) => condition.Is(CmpOp.Ge);

        public static IContextFilterCondition Ls(this IContextFilterCondition condition) => condition.Is(CmpOp.Ls);

        public static IContextFilterCondition Le(this IContextFilterCondition condition) => condition.Is(CmpOp.Le);

        public static IContextFilterCondition Like(this IContextFilterCondition condition) => condition.Is(CmpOp.Like);

        public static IContextFilterCondition IsNull(this IContextFilterCondition condition) => condition.Is(CmpOp.IsNull);

        public static IContextFilterCondition NotNull(this IContextFilterCondition condition) => condition.Is(CmpOp.NotNull);

        public static IContextFilterCondition Eq(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Eq).Value(value);

        public static IContextFilterCondition Neq(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Neq).Value(value);

        public static IContextFilterCondition Gt(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Gt).Value(value);

        public static IContextFilterCondition Ge(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Ge).Value(value);

        public static IContextFilterCondition Ls(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Ls).Value(value);

        public static IContextFilterCondition Le(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Le).Value(value);

        public static IContextFilterCondition Like(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Like).Value(value);
    }

    public interface IEntityContext : IDisposable
    {
        IEntityQuery DropEntity(Type type);

        IEntityQuery CreateEntity(Type type);

        IModifyEntityQuery InsertEntity(Type type, bool createKey);

        IModifyEntityQuery UpdateEntity(Type type);

        IModifyEntityQuery DeleteEntity(Type type);

        IContextQueryWithCondition DeleteMultiple(Type type);

        IContextSelect Select(Type type);

        IContextCount Count(Type type);

        IEntityContextTransaction BeginTransaction();
    }

    public static class EntityContextExtension
    {
        public static IEntityQuery DropEntity<T>(this IEntityContext context) => context.DropEntity(typeof(T));

        public static IEntityQuery CreateEntity<T>(this IEntityContext context) => context.CreateEntity(typeof(T));

        public static IModifyEntityQuery InsertEntity<T>(this IEntityContext context, bool createKey = true) => context.InsertEntity(typeof(T), createKey);

        public static IModifyEntityQuery UpdateEntity<T>(this IEntityContext context) => context.UpdateEntity(typeof(T));

        public static IModifyEntityQuery DeleteEntity<T>(this IEntityContext context) => context.DeleteEntity(typeof(T));

        public static IContextQueryWithCondition DeleteMultiple<T>(this IEntityContext context) => context.DeleteMultiple(typeof(T));

        public static IContextSelect Select<T>(this IEntityContext context) => context.Select(typeof(T));

        public static IContextCount Count<T>(this IEntityContext context) => context.Count(typeof(T));

        public static T ReadOne<T>(this IContextSelect query) => (T)query.ReadOne();

        public static async Task<T> ReadOneAsync<T>(this IContextSelect query) => (T)(await query.ReadOneAsync());

        public static TC ReadAll<TC, T>(this IContextSelect query)
            where TC : ICollection<T>, new()
            where T : class
        {
            TC r = new TC();
            T t;
            while (true)
            {
                t = (T)query.ReadOne();
                if (t == null)
                    break;
                r.Add(t);
            }
            return r;
        }

        public static async Task<TC> ReadAllAsync<TC, T>(this IContextSelect query, CancellationToken? token = null)
            where TC : ICollection<T>, new()
            where T : class
        {
            TC r = new TC();
            T t;
            while (true)
            {
                t = (T)(await query.ReadOneAsync(token));
                if (t == null)
                    break;
                r.Add(t);
            }
            return r;
        }

        public static T Get<T>(this IEntityContext context, object key)
        {
            using (var query = context.Select(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                query.Execute();
                return query.ReadOne<T>();
            }
        }

        public static async Task<T> GetAsync<T>(this IEntityContext context, object key, CancellationToken? token = null)
        {
            using (var query = context.Select(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                await query.ExecuteAsync(token);
                return await query.ReadOneAsync<T>();
            }
        }

        public static bool Exists<T>(this IEntityContext context, object key)
        {
            using (var query = context.Count(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                return query.GetCount() > 0;
            }
        }

        public static async Task<bool> ExistsAsync<T>(this IEntityContext context, object key, CancellationToken? _ = null)
        {
            using (var query = context.Count(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                return (await query.GetCountAsync()) > 0;
            }
        }

        public static void Save<T>(this IEntityContext context, T entity)
        {
            bool exists = context.Exists<T>(entity.GetEfEntityId());
            using (var query = exists ? context.UpdateEntity<T>() : context.InsertEntity<T>())
                query.Execute(entity);
        }

        public static async Task SaveAsync<T>(this IEntityContext context, T entity, CancellationToken? token = null)
        {
            bool exists = await context.ExistsAsync<T>(entity.GetEfEntityId(), token);
            using (var query = exists ? context.UpdateEntity<T>() : context.InsertEntity<T>())
                await query.ExecuteAsync(entity, token);
        }
    }
}