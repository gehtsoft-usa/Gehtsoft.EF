using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public sealed class AllEntities : IEnumerable<Type>
    {
        public event EventHandler<EntityDescriptorEventArgs> OnEntityDiscovered;

        public static EntityDescriptor Get(Type type, bool exceptionIsNotFound = true) => AllEntities.Inst[type, exceptionIsNotFound];
        public static EntityDescriptor Get<T>(bool exceptionIsNotFound = true) => AllEntities.Inst[typeof(T), exceptionIsNotFound];

        public EntityDescriptor[] All() => mEntities.Values.ToArray();

        public NamingPolicyManager NamingPolicy { get; } = new NamingPolicyManager();

        private readonly Dictionary<Type, EntityDescriptor> mEntities = new Dictionary<Type, EntityDescriptor>();

        private static AllEntities gEntities = null;

        private readonly object mMutex = new object();

        private readonly List<IEntityDisoverer> mDisoverer = new List<IEntityDisoverer>();

        public static AllEntities Inst
        {
            get
            {
                return gEntities ?? (gEntities = new AllEntities());
            }
        }

        private AllEntities()
        {
            mDisoverer.Add(new StandardEntityDiscoverer());
            mDisoverer.Add(new DynamicEntityDiscoverer());
        }

        public void AddDiscoverer(IEntityDisoverer discoverer)
        {
            lock (mMutex)
                mDisoverer.Add(discoverer);
        }

        public void ForgetType(Type entity)
        {
            lock (mMutex)
            {
                if (mEntities.ContainsKey(entity))
                    mEntities.Remove(entity);
            }
        }

        public void ForgetScope(string scope)
        {
            lock (mMutex)
            {
                List<Type> toRemove = new List<Type>();
                foreach (Type key in mEntities.Keys)
                {
                    EntityDescriptor descriptor = mEntities[key];
                    if (descriptor.TableDescriptor.Scope == scope)
                        toRemove.Add(key);
                }

                foreach (Type key in toRemove)
                    mEntities.Remove(key);
            }
        }

        public EntityDescriptor this[Type type, bool exceptionIfNotFound = true]
        {
            get
            {
                lock (mMutex)
                {
                    if (mEntities.TryGetValue(type, out EntityDescriptor descriptor))
                        return descriptor;

                    TableDescriptor td = CreateTableDescriptor(type, exceptionIfNotFound);

                    if (td == null)
                        return null;

                    descriptor = new EntityDescriptor() { TableDescriptor = td, EntityType = type };
                    if (!td.Obsolete)
                    {
                        mEntities[type] = descriptor;
                        foreach (TableDescriptor.ColumnInfo column in descriptor.TableDescriptor)
                        {
                            if (column.ForeignKey && column.ForeignTable == descriptor.TableDescriptor)
                            {
                                descriptor.SelfReference = column;
                                break;
                            }
                        }
                    }

                    OnEntityDiscovered?.Invoke(this, new EntityDescriptorEventArgs(descriptor));
                    return descriptor;
                }
            }
        }

        public void PreloadEntities(Assembly assembly, string scope = null) => PreloadEntities(new Assembly[] { assembly }, scope);

        public void PreloadEntities(IEnumerable<Assembly> assemblies, string scope = null)
        {
            lock (mMutex)
            {
                EntityFinder.EntityTypeInfo[] types = EntityFinder.FindEntities(assemblies, scope, false);
                foreach (EntityFinder.EntityTypeInfo type in types)
                {
                    if (!mEntities.ContainsKey(type.EntityType))
                    {
                        EntityDescriptor descriptor = new EntityDescriptor() { TableDescriptor = CreateTableDescriptor(type.EntityType, true), EntityType = type.EntityType };
                        foreach (TableDescriptor.ColumnInfo column in descriptor.TableDescriptor)
                        {
                            if (column.ForeignKey && column.ForeignTable == descriptor.TableDescriptor)
                            {
                                descriptor.SelfReference = column;
                                break;
                            }
                        }

                        mEntities[type.EntityType] = descriptor;
                        OnEntityDiscovered?.Invoke(this, new EntityDescriptorEventArgs(descriptor));
                    }
                }
            }
        }

        private TableDescriptor CreateTableDescriptor(Type type, bool exceptionIfNotFound)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            TableDescriptor descriptor = null;
            for (int i = 0; i < mDisoverer.Count && descriptor == null; i++)
                descriptor = mDisoverer[i].Discover(this, type);

            if (descriptor == null && exceptionIfNotFound)
                throw new EfSqlException(EfExceptionCode.NotEntity, type.FullName);

            return descriptor;
        }

        public IEnumerator<Type> GetEnumerator()
        {
            return mEntities.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return mEntities.Keys.GetEnumerator();
        }
    }
}
