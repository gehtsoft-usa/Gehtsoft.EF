using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Serialization.IO.Db
{
    public class DbEntityReader : IEntityReader
    {
        public int FrameSize { get; set; } = 1000;

        private readonly SqlDbConnection mConnection;
        private readonly EntityFinder.EntityTypeInfo[] mEntityTypes;

        public DbEntityReader(EntityFinder.EntityTypeInfo[] types, SqlDbConnection connection, CancellationToken? cancellationToken)
        {
            EntityFinder.ArrageEntities(types);
            mEntityTypes = types;
            mConnection = connection;
            mCancellationToken = cancellationToken;
        }

        private CancellationToken? mCancellationToken;

        public event TypeStartedDelegate OnTypeStarted;
        public event EntityDelegate OnEntity;

        public void Scan()
        {
            foreach (EntityFinder.EntityTypeInfo type in mEntityTypes)
            {
                HandleType(type.EntityType);
                if (mCancellationToken != null && ((CancellationToken)mCancellationToken).IsCancellationRequested)
                    return;
            }
        }

        protected virtual void HandleType(Type type)
        {
            EntityDescriptor descriptor = AllEntities.Inst[type];
            TableDescriptor.ColumnInfo selfReference = null;
            foreach (TableDescriptor.ColumnInfo column in descriptor.TableDescriptor)
            {
                if (column.ForeignKey && object.ReferenceEquals(column.ForeignTable, descriptor.TableDescriptor))
                {
                    if (selfReference != null)
                        throw new InvalidOperationException($"The entity type {type.Name} has more than one self reference. Such scenario is not supported by the default implementation.");
                    selfReference = column;
                }
            }

            if (selfReference == null)
                ProcessRegularEntity(descriptor);
            else
                ProcessSelfReferencedEntity(descriptor, selfReference, null);
        }

        protected void ProcessRegularEntity(EntityDescriptor entityDescriptor)
        {
            OnTypeStarted?.Invoke(entityDescriptor.EntityType);
            int skip = 0;
            while (true)
            {
                using (SelectEntitiesQuery query = mConnection.GetSelectEntitiesQuery(entityDescriptor.EntityType))
                {
                    query.AddOrderBy(entityDescriptor.PrimaryKey.ID);

                    query.Skip = skip;
                    query.Limit = FrameSize;
                    query.Execute();
                    int count = 0;
                    while (true)
                    {
                        object entity = query.ReadOne();
                        if (entity == null)
                            break;
                        OnEntity?.Invoke(entity);
                        if (mCancellationToken != null && ((CancellationToken)mCancellationToken).IsCancellationRequested)
                            return;
                        count++;
                    }

                    if (count < FrameSize)
                        return;

                    skip += count;
                }
            }
        }

        protected void ProcessSelfReferencedEntity(EntityDescriptor entityDescriptor, TableDescriptor.ColumnInfo selftReference, object parent = null)
        {
            if (parent == null)
                OnTypeStarted?.Invoke(entityDescriptor.EntityType);

            List<object> result = new List<object>();

            using (SelectEntitiesQuery query = mConnection.GetSelectEntitiesQuery(entityDescriptor.EntityType))
            {
                if (parent == null)
                    query.Where.Property(selftReference.ID).IsNull();
                else
                    query.Where.Property(selftReference.ID).Eq(parent);

                query.AddOrderBy(entityDescriptor.PrimaryKey.ID);
                query.Execute();
                while (true)
                {
                    object entity = query.ReadOne();
                    if (entity == null)
                        break;
                    result.Add(entity);
                }
            }

            foreach (object o in result)
            {
                OnEntity?.Invoke(o);
                if (mCancellationToken != null && ((CancellationToken)mCancellationToken).IsCancellationRequested)
                    return;
            }

            foreach (object o in result)
            {
                ProcessSelfReferencedEntity(entityDescriptor, selftReference, entityDescriptor.PrimaryKey.PropertyAccessor.GetValue(o));
                if (mCancellationToken != null && ((CancellationToken)mCancellationToken).IsCancellationRequested)
                    return;
            }
        }
    }
}


