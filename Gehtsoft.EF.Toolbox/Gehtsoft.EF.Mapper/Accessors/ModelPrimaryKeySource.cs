using System;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Mapper
{
    public class ModelPrimaryKeySource : IMappingSource
    {
        private readonly TableDescriptor.ColumnInfo mForeignKeyAccessor;
        private readonly TableDescriptor.ColumnInfo mPrimaryKey;
        private readonly PropertyInfo mPrimaryKeySource;


        public ModelPrimaryKeySource(TableDescriptor.ColumnInfo foreignKeyAccessor, PropertyInfo primaryKeySource)
        {
            mForeignKeyAccessor = foreignKeyAccessor;
            mPrimaryKey = foreignKeyAccessor.ForeignTable.PrimaryKey;
            mPrimaryKeySource = primaryKeySource;
        }

        public Type ValueType => mForeignKeyAccessor.PropertyAccessor.PropertyType;
        public string Name => mPrimaryKeySource.Name;


        public object Get(object obj)
        {
            if (obj == null)
                return null;
            object pkValue = mPrimaryKeySource.GetValue(obj);
            if (pkValue == null)
                return null;

            object refObject = Activator.CreateInstance(mForeignKeyAccessor.PropertyAccessor.PropertyType);
            mPrimaryKey.PropertyAccessor.SetValue(refObject, pkValue);
            return refObject;
        }
    }
}