namespace Gehtsoft.EF.Entities.Context
{
    public static class EntityFilterBuilderExtension
    {
        public static IContextFilterCondition And(this IContextFilter builder) => builder.Add(LogOp.And);

        public static IContextFilterCondition Or(this IContextFilter builder) => builder.Add(LogOp.Or);

        public static IContextFilterCondition Property(this IContextFilter builder, string name) => builder.Add(LogOp.And).Property(name);

        public static IContextFilterCondition IsNull(this IContextFilter builder, string name) => builder.Add(LogOp.And).Is(CmpOp.IsNull).Property(name);

        public static IContextFilterCondition NotNull(this IContextFilter builder, string name) => builder.Add(LogOp.And).Is(CmpOp.NotNull).Property(name);

        public static IContextFilterCondition Is(this IContextFilter builder, CmpOp op) => builder.Add(LogOp.And).Is(op);

        public static IContextFilterCondition Eq(this IContextFilterCondition condition) => condition.Is(CmpOp.Eq);

        public static IContextFilterCondition Neq(this IContextFilterCondition condition) => condition.Is(CmpOp.Neq);

        public static IContextFilterCondition Gt(this IContextFilterCondition condition) => condition.Is(CmpOp.Gt);

        public static IContextFilterCondition Ge(this IContextFilterCondition condition) => condition.Is(CmpOp.Ge);

        public static IContextFilterCondition Ls(this IContextFilterCondition condition) => condition.Is(CmpOp.Ls);

        public static IContextFilterCondition Le(this IContextFilterCondition condition) => condition.Is(CmpOp.Le);

        public static IContextFilterCondition Like(this IContextFilterCondition condition) => condition.Is(CmpOp.Like);

        public static IContextFilterCondition IsNull(this IContextFilterCondition condition) => condition.Is(CmpOp.IsNull);

        public static IContextFilterCondition NotNull(this IContextFilterCondition condition) => condition.Is(CmpOp.NotNull);

        public static IContextFilterCondition Eq(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Eq).Value(value);

        public static IContextFilterCondition Neq(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Neq).Value(value);

        public static IContextFilterCondition Gt(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Gt).Value(value);

        public static IContextFilterCondition Ge(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Ge).Value(value);

        public static IContextFilterCondition Ls(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Ls).Value(value);

        public static IContextFilterCondition Le(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Le).Value(value);

        public static IContextFilterCondition Like(this IContextFilterCondition condition, object value) => condition.Is(CmpOp.Like).Value(value);
    }
}