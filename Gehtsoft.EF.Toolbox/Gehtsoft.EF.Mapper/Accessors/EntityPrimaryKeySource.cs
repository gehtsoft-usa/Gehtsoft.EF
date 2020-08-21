using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Mapper
{
    public class EntityPrimaryKeySource : IMappingSource
    {
        private TableDescriptor.ColumnInfo mForeignKeyAccessor;
        private TableDescriptor.ColumnInfo mPrimaryKey;

        public EntityPrimaryKeySource(TableDescriptor.ColumnInfo foreignKeyAccessor)
        {
            mForeignKeyAccessor = foreignKeyAccessor;
            mPrimaryKey = foreignKeyAccessor.ForeignTable.PrimaryKey;

        }

        public Type ValueType => mPrimaryKey.PropertyAccessor.PropertyType;
        public string Name => mForeignKeyAccessor.PropertyAccessor.Name;
        
        public object Get(object obj)
        {
            if (obj == null)
                return null;

            object refObj = mForeignKeyAccessor.PropertyAccessor.GetValue(obj);

            if (refObj == null)
                return null;

            return mPrimaryKey.PropertyAccessor.GetValue(refObj);
        }
    }


}