﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoSelectQuery : MongoSelectQueryBase
    {
        private readonly bool mExpandExternal = false;
        private List<Tuple<string, bool>> mResultSet = null;

        internal MongoSelectQuery(MongoConnection connection, Type entityType, bool expandExternalReferences) : base(connection, entityType)
        {
            mExpandExternal = expandExternalReferences;
        }

        public int Skip { get; set; }
        public int Limit { get; set; }

        private SortDefinition<BsonDocument> mSort = null;

        public void AddToResultset(string path)
        {
            if (mResultSet == null)
                mResultSet = new List<Tuple<string, bool>>();
            mResultSet.Add(new Tuple<string, bool>(TranslatePath(path), true));
        }

        public void ExcludeFromResultset(string path)
        {
            if (mResultSet == null)
                mResultSet = new List<Tuple<string, bool>>();
            mResultSet.Add(new Tuple<string, bool>(TranslatePath(path), false));
        }

        public void AddOrderBy(string property, SortDir direction = SortDir.Asc)
        {
            FieldDefinition<BsonDocument> field = TranslatePath(property);

            if (mSort == null)
            {
                SortDefinitionBuilder<BsonDocument> builder = new SortDefinitionBuilder<BsonDocument>();

                if (direction == SortDir.Asc)
                    mSort = builder.Ascending(field);
                else
                    mSort = builder.Descending(field);
            }
            else
            {
                if (direction == SortDir.Asc)
                    mSort = mSort.Ascending(field);
                else
                    mSort = mSort.Descending(field);
            }
        }

        private async Task ExecuteAsyncCore(CancellationToken token)
        {
            ResultSet = null;

            FilterDefinition<BsonDocument> filter = FilterBuilder.ToBsonDocument();

            if (mExpandExternal)
            {
                throw new NotImplementedException();
            }
            else
            {
                FindOptions<BsonDocument> options = new FindOptions<BsonDocument>();

                if (Skip > 0 || Limit > 0)
                {
                    options.Limit = Limit;
                    options.Skip = Skip;
                }

                if (mSort != null)
                    options.Sort = mSort;

                if (mResultSet != null)
                {
                    ProjectionDefinition<BsonDocument> projection = null;

                    foreach (Tuple<string, bool> v in mResultSet)
                    {
                        if (projection == null)
                            projection = v.Item2 ? Builders<BsonDocument>.Projection.Include(v.Item1) : Builders<BsonDocument>.Projection.Exclude(v.Item1);
                        else
                            projection = v.Item2 ? projection.Include(v.Item1) : projection.Exclude(v.Item1);
                    }
                    options.Projection = projection;
                }

                using (IAsyncCursor<BsonDocument> cursor = await Collection.FindAsync(filter, options, token))
                {
                    List<BsonDocument> rs = new List<BsonDocument>();
                    while (await cursor.MoveNextAsync(token))
                    {
                        IEnumerable<BsonDocument> batch = cursor.Current;
                        rs.AddRange(batch);
                    }
                    ResultSet = rs;
                }
            }
        }

        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }

        public EntityCollection<T> ReadAll<T>() where T : class => ReadAll<EntityCollection<T>, T>();

        public TC ReadAll<TC, T>() where TC : EntityCollection<T>, new() where T : class
        {
            if (ResultSet == null)
                Execute();

            TC coll = new TC();
            while (ReadNext())
                coll.Add(GetEntity<T>());
            return coll;
        }

        public override Task ExecuteAsync(CancellationToken? token = null) => ExecuteAsyncCore(token ?? CancellationToken.None);
    }
}
