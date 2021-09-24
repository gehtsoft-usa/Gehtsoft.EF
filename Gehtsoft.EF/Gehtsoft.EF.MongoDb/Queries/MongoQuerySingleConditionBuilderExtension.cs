using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The extension to simplify specifying of a single condition.
    ///
    /// See also <see cref="MongoQuerySingleConditionBuilder"/>.
    /// </summary>
    public static class MongoQuerySingleConditionBuilderExtension
    {
        /// <summary>
        /// Adds equals to comparison operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Eq(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Eq);

        /// <summary>
        /// Adds not equal to comparison operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Neq(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Neq);

        /// <summary>
        /// Adds less than comparison operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Ls(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Ls);

        /// <summary>
        /// Adds less than or equals to comparison operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Le(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Le);

        /// <summary>
        /// Adds greater than comparison operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Gt(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Gt);
        /// <summary>
        /// Adds greater than or equals to comparison operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Ge(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Ge);

        /// <summary>
        /// Adds equals to comparison operation with the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Eq(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Eq).Value(value);

        /// <summary>
        /// Adds not equal to comparison operation with the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Neq(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Neq).Value(value);

        /// <summary>
        /// Adds less than comparison operation with the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Ls(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Ls).Value(value);

        /// <summary>
        /// Adds less than or equals to comparison operation with the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Le(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Le).Value(value);

        /// <summary>
        /// Adds greater than comparison operation with the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Gt(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Gt).Value(value);

        /// <summary>
        /// Adds greater than or equal to comparison operation with the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Ge(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Ge).Value(value);

        /// <summary>
        /// Adds is like to operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Like(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Like);

        /// <summary>
        /// Adds is like to operation with the mask specified.
        ///
        /// See also <see cref="MongoQuerySingleConditionBuilder.Value"/> for details about the mask rules.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="mask"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Like(this MongoQuerySingleConditionBuilder builder, string mask) => builder.Is(CmpOp.Like).Value(mask);

        /// <summary>
        /// Adds in list operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder In(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.In);

        /// <summary>
        /// Adds not in list operation.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder NotIn(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.NotIn);

        /// <summary>
        /// Adds in list operation with the list values specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder In(this MongoQuerySingleConditionBuilder builder, params object[] values) => builder.Is(CmpOp.In).Value(values);

        /// <summary>
        /// Adds not in list operation with the list values specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder NotIn(this MongoQuerySingleConditionBuilder builder, params object[] values) => builder.Is(CmpOp.NotIn).Value(values);

        /// <summary>
        /// Adds check for null value.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder IsNull(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.IsNull);

        /// <summary>
        /// Adds check for is not null value.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder NotNull(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.NotNull);
    }
}
