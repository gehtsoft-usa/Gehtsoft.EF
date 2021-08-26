using System;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The extensions for <see cref="IContextFilter"/> class
    /// </summary>
    public static class EntityFilterBuilderExtension
    {
        /// <summary>
        /// Adds condition join with the previous conditions by Logical And
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IContextFilterCondition And(this IContextFilter builder) => builder.Add(LogOp.And);

        /// <summary>
        /// Adds an inverted condition join with the previous conditions by Logical And
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IContextFilterCondition AndNot(this IContextFilter builder) => builder.Add(LogOp.And | LogOp.Not);

        /// <summary>
        /// Adds condition join with the previous conditions by Logical Or
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IContextFilterCondition Or(this IContextFilter builder) => builder.Add(LogOp.Or);

        /// <summary>
        /// Adds an inverted condition join with the previous conditions by Logical And
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IContextFilterCondition OrNot(this IContextFilter builder) => builder.Add(LogOp.Or | LogOp.Not);

        /// <summary>
        /// Adds condition which starts with the property name, joined with the previous conditions with the logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IContextFilterCondition Property(this IContextFilter builder, string name) => builder.Add(LogOp.And).Property(name);

        /// <summary>
        /// Adds condition which checks whether the property is null, joined with the previous conditions with the logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IContextFilterCondition IsNull(this IContextFilter builder, string name) => builder.Add(LogOp.And).Is(CmpOp.IsNull).Property(name);

        /// <summary>
        /// Adds condition which starts with the property is not null, joined with the previous conditions with the logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IContextFilterCondition NotNull(this IContextFilter builder, string name) => builder.Add(LogOp.And).Is(CmpOp.NotNull).Property(name);

        /// <summary>
        /// Adds a condition with unary operator (e.g. `IsNull`), joined with the previous conditions with the logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="op"></param>
        /// <returns></returns>
        public static IContextFilterCondition Is(this IContextFilter builder, CmpOp op) => builder.Add(LogOp.And).Is(op);

        /// <summary>
        /// Adds a group defined by the action specified and joined by the logical operator specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="op"></param>
        /// <param name="action"></param>
        public static void AddGroup(this IContextFilter builder, LogOp op, Action<IContextFilter> action)
        {
            using (var g = builder.AddGroup(op))
                action(builder);
        }

        /// <summary>
        /// Adds a group defined by the action specified and joined by the logical and to the previous conditions.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        public static void And(this IContextFilter builder, Action<IContextFilter> action) => AddGroup(builder, LogOp.And, action);

        /// <summary>
        /// Adds a group defined by the action specified and joined by the logical or to the previous conditions.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        public static void Or(this IContextFilter builder, Action<IContextFilter> action) => AddGroup(builder, LogOp.Or, action);
    }
}