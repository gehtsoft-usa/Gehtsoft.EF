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

        private bool Equals(EntityPropertyAccessor other)
        {
            return ReferenceEquals(ColumnInfo, other.ColumnInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EntityPropertyAccessor)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                if (ColumnInfo == null)
                    return 0;
                return ColumnInfo.Name.GetHashCode() ^ ColumnInfo.Table.Name.GetHashCode() * 397;
            }
        }

        public bool Equals(IMappingTarget target) => Equals((object)target);
    }
}