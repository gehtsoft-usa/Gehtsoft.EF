namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The extensions for <see cref="IContextFilterCondition"/> class.
    /// </summary>
    public static class EntityFilterConditionExtension
    {
        /// <summary>
        /// Adds an "equals to" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition Eq(this IContextFilterCondition condition) => condition.Is(CmpOp.Eq);

        /// <summary>
        /// Adds a "not equals to" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition Neq(this IContextFilterCondition condition) => condition.Is(CmpOp.Neq);

        /// <summary>
        /// Adds a "greater than" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition Gt(this IContextFilterCondition condition) => condition.Is(CmpOp.Gt);

        /// <summary>
        /// Adds a "greater than or equals to" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition Ge(this IContextFilterCondition condition) => condition.Is(CmpOp.Ge);

        /// <summary>
        /// Adds a "less than" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition Ls(this IContextFilterCondition condition) => condition.Is(CmpOp.Ls);

        /// <summary>
        /// Adds a "less than or equals to" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition Le(this IContextFilterCondition condition) => condition.Is(CmpOp.Le);

        /// <summary>
        /// Adds a "is like" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition Like(this IContextFilterCondition condition) => condition.Is(CmpOp.Like);

        /// <summary>
        /// Adds an "is null" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition IsNull(this IContextFilterCondition condition) => condition.Is(CmpOp.IsNull);

        /// <summary>
        /// Adds an "is not null" comparison to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static IContextFilterCondition NotNull(this IContextFilterCondition condition) => condition.Is(CmpOp.NotNull);

        /// <summary>
        /// Adds an "equals to" comparison with the value specified to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IContextFilterCondition Eq(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Eq).Value(value);

        /// <summary>
        /// Adds a "not equals to" comparison with the value specified to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IContextFilterCondition Neq(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Neq).Value(value);

        /// <summary>
        /// Adds a "greater than" comparison with the value specified to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IContextFilterCondition Gt(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Gt).Value(value);

        /// <summary>
        /// Adds a "greater than or equals to" comparison with the value specified to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IContextFilterCondition Ge(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Ge).Value(value);

        /// <summary>
        /// Adds a "less than" comparison with the value specified to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IContextFilterCondition Ls(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Ls).Value(value);

        /// <summary>
        /// Adds a "less than or equals to" comparison with the value specified to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IContextFilterCondition Le(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Le).Value(value);

        /// <summary>
        /// Adds an "is like" comparison with the value specified to the condition.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IContextFilterCondition Like(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Like).Value(value);
    }
}