using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    public static class MongoQuerySingleConditionBuilderExtension
    {
        public static MongoQuerySingleConditionBuilder Eq(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Eq);
        public static MongoQuerySingleConditionBuilder Neq(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Neq);
        public static MongoQuerySingleConditionBuilder Ls(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Ls);
        public static MongoQuerySingleConditionBuilder Le(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Le);
        public static MongoQuerySingleConditionBuilder Gt(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Gt);
        public static MongoQuerySingleConditionBuilder Ge(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Ge);

        public static MongoQuerySingleConditionBuilder Eq(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Eq).Value(value);
        public static MongoQuerySingleConditionBuilder Neq(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Neq).Value(value);
        public static MongoQuerySingleConditionBuilder Ls(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Ls).Value(value);
        public static MongoQuerySingleConditionBuilder Le(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Le).Value(value);
        public static MongoQuerySingleConditionBuilder Gt(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Gt).Value(value);
        public static MongoQuerySingleConditionBuilder Ge(this MongoQuerySingleConditionBuilder builder, object value) => builder.Is(CmpOp.Ge).Value(value);

        public static MongoQuerySingleConditionBuilder Like(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.Like);
        public static MongoQuerySingleConditionBuilder Like(this MongoQuerySingleConditionBuilder builder, string mask) => builder.Is(CmpOp.Like).Value(mask);
        public static MongoQuerySingleConditionBuilder In(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.In);
        public static MongoQuerySingleConditionBuilder NotIn(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.NotIn);
        public static MongoQuerySingleConditionBuilder In(this MongoQuerySingleConditionBuilder builder, params object[] values) => builder.Is(CmpOp.In).Value(values);
        public static MongoQuerySingleConditionBuilder NotIn(this MongoQuerySingleConditionBuilder builder, params object[] values) => builder.Is(CmpOp.NotIn).Value(values);

        public static MongoQuerySingleConditionBuilder IsNull(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.IsNull);
        public static MongoQuerySingleConditionBuilder NotNull(this MongoQuerySingleConditionBuilder builder) => builder.Is(CmpOp.NotNull);
    }
}
