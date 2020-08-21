using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.EF.FTS
{
    public static class FtsConnection
    {
        private static Type[] gTypes = new Type[] {typeof(FtsWordEntity), typeof(FtsObjectEntity), typeof(FtsWord2ObjectEntity)};

        private static async Task FtsCreateTablesCore(this SqlDbConnection connection, bool sync, CancellationToken? token)
        {
            foreach (Type t in gTypes.Reverse())
            {
                using (EntityQuery query = connection.GetDropEntityQuery(t))
                {
                    if (sync)
                        query.Execute();
                    else
                        await query.ExecuteAsync(token);
                }
            }

            foreach (Type t in gTypes)
            {
                using (EntityQuery query = connection.GetCreateEntityQuery(t))
                {
                    if (sync)
                        query.Execute();
                    else
                        await query.ExecuteAsync(token);
                }
            }
        }

        public static void FtsCreateTables(this SqlDbConnection connection) => connection.FtsCreateTablesCore(true, null).ConfigureAwait(false).GetAwaiter().GetResult();
        public static Task FtsCreateTablesAsync(this SqlDbConnection connection, CancellationToken? token = null) => connection.FtsCreateTablesCore(false, token);
        public static void FtsDropTables(this SqlDbConnection connection)
        {
            foreach (Type t in gTypes.Reverse())
            {
                using (EntityQuery query = connection.GetDropEntityQuery(t))
                    query.Execute();
            }
        }
        public static async Task FtsDropTablesAsync(this SqlDbConnection connection, CancellationToken? token = null)
        {
            foreach (Type t in gTypes.Reverse())
            {
                using (EntityQuery query = connection.GetDropEntityQuery(t))
                    await query.ExecuteAsync(token);
            }
        }

        private static async Task<bool> DoesFtsTableExistCore(this SqlDbConnection connection, bool sync, CancellationToken? token)
        {
            TableDescriptor[] schema;

            if (sync)
                schema = connection.Schema();
            else
                schema = await connection.SchemaAsync(token);

            bool f1, f2, f3;

            f1 = f2 = f3 = false;
            foreach (TableDescriptor table in schema)
            {
                if (string.Compare(table.Name, "fts_words", StringComparison.OrdinalIgnoreCase) == 0)
                    f1 = true;
                else if (string.Compare(table.Name, "fts_objects", StringComparison.OrdinalIgnoreCase) == 0)
                    f2 = true;
                else if (string.Compare(table.Name, "fts_words2objects", StringComparison.OrdinalIgnoreCase) == 0)
                    f3 = true;
            }

            return f1 && f2 && f3;
        }


        public static bool DoesFtsTableExist(this SqlDbConnection connection) => connection.DoesFtsTableExistCore(true, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public static Task<bool> DoesFtsTableExistAsync(this SqlDbConnection connection, CancellationToken? token = null) => connection.DoesFtsTableExistCore(false, token);

        private static async Task<FtsObjectEntity> FtsFindOrCreateObjectCore(this SqlDbConnection connection, bool sync, string objectID, string objectType, string sorter, bool forceCreate, CancellationToken? token)
        {
            FtsObjectEntity entity;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(FtsObjectEntity)))
            {
                query.Where.And().PropertyOf(nameof(FtsObjectEntity.ObjectID)).Is(CmpOp.Eq).Value(objectID);
                query.Where.And().PropertyOf(nameof(FtsObjectEntity.ObjectType)).Is(CmpOp.Eq).Value(objectType);
                if (sync)
                    entity = query.ReadOne<FtsObjectEntity>();
                else
                    entity = await query.ReadOneAsync<FtsObjectEntity>(token);

                if (entity != null)
                    return entity;
            }

            if (!forceCreate)
                return null;

            entity = new FtsObjectEntity() { ObjectID = objectID, ObjectType = objectType, Sorter = sorter };
            using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(FtsObjectEntity)))
            {
                if (sync)
                    query.Execute(entity);
                else
                    await query.ExecuteAsync(entity, token);
                return entity;
            }
        }

        private static async Task<FtsWordEntity> FtsFindOrCreateWordCore(this SqlDbConnection connection, bool sync, string word, CancellationToken? token)
        {
            word = word.ToUpper();
            FtsWordEntity entity;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(FtsWordEntity)))
            {
                query.Where.Add().Property(nameof(FtsWordEntity.Word)).Is(CmpOp.Eq).Value(word);
                if (sync)
                    entity = query.ReadOne<FtsWordEntity>();
                else
                    entity = await query.ReadOneAsync<FtsWordEntity>(token);
                if (entity != null)
                    return entity;
            }

            entity = new FtsWordEntity() { Word = word };
            using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(FtsWordEntity)))
            {
                if (sync)
                    query.Execute(entity);
                else
                    await query.ExecuteAsync(entity, token);
                return entity;
            }
        }

        public static void FtsSetObjectText(this SqlDbConnection connection, string type, string objectID, string text)
        {
            connection.FtsSetObjectText(type, objectID, objectID, text);
        }

        public static async Task FtsSetObjectTextAsync(this SqlDbConnection connection, string type, string objectID, string text, CancellationToken? token = null)
        {
            await connection.FtsSetObjectTextAsync(type, objectID, objectID, text);
        }

        private static async Task FtsSetObjectTextCore(this SqlDbConnection connection, bool sync, string type, string objectID, string sorter, string text, CancellationToken? token)
        {
            string[] words = StringUtils.ParseToWords(text, false);
            FtsObjectEntity objectEntity;
            FtsWordEntity wordEntity;

            if (sync)
                objectEntity = connection.FtsFindOrCreateObjectCore(true, objectID, type, sorter, true, token).ConfigureAwait(false).GetAwaiter().GetResult();
            else
                objectEntity = await connection.FtsFindOrCreateObjectCore(false, objectID, type, sorter, true, token);

            DeleteEntityQueryBuilder builder = connection.GetDeleteEntityQueryBuilder(typeof(FtsWord2ObjectEntity));
            builder.Where.And().PropertyOf(nameof(FtsWord2ObjectEntity.Object), typeof(FtsWord2ObjectEntity)).Is(CmpOp.Eq).Parameter("objID");
            using (SqlDbQuery query = connection.GetQuery(builder.QueryBuilder))
            {
                query.BindParam("objID", objectEntity.ID);
                if (sync)
                    query.ExecuteNoData();
                else
                    await query.ExecuteNoDataAsync(token);
            }

            foreach (string word in words)
            {
                if (sync)
                    wordEntity = connection.FtsFindOrCreateWordCore(true, word, token).ConfigureAwait(false).GetAwaiter().GetResult();
                else
                    wordEntity = await connection.FtsFindOrCreateWordCore(false, word, token);
                FtsWord2ObjectEntity word2object = new FtsWord2ObjectEntity() { Object = objectEntity, Word = wordEntity };
                using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(FtsWord2ObjectEntity)))
                {
                    if (sync)
                        query.Execute(word2object);
                    else
                        await query.ExecuteAsync(word2object, token);
                }
            }
        }


        public static void FtsSetObjectText(this SqlDbConnection connection, string type, string objectID, string sorter, string text) => connection.FtsSetObjectTextCore(true, type, objectID, sorter, text, null).ConfigureAwait(false).GetAwaiter().GetResult();
        public static Task FtsSetObjectTextAsync(this SqlDbConnection connection, string type, string objectID, string sorter, string text, CancellationToken? token = null) => connection.FtsSetObjectTextCore(false, type, objectID, sorter, text, token);

        private static async Task FtsDeleteObjectCore(this SqlDbConnection connection, bool sync, string type, string objectID, CancellationToken? token)
        {
            FtsObjectEntity entity;

            if (sync)
                entity = connection.FtsFindOrCreateObjectCore(true, objectID, type, null, false, token).ConfigureAwait(false).GetAwaiter().GetResult();
            else
                entity = await connection.FtsFindOrCreateObjectCore(false, objectID, type, null, false, token);

            if (entity != null)
            {
                DeleteEntityQueryBuilder builder = connection.GetDeleteEntityQueryBuilder(typeof(FtsWord2ObjectEntity));
                builder.Where.And().PropertyOf(nameof(FtsWord2ObjectEntity.Object), typeof(FtsWord2ObjectEntity)).Is(CmpOp.Eq).Parameter("objID");

                using (SqlDbQuery query = connection.GetQuery(builder))
                {
                    query.BindParam("objID", entity.ID);

                    if (sync)
                        query.ExecuteNoData();
                    else
                        await query.ExecuteNoDataAsync(token);
                }

                using (ModifyEntityQuery query = connection.GetDeleteEntityQuery(typeof(FtsObjectEntity)))
                {
                    if (sync)
                        query.Execute(entity);
                    else
                        await query.ExecuteAsync(entity, token);
                }
            }
        }


        public static void FtsDeleteObject(this SqlDbConnection connection, string type, string objectID) => FtsDeleteObjectCore(connection, true, type, objectID, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public static Task FtsDeleteObjectAsync(this SqlDbConnection connection, string type, string objectID, CancellationToken? token = null) => FtsDeleteObjectCore(connection, false, type, objectID, token);

        private static async Task FtsCleanupWordsCore(this SqlDbConnection connection, bool sync, CancellationToken? token)
        {
            TableDescriptor words = AllEntities.Inst[typeof(FtsWordEntity)].TableDescriptor;
            TableDescriptor words2objects = AllEntities.Inst[typeof(FtsWord2ObjectEntity)].TableDescriptor;

            SelectQueryBuilder subquery = new SelectQueryBuilder(connection.GetLanguageSpecifics(), words);
            subquery.AddTable(words2objects, words2objects[nameof(FtsWord2ObjectEntity.Word)],
                TableJoinType.Left, subquery.Entities[0], words[nameof(FtsWordEntity.ID)]);
            subquery.AddToResultset(words[nameof(FtsWordEntity.ID)]);
            subquery.Having.Add(LogOp.And, subquery.Having.PropertyName(AggFn.Max, null, words2objects[nameof(FtsWord2ObjectEntity.ID)]), CmpOp.IsNull, null);
            subquery.AddGroupBy(words[nameof(FtsWordEntity.ID)]);

            if (connection.ConnectionType == "mysql")
            {
                List<int> ids = new List<int>();
                using (SqlDbQuery query = connection.GetQuery(subquery))
                {
                    if (sync)
                    {
                        query.ExecuteReader();
                        while (query.ReadNext())
                            ids.Add(query.GetValue<int>(0));
                    }
                    else
                    {
                        await query.ExecuteReaderAsync(token);
                        while (await query.ReadNextAsync(token))
                            ids.Add(query.GetValue<int>(0));
                    }
                }

                DeleteQueryBuilder mainquery = new DeleteQueryBuilder(connection.GetLanguageSpecifics(), words);
                mainquery.Where.Property(words[nameof(FtsWordEntity.ID)]).Is(CmpOp.Eq).Parameter("id");
                using (SqlDbQuery query = connection.GetQuery(mainquery))
                {
                    foreach (int id in ids)
                    {
                        query.BindParam("id", id);
                        if (sync)
                            query.ExecuteNoData();
                        else
                            await query.ExecuteNoDataAsync(token);
                    }
                }
            }
            else
            {
                DeleteQueryBuilder mainquery = new DeleteQueryBuilder(connection.GetLanguageSpecifics(), words);
                mainquery.Where.Property(words[nameof(FtsWordEntity.ID)]).Is(CmpOp.In).Query(subquery);

                using (SqlDbQuery query = connection.GetQuery(mainquery))
                {
                    if (sync)
                        query.ExecuteNoData();
                    else
                        await query.ExecuteNoDataAsync(token);
                }
            }
        }

        public static void FtsCleanupWords(this SqlDbConnection connection) => FtsCleanupWordsCore(connection, true, null).ConfigureAwait(false).GetAwaiter().GetResult();
        public static Task FtsCleanupWordsAsync(this SqlDbConnection connection, CancellationToken? token = null) => FtsCleanupWordsCore(connection, false, token);

        private static int gParamBase = 1;

        private static int NextParam => (gParamBase = (gParamBase + 1) % 65536);

        internal static void FtsAddWhereToQuery(this SqlDbConnection connection, FtsSelectQueryBuilder builder, string[]words, bool allWords, string[]types)
        {
            string paramBase = $"ftsparam{NextParam}_";
            int param = 0;
            TableDescriptor entityObjects = AllEntities.Inst[typeof(FtsObjectEntity)].TableDescriptor;
            TableDescriptor entityWords = AllEntities.Inst[typeof(FtsWordEntity)].TableDescriptor;
            TableDescriptor entityWords2Object = AllEntities.Inst[typeof(FtsWord2ObjectEntity)].TableDescriptor;
            List<FtsSelectQueryBuilder.QueryParameter> parameters =  new List<FtsSelectQueryBuilder.QueryParameter>();
            if (words != null && words.Length > 0)
            {
                using (var bracket = builder.QueryBuilder.Where.AddGroup())
                {
                    foreach (string word in words)
                    {
                        string paramName = $"{paramBase}{param++}";
                        SelectQueryBuilder subquery2 = connection.GetSelectQueryBuilder(entityWords);
                        subquery2.AddToResultset(entityWords[nameof(FtsWordEntity.ID)]);
                        subquery2.Where.Property(entityWords[nameof(FtsWordEntity.Word)]).Is(word.Contains("%") ? CmpOp.Like : CmpOp.Eq).Parameter(paramName);

                        SelectQueryBuilder subquery1 = connection.GetSelectQueryBuilder(entityWords2Object);
                        subquery1.AddToResultset(entityWords2Object[nameof(FtsWord2ObjectEntity.Object)]);
                        subquery1.Where.Property(entityWords2Object[nameof(FtsWord2ObjectEntity.Word)]).Is(CmpOp.In).Query(subquery2);

                        (allWords ? builder.QueryBuilder.Where.And() : builder.QueryBuilder.Where.Or())
                            .Property(entityObjects[nameof(FtsObjectEntity.ID)])
                            .Is(CmpOp.In)
                            .Query(subquery1);

                        parameters.Add(new FtsSelectQueryBuilder.QueryParameter() {Name = paramName, Type = typeof(string), Value = word});
                    }
                }
            }

            if (types != null && types.Length > 0)
            {
                if (types.Length > 1)
                {
                    ParameterGroupQueryBuilder subquery1 = connection.GetParameterGroupBuilder();
                    foreach (string type in types)
                    {
                        string paramName = $"{paramBase}{param++}";
                        subquery1.AddParameter(paramName);
                        parameters.Add(new FtsSelectQueryBuilder.QueryParameter() {Name = paramName, Type = typeof(string), Value = type});
                    }
                    builder.QueryBuilder.Where.Property(entityObjects[nameof(FtsObjectEntity.ObjectType)]).Is(CmpOp.In).Query(subquery1);
                }
                else
                {
                    string paramName = $"{paramBase}{param++}";
                    parameters.Add(new FtsSelectQueryBuilder.QueryParameter() {Name = paramName, Type = typeof(string), Value = types[0]});
                    builder.QueryBuilder.Where.Property(entityObjects[nameof(FtsObjectEntity.ObjectType)]).Is(CmpOp.Eq).Parameter(paramName);
                }
            }
            builder.Params = parameters.ToArray();
        }

        public static FtsSelectQueryBuilder FtsBuildQuery(this SqlDbConnection connection, FtsSelectQueryBuilder.QueryType type, string text, bool useMask, bool allWords, string[] types, int limit, int skip)
        {
            FtsSelectQueryBuilder builder = new FtsSelectQueryBuilder(connection.GetLanguageSpecifics());
            TableDescriptor words = AllEntities.Inst[typeof(FtsObjectEntity)].TableDescriptor;
            builder.QueryBuilder = connection.GetSelectQueryBuilder(words);
            switch (type)
            {
                case FtsSelectQueryBuilder.QueryType.ObjectList:
                    builder.QueryBuilder.Distinct = true;
                    builder.QueryBuilder.AddToResultset(words[nameof(FtsObjectEntity.ID)]);
                    builder.QueryBuilder.AddToResultset(words[nameof(FtsObjectEntity.ObjectID)]);
                    builder.QueryBuilder.AddToResultset(words[nameof(FtsObjectEntity.ObjectType)]);
                    builder.QueryBuilder.AddToResultset(words[nameof(FtsObjectEntity.Sorter)]);
                    builder.QueryBuilder.AddOrderBy(words[nameof(FtsObjectEntity.ObjectType)]);
                    builder.QueryBuilder.AddOrderBy(words[nameof(FtsObjectEntity.Sorter)]);
                    builder.QueryBuilder.Limit = limit;
                    builder.QueryBuilder.Skip = skip;
                    break;
                case FtsSelectQueryBuilder.QueryType.ObjectIds:
                    builder.QueryBuilder.Distinct = true;
                    builder.QueryBuilder.AddToResultset(words[nameof(FtsObjectEntity.ObjectID)]);
                    builder.QueryBuilder.Limit = limit;
                    builder.QueryBuilder.Skip = skip;
                    break;
                case FtsSelectQueryBuilder.QueryType.ObjectIdsAsInt:
                {
                    builder.QueryBuilder.Distinct = true;
                    builder.QueryBuilder.AddExpressionToResultset(connection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.ToInteger, new string[] {words[nameof(FtsObjectEntity.ObjectID)].Name}), DbType.Int32, false, "id");
                    builder.QueryBuilder.Limit = limit;
                    builder.QueryBuilder.Skip = skip;
                }
                    break;
                case FtsSelectQueryBuilder.QueryType.ObjectIdsAndTypes:
                    builder.QueryBuilder.Distinct = true;
                    builder.QueryBuilder.AddToResultset(words[nameof(FtsObjectEntity.ObjectID)]);
                    builder.QueryBuilder.AddToResultset(words[nameof(FtsObjectEntity.ObjectType)]);
                    builder.QueryBuilder.Limit = limit;
                    builder.QueryBuilder.Skip = skip;
                    break;
                case FtsSelectQueryBuilder.QueryType.Count:
                    builder.QueryBuilder.AddToResultset(AggFn.Count);
                    break;
            }
            FtsAddWhereToQuery(connection, builder, StringUtils.ParseToWords(text.ToUpper(), useMask), allWords, types);
            return builder;
        }

        private static async Task<int> FtsCountObjectsCore(this SqlDbConnection connection, bool sync, string text, bool allWords, string[] types, CancellationToken? token)
        {
            FtsSelectQueryBuilder builder = FtsBuildQuery(connection, FtsSelectQueryBuilder.QueryType.Count, text, true, allWords, types, 0, 0);
            using (SqlDbQuery query = connection.GetQuery(builder))
            {
                builder.BindTo(query);
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        return query.GetValue<int>(0);
                    return 0;

                }
                else
                {
                    await query.ExecuteReaderAsync(token);
                    while (await query.ReadNextAsync(token))
                        return query.GetValue<int>(0);
                    return 0;

                }
            }
        }


        public static int FtsCountObjects(this SqlDbConnection connection, string text, bool allWords = false, string[] types = null) => FtsCountObjectsCore(connection, true, text, allWords, types, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public static Task<int> FtsCountObjectsAsync(this SqlDbConnection connection, string text, bool allWords = false, string[] types = null, CancellationToken? token = null) => FtsCountObjectsCore(connection, false, text, allWords, types, null);

        private static async Task<FtsObjectEntityCollection> FtsGetObjectsCore(this SqlDbConnection connection, bool sync, string text, bool allWords, string[] types, int limit, int skip, CancellationToken? token)
        {
            FtsSelectQueryBuilder builder = FtsBuildQuery(connection, FtsSelectQueryBuilder.QueryType.ObjectList, text, true, allWords, types, limit, skip);
            using (SqlDbQuery query = connection.GetQuery(builder))
            {
                builder.BindTo(query);
                if (sync)
                    query.ExecuteReader();
                else
                    await query.ExecuteReaderAsync(token);

                FtsObjectEntityCollection collection = new FtsObjectEntityCollection();
                SelectQueryTypeBinder binder = new SelectQueryTypeBinder(typeof(FtsObjectEntity));
                foreach (TableDescriptor.ColumnInfo ci in AllEntities.Inst[typeof(FtsObjectEntity)].TableDescriptor)
                    binder.AddBinding(ci.Name, ci.PropertyAccessor, ci.PrimaryKey);

                if (sync)
                {
                    while (query.ReadNext())
                        collection.Add(binder.Read<FtsObjectEntity>(query));
                }
                else
                {
                    while (await query.ReadNextAsync(token))
                        collection.Add(binder.Read<FtsObjectEntity>(query));
                }

                return collection;
            }
        }

        public static FtsObjectEntityCollection FtsGetObjects(this SqlDbConnection connection, string text, bool allWords = false, string[] types = null, int limit = 0, int skip = 0)
            => connection.FtsGetObjectsCore(true, text, allWords, types, limit, skip, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public static Task<FtsObjectEntityCollection> FtsGetObjectsAsync(this SqlDbConnection connection, string text, bool allWords = false, string[] types = null, int limit = 0, int skip = 0, CancellationToken? token = null)
            => connection.FtsGetObjectsCore(false, text, allWords, types, limit, skip, token);

        private static async Task<FtsWordEntityCollection> FtsGetWordsCore(this SqlDbConnection connection, bool sync, string mask, int limit, int skip, CancellationToken? token)
        {
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(FtsWordEntity)))
            {
                if (mask != null)
                {
                    query.Where.Add().Property(nameof(FtsWordEntity.Word))
                        .Is(mask.Contains("%") ? CmpOp.Like : CmpOp.Eq)
                        .Value(mask.ToUpper());
                }

                query.Limit = limit;
                query.Skip = skip;
                query.AddOrderBy(nameof(FtsWordEntity.Word));
                if (sync)
                    return query.ReadAll<FtsWordEntityCollection, FtsWordEntity>();
                else
                    return await query.ReadAllAsync<FtsWordEntityCollection, FtsWordEntity>(null, token);
            }
        }

        public static FtsWordEntityCollection FtsGetWords(this SqlDbConnection connection, string mask, int limit, int skip)
            => connection.FtsGetWordsCore(true, mask, limit, skip, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public static Task<FtsWordEntityCollection> FtsGetWordsAsync(this SqlDbConnection connection, string mask, int limit, int skip, CancellationToken? token = null)
            => connection.FtsGetWordsCore(true, mask, limit, skip, token);
    }

    public class FtsSelectQueryBuilder : AQueryBuilder
    {
        public enum QueryType
        {
            ObjectList,
            ObjectIds,
            ObjectIdsAsInt,
            ObjectIdsAndTypes,
            Count,
        }

        public class QueryParameter
        {
            public string Name { get; internal set; }
            public object Value { get; internal set; }
            public Type Type { get; internal set; }

        }

        public SelectQueryBuilder QueryBuilder { get; internal set; }
        public QueryParameter[] Params { get; internal set; }

        public FtsSelectQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        public override void PrepareQuery() => QueryBuilder.PrepareQuery();

        public override string Query => QueryBuilder.Query;

        public void BindTo(SqlDbQuery query)
        {
            foreach (QueryParameter param in Params)
                query.BindParam(param.Name, ParameterDirection.Input, param.Value, param.Type);
        }
    }

    public static class FtsQueryExtension
    {
        public enum QueryType
        {
            AnyWordInclude,
            AllWordsInclude,
            AnyWordExclude,
            AllWordsExclude,
        }

        public static void AddFtsSearch(this EntityQueryConditionBuilder condition, string text, QueryType queryType, string ftsType, int? limit = null)
        {
            TableDescriptor.ColumnInfo primaryKey = condition.BaseQuery.Builder.Descriptor.PrimaryKey;
            FtsSelectQueryBuilder.QueryType subqueryType;
            if (primaryKey.DbType == DbType.Int32)
                subqueryType = FtsSelectQueryBuilder.QueryType.ObjectIdsAsInt;
            else
                subqueryType = FtsSelectQueryBuilder.QueryType.ObjectIds;

            FtsSelectQueryBuilder subquery = condition.BaseQuery.Query.Connection.FtsBuildQuery(subqueryType, text, 
                text.Contains("%"), queryType == QueryType.AllWordsExclude || queryType == QueryType.AllWordsInclude, 
                new string[] {ftsType}, limit ?? 0, 0);

            foreach (FtsSelectQueryBuilder.QueryParameter p in subquery.Params)
                condition.BaseQuery.BindParam(p.Name, ParameterDirection.Input, p.Value, p.Type);

            string leftSide = condition.BaseQuery.ConditionQueryBuilder.Alias(primaryKey.ID, out DbType _);
            condition.Add().Raw(leftSide).Is(queryType == QueryType.AnyWordInclude || queryType == QueryType.AllWordsInclude ? CmpOp.In : CmpOp.NotIn).Query(subquery);
        }
    }
}
