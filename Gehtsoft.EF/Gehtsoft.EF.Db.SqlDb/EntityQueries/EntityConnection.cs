using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    [DocgenIgnore]
    [ExcludeFromCodeCoverage]
    public static class EntityConnectionBuilderExtension
    {
        /// <summary>
        /// Returns the query to create the entity table.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static AQueryBuilder GetCreateEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return connection.GetCreateTableBuilder(AllEntities.Inst[type].TableDescriptor);
        }

        /// <summary>
        /// Returns the query to create the entity view.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static AQueryBuilder GetCreateViewQueryBuilder(this SqlDbConnection connection, Type type)
        {
            var entityDescriptor = AllEntities.Inst[type];
            if (!entityDescriptor.TableDescriptor.View)
                throw new ArgumentException($"Type {type.FullName} is not attributed as a view", nameof(type));
            if (entityDescriptor.TableDescriptor.Metadata is IViewCreationMetadata vcm)
                return connection.GetCreateViewBuilder(entityDescriptor.TableDescriptor.Name, vcm.GetSelectQuery(connection));
            throw new ArgumentException($"Type {type.FullName} does not have view creation meta data", nameof(type));
        }

        /// <summary>
        /// Returns the query to create the entity table (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static AQueryBuilder GetCreateEntityQueryBuilder<T>(this SqlDbConnection connection) => GetCreateEntityQueryBuilder(connection, typeof(T));

        /// <summary>
        /// Returns the query to create the entity view (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static AQueryBuilder GetCreateViewQueryBuilder<T>(this SqlDbConnection connection) => GetCreateViewQueryBuilder(connection, typeof(T));

        /// <summary>
        /// Returns the query to drop entity table.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static AQueryBuilder GetDropEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return connection.GetDropTableBuilder(AllEntities.Inst[type].TableDescriptor);
        }

        /// <summary>
        /// Returns the query to drop entity table (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static AQueryBuilder GetDropEntityQueryBuilder<T>(this SqlDbConnection connection) => GetDropEntityQueryBuilder(connection, typeof(T));

        /// <summary>
        /// Returns the query to drop entity view.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static AQueryBuilder GetDropViewQueryBuilder(this SqlDbConnection connection, Type type)
        {
            var entityDescriptor = AllEntities.Inst[type];
            if (!entityDescriptor.TableDescriptor.View)
                throw new ArgumentException($"Type {type.FullName} is not attributed as a view", nameof(type));
            if (entityDescriptor.TableDescriptor.Metadata is IViewCreationMetadata)
                return connection.GetDropViewBuilder(entityDescriptor.TableDescriptor.Name);
            throw new ArgumentException($"Type {type.FullName} does not have view creation meta data", nameof(type));
        }

        /// <summary>
        /// Returns the query to drop entity view (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static AQueryBuilder GetDropViewQueryBuilder<T>(this SqlDbConnection connection) => GetDropViewQueryBuilder(connection, typeof(T));

        /// <summary>
        /// Returns the query to insert the entity.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <param name="ignoreAutoIncrement"></param>
        /// <returns></returns>
        internal static InsertEntityQueryBuilder GetInsertEntityQueryBuilder(this SqlDbConnection connection, Type type, bool ignoreAutoIncrement = false)
        {
            return new InsertEntityQueryBuilder(type, connection, ignoreAutoIncrement);
        }

        /// <summary>
        /// Returns the query to delete one entity by id.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static DeleteEntityQueryBuilder GetDeleteEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new DeleteEntityQueryBuilder(type, connection);
        }

        /// <summary>
        /// Returns the query to update one entity by id.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static UpdateEntityQueryBuilder GetUpdateEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new UpdateEntityQueryBuilder(type, connection);
        }

        /// <summary>
        /// Returns the query that selects the entities.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static SelectEntityQueryBuilder GetSelectEntityQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new SelectEntityQueryBuilder(type, connection);
        }

        /// <summary>
        /// Returns the query that selects the number of entities.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static SelectEntityCountQueryBuilder GetSelectEntityCountQueryBuilder(this SqlDbConnection connection, Type type)
        {
            return new SelectEntityCountQueryBuilder(type, connection);
        }

        internal static InsertEntityQueryBuilder GetInsertEntityQueryBuilder<T>(this SqlDbConnection connection) => connection.GetInsertEntityQueryBuilder(typeof(T));

        internal static InsertEntityQueryBuilder GetInsertEntityQueryBuilder<T>(this SqlDbConnection connection, bool ignoreAutoIncrement) => connection.GetInsertEntityQueryBuilder(typeof(T), ignoreAutoIncrement);

        internal static DeleteEntityQueryBuilder GetDeleteEntityQueryBuilder<T>(this SqlDbConnection connection) => connection.GetDeleteEntityQueryBuilder(typeof(T));

        internal static UpdateEntityQueryBuilder GetUpdateEntityQueryBuilder<T>(this SqlDbConnection connection) => connection.GetUpdateEntityQueryBuilder(typeof(T));

        internal static SelectEntityQueryBuilder GetSelectEntityQueryBuilder<T>(this SqlDbConnection connection) => connection.GetSelectEntityQueryBuilder(typeof(T));

        internal static SelectEntityCountQueryBuilder GetSelectEntityCountQueryBuilder<T>(this SqlDbConnection connection) => connection.GetSelectEntityCountQueryBuilder(typeof(T));
    }

    /// <summary>
    /// Extension class to create entity queries.
    /// </summary>
    public static class EntityConnectionExtension
    {
        /// <summary>
        /// Returns the query that creates the entity table.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static EntityQuery GetCreateEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetCreateEntityQueryBuilder(type)));
        }

        /// <summary>
        /// Returns the query that create the entity view.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static EntityQuery GetCreateViewQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetCreateViewQueryBuilder(type)));
        }

        /// <summary>
        /// Returns the query that drop the entity table.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static EntityQuery GetDropEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetDropEntityQueryBuilder(type)));
        }

        /// <summary>
        /// Returns the query that drops the entity view.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static EntityQuery GetDropViewQuery(this SqlDbConnection connection, Type type)
        {
            return new EntityQuery(connection.GetQuery(), new EntityQueryBuilder(connection.GetLanguageSpecifics(), type, connection.GetDropViewQueryBuilder(type)));
        }

        /// <summary>
        /// Returns the query that inserts one entity.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <param name="ignoreAutoIncrement"></param>
        /// <returns></returns>
        public static ModifyEntityQuery GetInsertEntityQuery(this SqlDbConnection connection, Type type, bool ignoreAutoIncrement = false)
        {
            return new InsertEntityQuery(connection.GetQuery(), connection.GetInsertEntityQueryBuilder(type, ignoreAutoIncrement));
        }

        /// <summary>
        /// Returns the query that updates one entity by id.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static ModifyEntityQuery GetUpdateEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new UpdateEntityQuery(connection.GetQuery(), connection.GetUpdateEntityQueryBuilder(type));
        }

        /// <summary>
        /// Returns the query that deletes one entity by id.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static ModifyEntityQuery GetDeleteEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new DeleteEntityQuery(connection.GetQuery(), connection.GetDeleteEntityQueryBuilder(type));
        }

        /// <summary>
        /// Returns the query that deletes multiple entities by condition.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static MultiDeleteEntityQuery GetMultiDeleteEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new MultiDeleteEntityQuery(connection.GetQuery(), connection.GetDeleteEntityQueryBuilder(type));
        }

        /// <summary>
        /// Returns the query that updates multiple entities by condition.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static MultiUpdateEntityQuery GetMultiUpdateEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new MultiUpdateEntityQuery(connection.GetQuery(), connection.GetUpdateEntityQueryBuilder(type));
        }

        /// <summary>
        /// Returns the query to construct free-from select entity query.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static SelectEntitiesQueryBase GetGenericSelectEntityQuery(this SqlDbConnection connection, Type type)
        {
            return new SelectEntitiesQueryBase(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection));
        }

        /// <summary>
        /// Returns the query to select a tree of self-connected entities.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <param name="hasRootParam"></param>
        /// <returns></returns>
        public static SelectEntitiesTreeQuery GetSelectEntityTreeQuery(this SqlDbConnection connection, Type type, bool hasRootParam)
        {
            return new SelectEntitiesTreeQuery(connection.GetQuery(), new SelectEntityTreeQueryBuilder(type, connection, hasRootParam));
        }

        /// <summary>
        /// Returns the query to select count of entities.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static SelectEntitiesCountQuery GetSelectEntitiesCountQuery(this SqlDbConnection connection, Type type)
        {
            return new SelectEntitiesCountQuery(connection.GetQuery(), new SelectEntityCountQueryBuilder(type, connection));
        }

        /// <summary>
        /// Returns the query to select the entities completely.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <param name="exclusions"></param>
        /// <returns></returns>
        public static SelectEntitiesQuery GetSelectEntitiesQuery(this SqlDbConnection connection, Type type, SelectEntityQueryFilter[] exclusions = null)
        {
            return new SelectEntitiesQuery(connection.GetQuery(), new SelectEntityQueryBuilder(type, connection, exclusions));
        }

        /// <summary>
        /// Returns the query that creates the entity table (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static EntityQuery GetCreateEntityQuery<T>(this SqlDbConnection connection)
            => connection.GetCreateEntityQuery(typeof(T));

        /// <summary>
        /// Returns the query that drops the entity table (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static EntityQuery GetDropEntityQuery<T>(this SqlDbConnection connection)
            => GetDropEntityQuery(connection, typeof(T));

        /// <summary>
        /// Returns the query that inserts one entity (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="ignoreAutoIncrement"></param>
        /// <returns></returns>
        public static ModifyEntityQuery GetInsertEntityQuery<T>(this SqlDbConnection connection, bool ignoreAutoIncrement = false)
            => GetInsertEntityQuery(connection, typeof(T), ignoreAutoIncrement);

        /// <summary>
        /// Returns the query that updates one entity by id (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static ModifyEntityQuery GetUpdateEntityQuery<T>(this SqlDbConnection connection)
            => GetUpdateEntityQuery(connection, typeof(T));

        /// <summary>
        /// Returns the query that deletes one entity by id (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static ModifyEntityQuery GetDeleteEntityQuery<T>(this SqlDbConnection connection)
            => GetDeleteEntityQuery(connection, typeof(T));

        /// <summary>
        /// Returns the query to construct free-from select entity query (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static SelectEntitiesQueryBase GetGenericSelectEntityQuery<T>(this SqlDbConnection connection)
            => GetGenericSelectEntityQuery(connection, typeof(T));

        public static SelectEntitiesCountQuery GetSelectEntitiesCountQuery<T>(this SqlDbConnection connection)
            => GetSelectEntitiesCountQuery(connection, typeof(T));

        /// <summary>
        /// Returns the query to select the entities completely (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="exclusions"></param>
        /// <returns></returns>
        public static SelectEntitiesQuery GetSelectEntitiesQuery<T>(this SqlDbConnection connection, SelectEntityQueryFilter[] exclusions = null)
            => GetSelectEntitiesQuery(connection, typeof(T), exclusions);

        /// <summary>
        /// Gets query to construct a free-form entity select query.
        ///
        /// The method is a synonym for <see cref="GetGenericSelectEntityQuery(SqlDbConnection, Type)"/>
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static SelectEntitiesQueryBase GetSelectEntitiesQueryBase(this SqlDbConnection connection, Type type)
            => new SelectEntitiesQueryBase(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection));

        /// <summary>
        /// Gets query to construct a free-form entity select query (generic version).
        ///
        /// The method is a synonym for <see cref="GetGenericSelectEntityQuery(SqlDbConnection, Type)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static SelectEntitiesQueryBase GetSelectEntitiesQueryBase<T>(this SqlDbConnection connection)
            => GetSelectEntitiesQueryBase(connection, typeof(T));

        /// <summary>
        /// Returns the query to select a tree of self-connected entities.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="hasRootParameter"></param>
        public static SelectEntitiesTreeQuery GetSelectEntitiesTreeQuery<T>(this SqlDbConnection connection, bool hasRootParameter = true)
            => new SelectEntitiesTreeQuery(connection.GetQuery(), new SelectEntityTreeQueryBuilder(typeof(T), connection, hasRootParameter));

        /// <summary>
        /// Returns the query that deletes multiple entities by condition (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static MultiDeleteEntityQuery GetMultiDeleteEntityQuery<T>(this SqlDbConnection connection)
            => GetMultiDeleteEntityQuery(connection, typeof(T));

        /// <summary>
        /// Returns the query that updates multiple entities by condition (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static MultiUpdateEntityQuery GetMultiUpdateEntityQuery<T>(this SqlDbConnection connection)
            => GetMultiUpdateEntityQuery(connection, typeof(T));

        /// <summary>
        /// Returns the query that selects the entity by its identifier.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="entity"></param>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        public static SelectEntitiesQuery GetSelectOneEntityQuery(this SqlDbConnection connection, Type entity, object primaryKey)
        {
            var query = connection.GetSelectEntitiesQuery(entity);
            query.Where.Property(entity.GetEfPrimaryKey().Name).Eq(primaryKey);
            return query;
        }

        /// <summary>
        /// Returns the query that selects the entity by its identifier (generic version).
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        public static SelectEntitiesQuery GetSelectOneEntityQuery<T>(this SqlDbConnection connection, object primaryKey)
            => connection.GetSelectOneEntityQuery(typeof(T), primaryKey);

        /// <summary>
        /// Returns the query that inserts the results of an entity select query into another entity.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="entityType"></param>
        /// <param name="selectQuery"></param>
        /// <param name="ignoreAutoIncrement"></param>
        /// <param name="includeOnlyProperties"></param>
        /// <returns></returns>
        public static InsertSelectEntityQuery GetInsertSelectEntityQuery(this SqlDbConnection connection, Type entityType, SelectEntitiesQueryBase selectQuery, bool ignoreAutoIncrement = false, string[] includeOnlyProperties = null)
            => new InsertSelectEntityQuery(connection.GetQuery(), entityType, selectQuery.SelectBuilder, ignoreAutoIncrement, includeOnlyProperties);

        /// <summary>
        /// Returns the query that inserts the results of an entity select query into another entity (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="selectQuery"></param>
        /// <param name="ignoreAutoIncrement"></param>
        /// <param name="includeOnlyProperties"></param>
        /// <returns></returns>
        public static InsertSelectEntityQuery GetInsertSelectEntityQuery<T>(this SqlDbConnection connection, SelectEntitiesQueryBase selectQuery, bool ignoreAutoIncrement = false, string[] includeOnlyProperties = null)
            => connection.GetInsertSelectEntityQuery(typeof(T), selectQuery, ignoreAutoIncrement, includeOnlyProperties);

        /// <summary>
        /// Checks whether entity has no dependencies and can be deleted.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static bool CanDelete<T>(this SqlDbConnection connection, T entity) => CanDelete<T>(connection, entity, null);

        /// <summary>
        /// Checks whether entity has no dependencies and can be deleted (asynchronous version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static Task<bool> CanDeleteAsync<T>(this SqlDbConnection connection, T entity) => CanDeleteAsync<T>(connection, entity, null, null);

        /// <summary>
        /// Checks whether entity has no dependencies and can be deleted excepting some dependencies by the type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="entity"></param>
        /// <param name="except"></param>
        /// <returns></returns>
        public static bool CanDelete<T>(this SqlDbConnection connection, T entity, Type[] except) => connection.CanDeleteCore(true, entity, except, null).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>
        /// Checks whether entity has no dependencies and can be deleted excepting some dependencies by the type (asynchronous version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="entity"></param>
        /// <param name="except"></param>
        /// <param name="token"></param>
        /// <returns></returns>
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
