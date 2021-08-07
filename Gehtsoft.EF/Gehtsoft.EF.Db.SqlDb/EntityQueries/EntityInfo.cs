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
    public class EntityDescriptor
    {
        public bool Obsolete { get; internal set; }
        public Type EntityType { get; internal set; }
        public TableDescriptor TableDescriptor { get; internal set; }
        public TableDescriptor.ColumnInfo SelfReference { get; internal set; }
        public TableDescriptor.ColumnInfo this[string id] => TableDescriptor[id];
        public TableDescriptor.ColumnInfo PrimaryKey => TableDescriptor.PrimaryKey;
        private Dictionary<Type, object> mTags = null;

        public object this[Type type]
        {
            get
            {
                if (mTags == null)
                    return null;
                if (mTags.TryGetValue(type, out object tag))
                    return tag;
                return null;
            }
            set
            {
                if (mTags == null)
                    mTags = new Dictionary<Type, object>();
                mTags[type] = value;
            }
        }

        public void SetTag<T>(T tag) where T : class => this[typeof(T)] = tag;
        public T GetTag<T>() where T : class => this[typeof(T)] as T;
    }

    public class AllEntities : IEnumerable<Type>
    {
        public delegate void OnEntityAddedDelegate(EntityDescriptor descriptor);

        public event OnEntityAddedDelegate OnEntityAdded;

        public static EntityDescriptor Get(Type type, bool exceptionIsNotFound = true) => AllEntities.Inst[type, exceptionIsNotFound];
        public static EntityDescriptor Get<T>(bool exceptionIsNotFound = true) => AllEntities.Inst[typeof(T), exceptionIsNotFound];

        public class NamingPolicyManager
        {
            public EntityNamingPolicy Default { get; set; } = EntityNamingPolicy.BackwardCompatibility;
            private readonly Dictionary<string, EntityNamingPolicy> mNamingPolicies = new Dictionary<string, EntityNamingPolicy>();
            private const string DEFAULTSCOPE = "gs$$defaultscope";

            public EntityNamingPolicy this[string scope]
            {
                get
                {
                    if (scope == null)
                        return Default;

                    if (!mNamingPolicies.TryGetValue(scope, out EntityNamingPolicy policy))
                        policy = Default;

                    return policy;
                }
                set => mNamingPolicies[scope ?? DEFAULTSCOPE] = value;
            }
        }

        public EntityDescriptor[] All() => mEntities.Values.ToArray();

        public NamingPolicyManager NamingPolicy { get; } = new NamingPolicyManager();

        private readonly Dictionary<Type, EntityDescriptor> mEntities = new Dictionary<Type, EntityDescriptor>();

        private static AllEntities gEntities = null;

        private readonly object mMutex = new object();

        public static AllEntities Inst
        {
            get
            {
                return gEntities ?? (gEntities = new AllEntities());
            }
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

                    OnEntityAdded?.Invoke(descriptor);

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
                        OnEntityAdded?.Invoke(descriptor);
                    }
                }
            }
        }

        private static readonly Type DynamicEntityType = typeof(DynamicEntity);

        private TableDescriptor CreateTableDescriptor(Type type, bool exceptionIfNotFound)
        {
            EntityAttribute typeAttribute;
            ObsoleteEntityAttribute obsoleteTypeAttribute = null;
            DynamicEntity dynamicEntity = null;
            TableDescriptor descriptor;
            if (DynamicEntityType.IsAssignableFrom(type))
            {
                dynamicEntity = (DynamicEntity)Activator.CreateInstance(type);
                typeAttribute = dynamicEntity.EntityAttribute;
                if (typeAttribute == null)
                    obsoleteTypeAttribute = dynamicEntity.ObsoleteEntityAttribute;
            }
            else
            {
                typeAttribute = type.GetCustomAttribute<EntityAttribute>();
                if (typeAttribute == null)
                    obsoleteTypeAttribute = type.GetCustomAttribute<ObsoleteEntityAttribute>();
            }

            EntityNamingPolicy namingPolicy = EntityNamingPolicy.Default;

            string tableScope;
            if (typeAttribute == null)
            {
                if (obsoleteTypeAttribute == null)
                {
                    if (exceptionIfNotFound)
                        throw new EfSqlException(EfExceptionCode.NotEntity, type.FullName);
                    else
                        return null;
                }

                tableScope = obsoleteTypeAttribute.Scope;
                descriptor = new TableDescriptor(obsoleteTypeAttribute.Table) { Obsolete = true, View = obsoleteTypeAttribute.View };
                if (obsoleteTypeAttribute.Metadata != null)
                    descriptor.Metadata = Activator.CreateInstance(obsoleteTypeAttribute.Metadata);
            }
            else
            {
                tableScope = typeAttribute.Scope;
                namingPolicy = typeAttribute.NamingPolicy;
                descriptor = new TableDescriptor(typeAttribute.Table) { View = typeAttribute.View };
                if (typeAttribute.Metadata != null)
                    descriptor.Metadata = Activator.CreateInstance(typeAttribute.Metadata);
            }

            namingPolicy = (namingPolicy == EntityNamingPolicy.Default ? NamingPolicy[tableScope] : namingPolicy);

            if (descriptor.Name == null)
                descriptor.Name = EntityNameConvertor.ConvertTableName(type.Name, namingPolicy == EntityNamingPolicy.BackwardCompatibility ? EntityNamingPolicy.AsIs : namingPolicy);

            descriptor.Scope = tableScope;

            if (dynamicEntity == null)
            {
                foreach (PropertyInfo propertyInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    CreateColumnDescriptor(type, namingPolicy, descriptor, new PropertyAccessor(propertyInfo));
            }
            else
            {
                foreach (IDynamicEntityProperty property in dynamicEntity.Properties)
                    CreateColumnDescriptor(type, namingPolicy, descriptor, new DynamicPropertyAccessor(property));
            }

            return descriptor;
        }

        private void CreateColumnDescriptor(Type type, EntityNamingPolicy policy, TableDescriptor descriptor, IPropertyAccessor propertyAccessor)
        {
            EntityPropertyAttribute propertyAttribute = propertyAccessor.GetCustomAttribute<EntityPropertyAttribute>();
            if (propertyAttribute != null)
            {
                if (policy == EntityNamingPolicy.BackwardCompatibility && propertyAttribute.Field == null)
                    propertyAttribute.Field = propertyAccessor.Name.ToLower();

                if (propertyAttribute.ForeignKey)
                {
                    TableDescriptor foreignTable = propertyAccessor.PropertyType == type ? descriptor : this[propertyAccessor.PropertyType].TableDescriptor;
                    TableDescriptor.ColumnInfo pk = foreignTable.PrimaryKey;

                    descriptor.Add(new TableDescriptor.ColumnInfo()
                    {
                        ID = propertyAccessor.Name,
                        Name = propertyAttribute.Field ?? EntityNameConvertor.ConvertName(foreignTable.Name + "Ref", policy),
                        DbType = pk.DbType,
                        PrimaryKey = false,
                        Autoincrement = false,
                        Nullable = propertyAttribute.Nullable,
                        Size = pk.Size,
                        Precision = pk.Precision,
                        ForeignTable = foreignTable,
                        PropertyAccessor = propertyAccessor,
                        DefaultValue = propertyAttribute.DefaultValue,
                    });
                }
                else if (propertyAttribute.AutoId)
                {
                    descriptor.Add(new TableDescriptor.ColumnInfo()
                    {
                        ID = propertyAccessor.Name,
                        Name = propertyAttribute.Field ?? EntityNameConvertor.ConvertName("Id", policy),
                        DbType = System.Data.DbType.Int32,
                        PrimaryKey = true,
                        Autoincrement = true,
                        Nullable = false,
                        Size = 0,
                        Precision = 0,
                        ForeignTable = null,
                        PropertyAccessor = propertyAccessor,
                        IgnoreRead = propertyAttribute.IgnoreRead,
                        DefaultValue = propertyAttribute.DefaultValue,
                    });
                }
                else
                {
                    if (propertyAttribute.DbType == DbType.Object)
                    {
                        bool nullable = false;

                        Type propType = propertyAccessor.PropertyType;
                        Type propType1 = Nullable.GetUnderlyingType(propType);

                        if (propType1 != null)
                        {
                            propType = propType1;
                            nullable = true;
                        }

                        if (propType == typeof(string))
                        {
                            propertyAttribute.DbType = DbType.String;
                        }
                        else if (propType == typeof(Guid))
                        {
                            propertyAttribute.DbType = DbType.Guid;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(bool))
                        {
                            propertyAttribute.DbType = DbType.Boolean;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(int))
                        {
                            propertyAttribute.DbType = DbType.Int32;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(double))
                        {
                            propertyAttribute.DbType = DbType.Double;
                            propertyAttribute.Nullable = nullable;
                            if (propertyAttribute.Size == 0)
                            {
                                propertyAttribute.Size = 18;
                                if (propertyAttribute.Precision == 0)
                                    propertyAttribute.Precision = 7;
                            }
                        }
                        else if (propType == typeof(DateTime))
                        {
                            propertyAttribute.DbType = DbType.DateTime;
                            propertyAttribute.Nullable = nullable;
                        }
                    }

                    descriptor.Add(new TableDescriptor.ColumnInfo()
                    {
                        ID = propertyAccessor.Name,
                        Name = propertyAttribute.Field ?? EntityNameConvertor.ConvertName(propertyAccessor.Name, policy),
                        DbType = propertyAttribute.DbType,
                        PrimaryKey = propertyAttribute.PrimaryKey,
                        Autoincrement = propertyAttribute.Autoincrement,
                        Nullable = propertyAttribute.Nullable,
                        Size = propertyAttribute.Size,
                        Precision = propertyAttribute.Precision,
                        Sorted = propertyAttribute.Sorted,
                        ForeignTable = null,
                        PropertyAccessor = propertyAccessor,
                        IgnoreRead = propertyAttribute.IgnoreRead,
                        DefaultValue = propertyAttribute.DefaultValue,
                        Unique = propertyAttribute.Unique,
                    });
                }
            }
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
