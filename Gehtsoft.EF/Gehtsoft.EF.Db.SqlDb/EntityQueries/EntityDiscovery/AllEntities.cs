using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The class that discovers and keeps information about all entities for SQL-related tables purpose.
    ///
    /// The class is a signleton. Use <see cref="Inst"/> method to get an instance of the class.
    /// </summary>
    public sealed class AllEntities : IEnumerable<Type>
    {
        /// <summary>
        /// The event invoked when a new entity is discovered.
        /// </summary>
        public event EventHandler<EntityDescriptorEventArgs> OnEntityDiscovered;

        /// <summary>
        /// Gets the entity descriptor of the type specified.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="exceptionIsNotFound"></param>
        /// <returns></returns>
        public static EntityDescriptor Get(Type type, bool exceptionIsNotFound = true) => AllEntities.Inst[type, exceptionIsNotFound];

        /// <summary>
        /// Gets the entity descriptor of the type specified (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="exceptionIsNotFound"></param>
        /// <returns></returns>
        public static EntityDescriptor Get<T>(bool exceptionIsNotFound = true) => AllEntities.Inst[typeof(T), exceptionIsNotFound];

        public static void EnableDynamicEntityDiscoveryInEntityFinder()
        {
            EntityFinder.AddProbe(new DynamicEntityDiscoverer());
        }

        /// <summary>
        /// Gets all already discovered entities.
        ///
        /// The entity must be requested in order to be included to this list. This method
        /// do not search for entities. Use <see cref="EntityFinder"/> to find all entities in the assemblies.
        /// </summary>
        /// <returns></returns>
        public EntityDescriptor[] All() => mEntities.Values.ToArray();

        /// <summary>
        /// The naming policy manager.
        /// </summary>
        public NamingPolicyManager NamingPolicy { get; } = new NamingPolicyManager();

        private readonly Dictionary<Type, EntityDescriptor> mEntities = new Dictionary<Type, EntityDescriptor>();

        private static AllEntities gEntities = null;

        private readonly object mMutex = new object();

        private readonly List<IEntityDisoverer> mDisoverer = new List<IEntityDisoverer>();

        /// <summary>
        /// Returns an instance of the class.
        /// </summary>
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

        /// <summary>
        /// Adds custom discoverer of the entity.
        ///
        /// By default, two discoverer are added - discoverer for entities
        /// defined by <see cref="EntityPropertyAttribute"/> and for entities derived from
        /// <see cref="DynamicEntity"/>
        /// </summary>
        /// <param name="discoverer"></param>
        public void AddDiscoverer(IEntityDisoverer discoverer)
        {
            lock (mMutex)
                mDisoverer.Add(discoverer);
        }

        /// <summary>
        /// Forced the manager to forget the entity.
        /// </summary>
        /// <param name="entity"></param>
        public void ForgetType(Type entity)
        {
            lock (mMutex)
            {
                if (mEntities.ContainsKey(entity))
                    mEntities.Remove(entity);
            }
        }

        /// <summary>
        /// Forces the manager to forget all entities in the scope.
        /// </summary>
        /// <param name="scope"></param>
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

        /// <summary>
        /// Gets the entity description by its type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="exceptionIfNotFound"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Preloads entities from the assembly.
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="scope"></param>
        public void PreloadEntities(Assembly assembly, string scope = null) => PreloadEntities(new Assembly[] { assembly }, scope);

        /// <summary>
        /// Preloads entities from the list of the assemblies.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <param name="scope"></param>
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

        [ExcludeFromCodeCoverage]
        [DocgenIgnore]
        public IEnumerator<Type> GetEnumerator()
        {
            return mEntities.Keys.GetEnumerator();
        }

        [ExcludeFromCodeCoverage]
        [DocgenIgnore]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return mEntities.Keys.GetEnumerator();
        }
    }
}
