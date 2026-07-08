using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Extension methods that start a condition on a value inside a JSON property of an entity.
    /// </summary>
    public static class JsonPropertyConditionBuilderExtension
    {
        /// <summary>
        /// Starts a condition on a value at a JSON path inside a JSON property, connected with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="propertyPath">The name of the JSON property.</param>
        /// <param name="jsonPath">The JSON path to the value, for example <c>"$.age"</c>.</param>
        /// <param name="type">The primitive type of the value at the path.</param>
        public static SingleEntityQueryConditionBuilder JsonProperty(this EntityQueryConditionBuilder builder, string propertyPath, string jsonPath, DbType type)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.JsonProperty(propertyPath, jsonPath, type);
            return rc;
        }

        /// <summary>
        /// Starts a condition on a value at a JSON path inside a JSON property of the specified
        /// occurrence of the specified type, connected with logical and.
        /// </summary>
        public static SingleEntityQueryConditionBuilder JsonPropertyOf(this EntityQueryConditionBuilder builder, string name, string jsonPath, DbType type, Type entityType = null, int occurrence = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.JsonPropertyOf(name, jsonPath, type, entityType, occurrence);
            return rc;
        }

        /// <summary>
        /// Starts a condition on a value at a JSON path inside a JSON property of the specified entity
        /// type, connected with logical and (generic version).
        /// </summary>
        public static SingleEntityQueryConditionBuilder JsonPropertyOf<T>(this EntityQueryConditionBuilder builder, string name, string jsonPath, DbType type, int occurrence = 0)
            => builder.JsonPropertyOf(name, jsonPath, type, typeof(T), occurrence);

        /// <summary>
        /// Starts a condition on a value inside a JSON property addressed by a member/array-index
        /// expression such as <c>e =&gt; e.Profile.Age</c> or <c>e =&gt; e.Data.ChildrenAge[0]</c>.
        /// The first member after the parameter is the JSON property, the remaining members and
        /// indexers form the JSON path, and the value type is taken from the leaf.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="expression">The member/array-index expression.</param>
        /// <param name="type">
        /// The value type to extract the value as, overriding the type inferred from the leaf.
        /// Use it to filter, for example, a <c>DateTime</c> stored as an ISO-8601 string as
        /// <see cref="DbType.String"/>.
        /// </param>
        public static SingleEntityQueryConditionBuilder JsonPropertyOf<T>(this EntityQueryConditionBuilder builder, Expression<Func<T, object>> expression, DbType? type = null)
        {
            JsonExpressionParser.Parse(expression, out string propertyName, out string jsonPath, out Type valueType);
            DbType dbType;
            if (type.HasValue)
            {
                dbType = type.Value;
            }
            else
            {
                SqlDbLanguageSpecifics specifics = builder.BaseQuery.Where.BaseWhere.ConditionBuilder.InfoProvider.Specifics;
                if (!specifics.TypeToDb(valueType, out dbType))
                    throw new ArgumentException($"The JSON value type {valueType.Name} is not supported", nameof(expression));
            }
            return builder.JsonPropertyOf(propertyName, jsonPath, dbType, typeof(T), 0);
        }
    }
}
