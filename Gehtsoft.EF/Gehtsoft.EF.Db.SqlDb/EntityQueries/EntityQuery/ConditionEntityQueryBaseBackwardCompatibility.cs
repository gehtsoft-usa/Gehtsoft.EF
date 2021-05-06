using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public static class ConditionEntityQueryBaseBackwardCompatibility
    {
        [Obsolete("Use Where property of the entity query instead")]
        public static string AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, string propertyName, CmpOp op, object parameter)
            => query.AddWhereFilter(logOp, type, 0, propertyName, op, parameter);

        [Obsolete("Use Where property of the entity query instead")]
        public static string AddWhereFilter(this ConditionEntityQueryBase query, Type type, int occurrence, string propertyName, CmpOp op, object parameter)
            => query.AddWhereFilter(LogOp.And, type, occurrence, propertyName, op, parameter);

        [Obsolete("Use Where property of the entity query instead")]
        public static string AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op, object parameter)
        {
            var c = query.Where.Add(logOp).PropertyOf(propertyName, type, occurrence).Is(op);

            if (parameter is ConditionEntityQueryBase.InQueryName name)
                c = c.Reference(name);
            else
                c = c.Value(parameter);

            return c.ParameterName;
        }

        [Obsolete("Use Where property of the entity query instead")]
        public static string AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, string propertyName, CmpOp op, object parameter)
        {
            var c = query.Where.Add(logOp).Property(propertyName).Is(op);

            if (parameter is ConditionEntityQueryBase.InQueryName name)
                c = c.Reference(name);
            else
                c = c.Value(parameter);

            return c.ParameterName;
        }

        [Obsolete("Use Where property of the entity query instead")]
        public static string[] AddWhereFilter(this ConditionEntityQueryBase query, Type type, string propertyName, CmpOp op, object[] parameters)
            => AddWhereFilter(query, LogOp.And, type, propertyName, op, parameters);
        [Obsolete("Use Where property of the entity query instead")]
        public static string[] AddWhereFilter(this ConditionEntityQueryBase query, Type type, int occurrence, string propertyName, CmpOp op, object[] parameters)
            => AddWhereFilter(query, LogOp.And, type, occurrence, propertyName, op, parameters);
        [Obsolete("Use Where property of the entity query instead")]
        public static string[] AddWhereFilter(this ConditionEntityQueryBase query, string propertyName, CmpOp op, object[] parameters)
            => AddWhereFilter(query, LogOp.And, propertyName, op, parameters);
        [Obsolete("Use Where property of the entity query instead")]
        public static string[] AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, string propertyName, CmpOp op, object[] parameters)
            => AddWhereFilter(query, logOp, type, 0, propertyName, op, parameters);

        [Obsolete("Use Where property of the entity query instead")]
        public static string[] AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op, object[] parameters)
            => query.Where.Add(logOp).PropertyOf(propertyName, type, occurrence).Is(op).Values(parameters).ParameterNames;

        [Obsolete("Use Where property of the entity query instead")]
        public static string[] AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, string propertyName, CmpOp op, object[] parameters)
            => AddWhereFilter(query, logOp, null, propertyName, op, parameters);

        [Obsolete("Use Where property of the entity query instead")]
        public static string AddWhereFilter(this ConditionEntityQueryBase query, Type type, string propertyName, CmpOp op, object parameter)
            => AddWhereFilter(query, LogOp.And, type, propertyName, op, parameter);

        [Obsolete("Use Where property of the entity query instead")]
        public static string AddWhereFilter(this ConditionEntityQueryBase query, string propertyName, CmpOp op, object parameter)
            => AddWhereFilter(query, LogOp.And, propertyName, op, parameter);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, string propertyName, CmpOp op)
            => query.Where.Add(logOp).PropertyOf(propertyName, type).Is(op);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op)
            => query.Where.Add(logOp).PropertyOf(propertyName, type, occurrence).Is(op);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, string propertyName, CmpOp op)
            => query.Where.Add(logOp).Property(propertyName).Is(op);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, string propertyName, CmpOp op, AQueryBuilder subquery)
            => query.Where.Add(logOp).PropertyOf(propertyName, type).Is(op).Query(subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op, AQueryBuilder subquery)
            => query.Where.Add(logOp).PropertyOf(propertyName, type, occurrence).Is(op).Query(subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, string propertyName, CmpOp op, AQueryBuilder subquery)
            => query.Where.Add(logOp).Property(propertyName).Is(op).Query(subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, Type type, string propertyName, CmpOp op, AQueryBuilder subquery)
            => AddWhereFilter(query, LogOp.And, type, propertyName, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, Type type, int occurrence, string propertyName, CmpOp op, AQueryBuilder subquery)
            => AddWhereFilter(query, LogOp.And, type, occurrence, propertyName, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, string propertyName, CmpOp op, AQueryBuilder subquery)
            => AddWhereFilter(query, LogOp.And, propertyName, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, Type type, string propertyName, CmpOp op)
            => AddWhereFilter(query, LogOp.And, type, propertyName, op);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, Type type, int occurrence, string propertyName, CmpOp op)
            => AddWhereFilter(query, LogOp.And, type, occurrence, propertyName, op);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, string propertyName, CmpOp op)
            => AddWhereFilter(query, LogOp.And, propertyName, op);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, string propertyName, CmpOp op, SelectEntitiesQueryBase subquery)
            => query.Where.Add(logOp).PropertyOf(propertyName, type).Is(op).Query(subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Type type, int occurrence, string propertyName, CmpOp op, SelectEntitiesQueryBase subquery)
            => query.Where.Add(logOp).PropertyOf(propertyName, type, occurrence).Is(op).Query(subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, string propertyName, CmpOp op, SelectEntitiesQueryBase subquery)
            => query.Where.Add(logOp).Property(propertyName).Is(op).Query(subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, Type type, string propertyName, CmpOp op, SelectEntitiesQueryBase subquery)
            => AddWhereFilter(query, LogOp.And, type, propertyName, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, Type type, int occurrence, string propertyName, CmpOp op, SelectEntitiesQueryBase subquery)
            => AddWhereFilter(query, LogOp.And, type, occurrence, propertyName, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, string propertyName, CmpOp op, SelectEntitiesQueryBase subquery)
            => AddWhereFilter(query, LogOp.And, propertyName, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, CmpOp op, SelectEntitiesQueryBase subquery)
            => AddWhereFilter(query, LogOp.And, null, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp lop, CmpOp op, SelectEntitiesQueryBase subquery)
            => AddWhereFilter(query, lop, null, op, subquery);

        [Obsolete("Use Where property of the entity query instead")]
        public static OpBracket AddWhereGroup(this ConditionEntityQueryBase query, LogOp logOp = LogOp.And)
            => query.Where.AddGroup(logOp);

        [Obsolete("Use Where property of the entity query instead")]
        public static void AddWhereExpression(this ConditionEntityQueryBase query, LogOp op, string expression) =>
            query.Where.Add(op, expression);
    }
}