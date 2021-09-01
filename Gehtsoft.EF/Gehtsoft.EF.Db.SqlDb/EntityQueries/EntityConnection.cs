using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public static class EntityConnectionExtension
    {
        public static AQueryBuilder GetCreateEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return connection.GetCreateTableBuilder(AllEntities.Inst[type].TableDescriptor);
        }

        public static AQueryBuilder GetCreateViewQueryBuilder(this SqlDbConnection connection, Type type)
        {
            var entityDescriptor = AllEntities.Inst[type];
            if (!entityDescriptor.TableDescriptor.View)
                throw new ArgumentException($"Type {type.FullName} is not attributed as a view", nameof(type));
            if (entityDescriptor.TableDescriptor.Metadata is IViewCreationMetadata vcm)
                return connection.GetCreateViewBuilder(entityDescriptor.TableDescriptor.Name, vcm.GetSelectQuery(connection));
            throw new ArgumentException($"Type {type.FullName} does not have view creation meta data", nameof(type));
        }

        public static AQueryBuilder GetCreateEntityQueryBuilder<T>(this SqlDbConnection connection) => GetCreateEntityQueryBuilder(connection, typeof(T));

        public static AQueryBuilder GetCreateViewQueryBuilder<T>(this SqlDbConnection connection) => GetCreateViewQueryBuilder(connection, typeof(T));

        public static AQueryBuilder GetDropEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return connection.GetDropTableBuilder(AllEntities.Inst[type].TableDescriptor);
        }

        public static AQueryBuilder GetDropEntityQueryBuilder<T>(this SqlDbConnection connection) => GetDropEntityQueryBuilder(connection, typeof(T));

        public static AQueryBuilder GetDropViewQueryBuilder(this SqlDbConnection connection, Type type)
        {
            var entityDescriptor = AllEntities.Inst[type];
            if (!entityDescriptor.TableDescriptor.View)
                throw new ArgumentException($"Type {type.FullName} is not attributed as a view", nameof(type));
            if (entityDescriptor.TableDescriptor.Metadata is IViewCreationMetadata)
                return connection.GetDropViewBuilder(entityDescriptor.TableDescriptor.Name);
            throw new ArgumentException($"Type {type.FullName} does not have view creation meta data", nameof(type));
        }

        public static AQueryBuilder GetDropViewQueryBuilder<T>(this SqlDbConnection connection) => GetDropViewQueryBuilder(connection, typeof(T));

        internal static InsertEntityQueryBuilder GetInsertEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new InsertEntityQueryBuilder(type, connection, false);
        }

        internal static InsertEntityQueryBuilder GetInsertEntityQueryBuilder(this SqlDbConnection connection, Type type, bool ignoreAutoIncrement)
        {
            return new InsertEntityQueryBuilder(type, connection, ignoreAutoIncrement);
        }

        internal static DeleteEntityQueryBuilder GetDeleteEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new DeleteEntityQueryBuilder(type, connection);
        }

        internal static UpdateEntityQueryBuilder GetUpdateEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new UpdateEntityQueryBuilder(type, connection);
        }

        internal static SelectEntityQueryBuilder GetSelectEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new SelectEntityQueryBuilder(type, connection);
        }

        internal static SelectEntityCountQueryBuilder GetSelectEntityCountQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new SelectEntityCountQueryBuilder(type, connection);
        }

        public static EntityQuery GetCreateEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetCreateEntityQueryBuilder(type)));
        }

        public static EntityQuery GetCreateViewQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetCreateViewQueryBuilder(type)));
        }

        public static EntityQuery GetDropEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetDropEntityQueryBuilder(type)));
        }

        public static EntityQuery GetDropViewQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetDropViewQueryBuilder(type)));
        }

        public static ModifyEntityQuery GetInsertEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new InsertEntityQuery(connection.GetQuery(), GetInsertEntityQueryBuilder(connection, type));
        }

        public static ModifyEntityQuery GetInsertEntityQuery(this SqlDbConnection connection, Type type, bool ignoreAutoIncrement)
        {
            return new InsertEntityQuery(connection.GetQuery(), GetInsertEntityQueryBuilder(connection, type, ignoreAutoIncrement));
        }

        public static ModifyEntityQuery GetUpdateEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new UpdateEntityQuery(connection.GetQuery(), GetUpdateEntityQueryBuilder(connection, type));
        }

        public static ModifyEntityQuery GetDeleteEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new DeleteEntityQuery(connection.GetQuery(), GetDeleteEntityQueryBuilder(connection, type));
        }

        public static MultiDeleteEntityQuery GetMultiDeleteEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new MultiDeleteEntityQuery(connection.GetQuery(), GetDeleteEntityQueryBuilder(connection, type));
        }

        public static MultiUpdateEntityQuery GetMultiUpdateEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new MultiUpdateEntityQuery(connection.GetQuery(), GetUpdateEntityQueryBuilder(connection, type));
        }

        public static SelectEntitiesQueryBase GetGenericSelectEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new SelectEntitiesQueryBase(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection));
        }

        public static SelectEntitiesTreeQuery GetSelectEntityTreeQuery(this SqlDbConnection connection, Type type, bool hasRootParam)
        {
            return new SelectEntitiesTreeQuery(connection.GetQuery(), new SelectEntityTreeQueryBuilder(type, connection, hasRootParam));
        }

        public static SelectEntitiesCountQuery GetSelectEntitiesCountQuery(this SqlDbConnection connection, Type type)
        {
            return new SelectEntitiesCountQuery(connection.GetQuery(), new SelectEntityCountQueryBuilder(type, connection));
        }

        public static SelectEntitiesQuery GetSelectEntitiesQuery(this SqlDbConnection connection, Type type, SelectEntityQueryFilter[] exclusions = null)
        {
            return new SelectEntitiesQuery(connection.GetQuery(), new SelectEntityQueryBuilder(type, connection, exclusions));
        }

        internal static InsertEntityQueryBuilder GetInsertEntityQueryBuilder<T>(this SqlDbConnection connection) => GetInsertEntityQueryBuilder(connection, typeof(T));

        internal static InsertEntityQueryBuilder GetInsertEntityQueryBuilder<T>(this SqlDbConnection connection, bool ignoreAutoIncrement) => GetInsertEntityQueryBuilder(connection, typeof(T), ignoreAutoIncrement);

        internal static DeleteEntityQueryBuilder GetDeleteEntityQueryBuilder<T>(this SqlDbConnection connection) => GetDeleteEntityQueryBuilder(connection, typeof(T));

        internal static UpdateEntityQueryBuilder GetUpdateEntityQueryBuilder<T>(this SqlDbConnection connection) => GetUpdateEntityQueryBuilder(connection, typeof(T));

        internal static SelectEntityQueryBuilder GetSelectEntityQueryBuilder<T>(this SqlDbConnection connection) => GetSelectEntityQueryBuilder(connection, typeof(T));

        internal static SelectEntityCountQueryBuilder GetSelectEntityCountQueryBuilder<T>(this SqlDbConnection connection) => GetSelectEntityCountQueryBuilder(connection, typeof(T));

        public static EntityQuery GetCreateEntityQuery<T>(this SqlDbConnection connection) => GetCreateEntityQuery(connection, typeof(T));

        public static EntityQuery GetDropEntityQuery<T>(this SqlDbConnection connection) => GetDropEntityQuery(connection, typeof(T));

        public static ModifyEntityQuery GetInsertEntityQuery<T>(this SqlDbConnection connection) => GetInsertEntityQuery(connection, typeof(T));

        public static ModifyEntityQuery GetInsertEntityQuery<T>(this SqlDbConnection connection, bool ignoreAutoIncrement) => GetInsertEntityQuery(connection, typeof(T), ignoreAutoIncrement);

        public static ModifyEntityQuery GetUpdateEntityQuery<T>(this SqlDbConnection connection) => GetUpdateEntityQuery(connection, typeof(T));

        public static ModifyEntityQuery GetDeleteEntityQuery<T>(this SqlDbConnection connection) => GetDeleteEntityQuery(connection, typeof(T));

        public static SelectEntitiesQueryBase GetGenericSelectEntityQuery<T>(this SqlDbConnection connection) => new SelectEntitiesQueryBase(connection.GetQuery(), new SelectEntityQueryBuilderBase(typeof(T), connection));

        public static SelectEntitiesCountQuery GetSelectEntitiesCountQuery<T>(this SqlDbConnection connection) => new SelectEntitiesCountQuery(connection.GetQuery(), GetSelectEntityCountQueryBuilder(connection, typeof(T)));

        public static SelectEntitiesQuery GetSelectEntitiesQuery<T>(this SqlDbConnection connection, SelectEntityQueryFilter[] exclusions = null) => new SelectEntitiesQuery(connection.GetQuery(), new SelectEntityQueryBuilder(typeof(T), connection, exclusions));

        public static SelectEntitiesQueryBase GetSelectEntitiesQueryBase(this SqlDbConnection connection, Type type) => new SelectEntitiesQueryBase(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection));
        public static SelectEntitiesQueryBase GetSelectEntitiesQueryBase<T>(this SqlDbConnection connection) => new SelectEntitiesQueryBase(connection.GetQuery(), new SelectEntityQueryBuilderBase(typeof(T), connection));

        public static SelectEntitiesTreeQuery GetSelectEntitiesTreeQuery<T>(this SqlDbConnection connection, bool hasRootParameter = true) => new SelectEntitiesTreeQuery(connection.GetQuery(), new SelectEntityTreeQueryBuilder(typeof(T), connection, hasRootParameter));

        public static MultiDeleteEntityQuery GetMultiDeleteEntityQuery<T>(this SqlDbConnection connection) => new MultiDeleteEntityQuery(connection.GetQuery(), GetDeleteEntityQueryBuilder(connection, typeof(T)));

        public static MultiUpdateEntityQuery GetMultiUpdateEntityQuery<T>(this SqlDbConnection connection) => new MultiUpdateEntityQuery(connection.GetQuery(), GetUpdateEntityQueryBuilder(connection, typeof(T)));

        public static bool CanDelete<T>(this SqlDbConnection connection, T entity) => CanDelete<T>(connection, entity, null);
        public static Task<bool> CanDeleteAsync<T>(this SqlDbConnection connection, T entity) => CanDeleteAsync<T>(connection, entity, null, null);

        public static bool CanDelete<T>(this SqlDbConnection connection, T entity, Type[] except) => connection.CanDeleteCore(true, entity, except, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public static Task<bool> CanDeleteAsync<T>(this SqlDbConnection connection, T entity, Type[] except, CancellationToken? token)
        {
            return connection.CanDeleteCore(false, entity, except, token);
        }

        internal static async Task<bool> CanDeleteCore<T>(this SqlDbConnection connection, bool sync, T entity, Type[] except, CancellationToken? token)
        {
            EntityDescriptor value = AllEntities.Inst[typeof(T)];
            TableDescriptor[] exceptTableDescriptors = except == null ? null : new TableDescriptor[except.Length];
            if (except != null)
            {
                for (int i = 0; i < except.Length; i++)
                    exceptTableDescriptors[i] = AllEntities.Inst[except[i]].TableDescriptor;
            }

            foreach (Type t in AllEntities.Inst)
            {
                EntityDescriptor other = AllEntities.Inst[t];
                if (exceptTableDescriptors == null || !exceptTableDescriptors.Any(x => x.Name == other.TableDescriptor.Name))
                {
                    foreach (TableDescriptor.ColumnInfo ci in other.TableDescriptor)
                    {
                        if (ci.ForeignKey && ci.ForeignTable.Name == value.TableDescriptor.Name)
                        {
                            using (SelectEntitiesCountQuery query = GetSelectEntitiesCountQuery(connection, t))
                            {
                                query.Where.Add().Property(ci.ID).Is(CmpOp.Eq).Value(entity);
                                if (!sync)
                                    await query.ExecuteAsync(token);
                                if (query.RowCount > 0)
                                    return false;
                            }
                        }
                    }
                }
            }
            return true;
        }
    }
}
