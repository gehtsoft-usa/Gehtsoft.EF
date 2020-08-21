﻿using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntityCountQueryBuilder : SelectEntityQueryBuilderBase
    {
        public SelectEntityCountQueryBuilder(Type type, SqlDbConnection connection) : base(type, connection)
        {
            AddEntitiesTree();
            AddToResultset(AggFn.Count, null);
        }
    }
}