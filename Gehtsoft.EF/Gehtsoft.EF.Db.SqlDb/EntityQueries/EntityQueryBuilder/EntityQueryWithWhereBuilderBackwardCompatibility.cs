using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal static class EntityQueryWithWhereBuilderBackwardCompatibility
    {
        [Obsolete("Use Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, string propertyName, CmpOp op, string parameterName)
            => AddWhereFilter(builder, LogOp.And, propertyName, op, parameterName);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, string propertyName, CmpOp op, string parameterName)
            => AddWhereFilter(builder, logOp, type, 0, propertyName, op, parameterName);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, int occurrence, string propertyName, CmpOp op, string parameterName)
            => AddWhereFilter(builder, LogOp.And, type, occurrence, propertyName, op, parameterName);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op, string parameterName)
            => builder.Where.Add(logOp, builder.Where.PropertyOfName(propertyName, type, occurrence), op, (parameterName?.Contains(".") ?? true) ? parameterName : builder.Where.Parameter(parameterName));

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, string propertyName, CmpOp op, string parameterName)
            => AddWhereFilter(builder, LogOp.And, type, propertyName, op, parameterName);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, string propertyName, CmpOp op, string parameterName)
            => builder.Where.Add(logOp, builder.Where.PropertyName(propertyName), op, (parameterName?.Contains(".") ?? true) ? parameterName : builder.Where.Parameter(parameterName));

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, string propertyName, CmpOp op, string[] parameterNames)
            => AddWhereFilter(builder, LogOp.And, propertyName, op, parameterNames);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, string propertyName, CmpOp op, string[] parameterNames)
            => AddWhereFilter(builder, logOp, type, 0, propertyName, op, parameterNames);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op, string[] parameterNames)
            => builder.Where.Add(logOp, builder.Where.PropertyOfName(propertyName, type, occurrence), op, builder.Where.Parameters(parameterNames));

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, string propertyName, CmpOp op, string[] parameterNames)
            => AddWhereFilter(builder, LogOp.And, type, 0, propertyName, op, parameterNames);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, int occurrence, string propertyName, CmpOp op, string[] parameterNames)
            => AddWhereFilter(builder, LogOp.And, type, occurrence, propertyName, op, parameterNames);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, string propertyName, CmpOp op, string[] parameterNames)
            => builder.Where.Add(logOp, builder.Where.PropertyName(propertyName), op, builder.Where.Parameters(parameterNames));

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, string propertyName, CmpOp op)
            => AddWhereFilter(builder, logOp, type, 0, propertyName, op);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op)
            => builder.Where.Add(logOp, builder.Where.PropertyOfName(propertyName, type, occurrence), op, null);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, string propertyName, CmpOp op)
            => AddWhereFilter(builder, LogOp.And, type, propertyName, op);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, int occurrence, string propertyName, CmpOp op)
            => AddWhereFilter(builder, LogOp.And, type, occurrence, propertyName, op);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, string propertyName, CmpOp op)
            => builder.Where.Add(logOp, builder.Where.PropertyName(propertyName), op, null);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, string propertyName, CmpOp op)
            => AddWhereFilter(builder, LogOp.And, propertyName, op);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, string propertyName, CmpOp op, AQueryBuilder subquery)
            => AddWhereFilter(builder, logOp, type, 0, propertyName, op, subquery);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op, AQueryBuilder subquery)
            => builder.Where.Add(logOp, builder.Where.PropertyOfName(propertyName, type, occurrence), op, builder.Where.Query(subquery));

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, string propertyName, CmpOp op, AQueryBuilder subquery)
            => AddWhereFilter(builder, LogOp.And, type, propertyName, op, subquery);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, Type type, int occurrence, string propertyName, CmpOp op, AQueryBuilder subquery)
            => AddWhereFilter(builder, LogOp.And, type, occurrence, propertyName, op, subquery);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, LogOp logOp, string propertyName, CmpOp op, AQueryBuilder subquery)
            => builder.Where.Add(logOp, builder.Where.PropertyName(propertyName), op, builder.Where.Query(subquery));

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereFilter(this EntityQueryWithWhereBuilder builder, string propertyName, CmpOp op, AQueryBuilder subquery)
            => AddWhereFilter(builder, LogOp.And, propertyName, op, subquery);

        [Obsolete("User Where property of the entity query builder instead")]
        public static OpBracket AddWhereGroup(this EntityQueryWithWhereBuilder builder, LogOp logOp = Entities.LogOp.And)
            => builder.Where.AddGroup(logOp);

        [Obsolete("User Where property of the entity query builder instead")]
        public static void AddWhereExpression(this EntityQueryWithWhereBuilder builder, LogOp op, string expression)
            => builder.Where.Add(op, expression);
    }
}