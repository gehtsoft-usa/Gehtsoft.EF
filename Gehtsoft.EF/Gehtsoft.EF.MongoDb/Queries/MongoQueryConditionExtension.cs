using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    public static class MongoQueryConditionExtension
    {
        public static MongoQuerySingleConditionBuilder And(this MongoQueryCondition where) => where.Add(LogOp.And);

        public static MongoQueryCondition And(this MongoQueryCondition where, Action<MongoQueryCondition> action)
        {
            where.AddGroup(LogOp.And, action);
            return where;
        }

        public static MongoQuerySingleConditionBuilder Or(this MongoQueryCondition where) => where.Add(LogOp.Or);

        public static MongoQueryCondition Or(this MongoQueryCondition where, Action<MongoQueryCondition> action)
        {
            where.AddGroup(LogOp.Or, action);
            return where;
        }

        public static MongoQuerySingleConditionBuilder Property(this MongoQueryCondition where, string property) => where.Add(LogOp.And).Property(property);
    }
}
