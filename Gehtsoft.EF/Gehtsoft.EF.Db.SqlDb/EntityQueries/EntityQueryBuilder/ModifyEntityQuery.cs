using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class InsertEntityQueryBuilder : EntityQueryBuilder
    {
        protected UpdateQueryToTypeBinder mBinder;
        protected bool mIgnoreAutoIncrement;
        public UpdateQueryToTypeBinder Binder => mBinder;

        public bool IgnoreAutoIncrement => mIgnoreAutoIncrement;

        public InsertEntityQueryBuilder(Type type, SqlDbConnection connection, bool ignoreAutoIncrement) : base(connection.GetLanguageSpecifics(), type)
        {
            mIgnoreAutoIncrement = ignoreAutoIncrement;
            mQueryBuilder = connection.GetInsertQueryBuilder(mEntityDescriptor.TableDescriptor, ignoreAutoIncrement);
            mBinder = new UpdateQueryToTypeBinder(type);
            PrepareBinder();
        }

        protected void PrepareBinder()
        {
             foreach (TableDescriptor.ColumnInfo columnInfo in mEntityDescriptor.TableDescriptor)
                mBinder.AddBinding(columnInfo.Name, columnInfo.PropertyAccessor, columnInfo.ForeignKey ? columnInfo.ForeignTable.PrimaryKey.PropertyAccessor : null, columnInfo.DbType, columnInfo.Size, columnInfo.Autoincrement && columnInfo.PrimaryKey && !mIgnoreAutoIncrement);
        }
    }

    public class DeleteEntityQueryBuilder : EntityQueryWithWhereBuilder
    {
        protected UpdateQueryToTypeBinder mBinder;

        public UpdateQueryToTypeBinder Binder => mBinder;

        public DeleteEntityQueryBuilder(Type type, SqlDbConnection connection) : base(connection.GetLanguageSpecifics(), type)
        {
            mQueryBuilder = connection.GetDeleteQueryBuilder(mEntityDescriptor.TableDescriptor);
            SetBuilder(type, (QueryWithWhereBuilder)mQueryBuilder);
        }

        public void PrepareBinder()
        {
            mBinder = new UpdateQueryToTypeBinder(mEntityDescriptor.EntityType);

            DeleteQueryBuilder builder = (mQueryBuilder as DeleteQueryBuilder);

            if (builder.Where.IsEmpty)
                builder.DeleteById();

            TableDescriptor.ColumnInfo columnInfo = mEntityDescriptor.TableDescriptor.PrimaryKey;
            mBinder.AddBinding(columnInfo.Name, columnInfo.PropertyAccessor, columnInfo.DbType, columnInfo.Size, false);
        }
    }

    public class UpdateEntityQueryBuilder : EntityQueryWithWhereBuilder
    {
        protected UpdateQueryToTypeBinder mBinder;

        public UpdateQueryToTypeBinder Binder => mBinder;

        public UpdateEntityQueryBuilder(Type type, SqlDbConnection connection) : base(connection.GetLanguageSpecifics(), type)
        {
            mQueryBuilder = connection.GetUpdateQueryBuilder(mEntityDescriptor.TableDescriptor);
            SetBuilder(type, (QueryWithWhereBuilder)mQueryBuilder);
        }

        public void AddUpdateColumn(string propertyName)
        {
            TableDescriptor.ColumnInfo columnInfo = mEntityDescriptor.TableDescriptor[propertyName];
            UpdateQueryBuilder builder = (mQueryBuilder as UpdateQueryBuilder);
            builder.AddUpdateColumn(columnInfo, propertyName);
        }

        public void PrepareBinder()
        {
            mBinder = new UpdateQueryToTypeBinder(mEntityDescriptor.EntityType);

            UpdateQueryBuilder builder = (mQueryBuilder as UpdateQueryBuilder);
            if (builder.IsFieldsetEmpty)
                builder.AddUpdateAllColumns();
            if (builder.Where.IsEmpty)
                builder.UpdateById();
            foreach (TableDescriptor.ColumnInfo columnInfo in mEntityDescriptor.TableDescriptor)
                mBinder.AddBinding(columnInfo.Name, columnInfo.PropertyAccessor, columnInfo.ForeignKey ? columnInfo.ForeignTable.PrimaryKey.PropertyAccessor : null, columnInfo.DbType, columnInfo.Size, false);
        }
    }
}
