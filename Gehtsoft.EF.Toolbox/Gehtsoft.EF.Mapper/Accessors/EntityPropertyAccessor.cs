using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Mapper
{
    public sealed class EntityPropertyAccessor : IMappingSource, IMappingTarget
    {
        public TableDescriptor.ColumnInfo ColumnInfo { get; }

        public EntityPropertyAccessor(TableDescriptor.ColumnInfo columnInfo)
        {
            ColumnInfo = columnInfo;
        }

        public string Name => ColumnInfo.PropertyAccessor.Name;
        public Type ValueType => ColumnInfo.PropertyAccessor.PropertyType;
        public void Set(object obj, object value) => ColumnInfo.PropertyAccessor.SetValue(obj, value);
        public object Get(object obj) => ColumnInfo.PropertyAccessor.GetValue(obj);

        public override bool Equals(object obj) => obj is IMappingTarget target && Equals(target);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Name?.GetHashCode() ?? 0) ^ (ValueType?.GetHashCode() ?? 0) * 397;
            }
        }

        // Two mapping targets denote the same destination member when they address the same
        // property (name + type), regardless of the concrete accessor implementation. This lets
        // ContainsRuleFor/Find — which build a ClassPropertyAccessor from the property name — match
        // rules whose stored target is an EntityPropertyAccessor (e.g. those produced by
        // EntityMapInitializer.ModelToSource). See KNOWN_BUGS.md.
        public bool Equals(IMappingTarget target)
        {
            if (target is null) return false;
            if (ReferenceEquals(this, target)) return true;
            return Name == target.Name && ValueType == target.ValueType;
        }
    }
}