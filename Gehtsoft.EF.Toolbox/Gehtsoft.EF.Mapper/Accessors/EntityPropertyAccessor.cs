using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Mapper
{
    public class EntityPropertyAccessor : IMappingSource, IMappingTarget
    {
        private readonly TableDescriptor.ColumnInfo mColumnInfo;

        public TableDescriptor.ColumnInfo ColumnInfo => mColumnInfo;

        public EntityPropertyAccessor(TableDescriptor.ColumnInfo columnInfo)
        {
            mColumnInfo = columnInfo;
        }

        public string Name => mColumnInfo.PropertyAccessor.Name;
        public Type ValueType => mColumnInfo.PropertyAccessor.PropertyType;
        public void Set(object obj, object value) => mColumnInfo.PropertyAccessor.SetValue(obj, value);
        public object Get(object obj) => mColumnInfo.PropertyAccessor.GetValue(obj);

        protected bool Equals(EntityPropertyAccessor other)
        {
            return ReferenceEquals(mColumnInfo, other.mColumnInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EntityPropertyAccessor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                if (mColumnInfo == null)
                    return 0;
                return mColumnInfo.Name.GetHashCode() ^ mColumnInfo.Table.Name.GetHashCode() * 397;     
            }           
        }

        public bool Equals(IMappingTarget target) => Equals((object) target);
    }
}