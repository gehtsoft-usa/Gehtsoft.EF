using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The controller to create, drop or update entities automatically.
    ///
    /// The method recognizes and handles the following cases:
    /// * Create table for the entity if the table does not exist.
    /// * Drop table for the entity if the table is attributed using <see cref="ObsoleteEntityAttribute"/>
    /// * Add column for the entity if the column does not exist.
    /// * Drops column if the column attributed using <see cref="ObsoleteEntityPropertyAttribute"/>
    ///
    /// If you need to enrich the operation with the custom logic, you can use action (see <see cref="EntityActionDelegate"/>)
    /// or events <see cref="CreateEntityController.OnAction"/>.
    ///
    /// Note: Some connections do not support dropping of the columns. Check <see cref="SqlDbLanguageSpecifics.DropColumnSupported"/> flag to verify the connection.
    /// If connection does not support columns dropping, the columns will remain in the table.
    ///
    /// Note: The controller does not recognize the situations when:
    /// * The type, name or other parameters of the column has changed.
    /// * The property deleted from the entity.
    /// * A new index is added via <see cref="ICompositeIndexMetadata"/>
    /// * View changed.
    ///
    /// In case the change is too complex to be handled automatically,
    /// use the patches (see <see cref="Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch"/>.
    /// </summary>
    public class CreateEntityController
    {
        private readonly IEnumerable<Assembly> mAssemblies;
        private readonly string mScope;
        private EntityFinder.EntityTypeInfo[] mTypes;

        /// <summary>
        /// The event raised when action is performed.
        /// </summary>
        public event EventHandler<CreateEntityControllerEventArgs> OnAction;

        [DocgenIgnore]
        public CreateEntityController(Type findNearThisType, string scope = null) :
               this(findNearThisType.GetTypeInfo().Assembly, scope)
        {
        }

        /// <summary>
        /// Constructor to search entities in one assembly.
        /// </summary>
        /// <param name="entityAssembly"></param>
        /// <param name="scope"></param>
        public CreateEntityController(Assembly entityAssembly, string scope = null) :
               this(new Assembly[] { entityAssembly }, scope)
        {
        }

        /// <summary>
        /// Constructor to search entities in multiple assemblies.
        /// </summary>
        /// <param name="assemblies"></param>
        /// <param name="scope"></param>
        public CreateEntityController(IEnumerable<Assembly> assemblies, string scope = null)
        {
            mAssemblies = assemblies;
            mScope = scope;
        }

        private void LoadTypes(bool includeObsolete = false)
        {
            mTypes = ActionController.FindEntities(mAssemblies, mScope, includeObsolete);
            foreach (EntityFinder.EntityTypeInfo type in mTypes)
            {
                if (type.Table == null)
                {
                    var namingPolicy = (type.NamingPolicy == EntityNamingPolicy.Default ? AllEntities.Inst.NamingPolicy[type.Scope] : type.NamingPolicy);
                    type.Table = EntityNameConvertor.ConvertTableName(type.EntityType.Name, namingPolicy == EntityNamingPolicy.BackwardCompatibility ? EntityNamingPolicy.AsIs : namingPolicy);
                }
            }
            EntityFinder.ArrageEntities(mTypes);
        }

        internal interface ICreateEntityControllerAction
        {
            EntityFinder.EntityTypeInfo[] FindEntities(IEnumerable<Assembly> assemblies, string scope, bool includeObsolete);
            void Create(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType);
            void Drop(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType);
            void AddColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns);
            void DropColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns);
        }

        private class CreateEntityControllerAction : ICreateEntityControllerAction
        {
            public void AddColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns)
            {
                var builder = connection.GetAlterTableQueryBuilder();
                builder.SetTable(td, columns, null);
                foreach (var queryText in builder.GetQueries())
                    using (var query = connection.GetQuery(queryText))
                        query.ExecuteNoData();
            }

            private static readonly Type gViewCreationMetata = typeof(IViewCreationMetadata);

            public void Create(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType)
            {
                EntityQuery query = null;
                try
                {
                    if (!entityType.View)
                        query = connection.GetCreateEntityQuery(entityType.EntityType);
                    else
                    {
                        if (entityType.Metadata != null && gViewCreationMetata.IsAssignableFrom(entityType.Metadata))
                            query = connection.GetCreateViewQuery(entityType.EntityType);
                    }
                    if (query != null)
                        query.Execute();
                }
                finally
                {
                    query?.Dispose();
                }
            }

            public void Drop(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType)
            {
                EntityQuery query = null;
                try
                {
                    if (!entityType.View)
                        query = connection.GetDropEntityQuery(entityType.EntityType);
                    else
                    {
                        if (entityType.Metadata != null &&
                            gViewCreationMetata.IsAssignableFrom(entityType.Metadata) &&
                            connection.DoesObjectExist(entityType.Table, null, "view"))
                            query = connection.GetDropViewQuery(entityType.EntityType);
                    }
                    if (query != null)
                        query.Execute();
                }
                finally
                {
                    query?.Dispose();
                }
            }

            public void DropColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns)
            {
                var builder = connection.GetAlterTableQueryBuilder();
                builder.SetTable(td, null, columns);
                foreach (var queryText in builder.GetQueries())
                    using (var query = connection.GetQuery(queryText))
                        query.ExecuteNoData();
            }

            public EntityFinder.EntityTypeInfo[] FindEntities(IEnumerable<Assembly> assemblies, string scope, bool includeObsolete)
                => EntityFinder.FindEntities(assemblies, scope, includeObsolete);
        }

        internal ICreateEntityControllerAction ActionController { get; set; } = new CreateEntityControllerAction();

        private static void InvokeAttribute<T>(object target, SqlDbConnection connection)
            where T : OnEntityActionAttribute
        {
            OnEntityActionAttribute attribute = null;
            if (target is Type type)
                attribute = type.GetCustomAttribute<T>();
            else if (target is EntityFinder.EntityTypeInfo typeInfo)
                attribute = typeInfo.EntityType.GetCustomAttribute<T>();
            else if (target is PropertyInfo propertyInfo)
                attribute = propertyInfo.GetCustomAttribute<T>();
            else if (target is IPropertyAccessor accessor)
                attribute = accessor.GetCustomAttribute<T>();
            else if (target is TableDescriptor.ColumnInfo columnInfo)
                attribute = columnInfo.PropertyAccessor?.GetCustomAttribute<T>();

            if (attribute != null)
                attribute.Invoke(connection);
        }

        private class UpdateModeHelper
        {
            private readonly UpdateMode mDefaultMode;
            private readonly IDictionary<Type, UpdateMode> mSpecificModes;

            public UpdateModeHelper(UpdateMode defaultMode, IDictionary<Type, UpdateMode> specificModes)
            {
                mDefaultMode = defaultMode;
                mSpecificModes = specificModes;
            }

            public UpdateMode GetUpdateMode(Type type)
            {
                if (mSpecificModes != null && mSpecificModes.TryGetValue(type, out var mode))
                    return mode;
                return mDefaultMode;
            }
        }

        /// <summary>
        /// Drop tables.
        /// </summary>
        /// <param name="connection"></param>
        public void DropTables(SqlDbConnection connection)
        {
            LoadTypes(includeObsolete: true);
            foreach (EntityFinder.EntityTypeInfo info in mTypes.Reverse())
            {
                InvokeAttribute<OnEntityDropAttribute>(info, connection);
                RaiseDrop(info.Table);
                ActionController.Drop(connection, info);
            }
        }

        /// <summary>
        /// Drop tables (async version).
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public Task DropTablesAsync(SqlDbConnection connection) => Task.Run(() => DropTables(connection));

        /// <summary>
        /// Creates tables.
        /// </summary>
        /// <param name="connection"></param>
        public void CreateTables(SqlDbConnection connection)
        {
            LoadTypes(includeObsolete: false);
            foreach (EntityFinder.EntityTypeInfo info in mTypes)
            {
                RaiseCreate(info.Table);
                ActionController.Create(connection, info);
                InvokeAttribute<OnEntityCreateAttribute>(info, connection);
            }
        }

        /// <summary>
        /// Creates tables (async version).
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public Task CreateTablesAsync(SqlDbConnection connection) => Task.Run(() => CreateTables(connection));

        /// <summary>
        /// The mode of update operation.
        /// </summary>
        public enum UpdateMode
        {
            /// <summary>
            /// Drop and create table.
            /// </summary>
            Recreate,
            /// <summary>
            /// Create new tables, update tables where columns are added or dropped.
            /// </summary>
            Update,
            /// <summary>
            /// Only creates new tables.
            /// </summary>
            CreateNew,
        }

        private readonly static TableDescriptor mDummyTable = new TableDescriptor("dummytable");

        /// <summary>
        /// Update tables (asynchronous version).
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="defaultUpdateMode">The default mode for update.</param>
        /// <param name="individualUpdateModes">Update modes for individual types.</param>
        /// <returns></returns>
        public Task UpdateTablesAsync(SqlDbConnection connection, UpdateMode defaultUpdateMode, IDictionary<Type, UpdateMode> individualUpdateModes = null)
            => Task.Run(() => UpdateTables(connection, defaultUpdateMode, individualUpdateModes));

        /// <summary>
        /// Update tables.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="defaultUpdateMode">The default mode for update.</param>
        /// <param name="individualUpdateModes">Update modes for individual types.</param>
        public void UpdateTables(SqlDbConnection connection, UpdateMode defaultUpdateMode, IDictionary<Type, UpdateMode> individualUpdateModes = null)
        {
            var updateModes = new UpdateModeHelper(defaultUpdateMode, individualUpdateModes);

            LoadTypes(includeObsolete: true);
            var schema = connection.Schema();

            foreach (EntityFinder.EntityTypeInfo info in mTypes.Where(info => info.View))
            {
                if (schema.Contains(info.Table))
                {
                    InvokeAttribute<OnEntityDropAttribute>(info, connection);
                    RaiseDrop(info.Table);
                    ActionController.Drop(connection, info);
                }
            }

            //drop obsolete tables and/or columns and tables which are forced to be recreated
            foreach (EntityFinder.EntityTypeInfo info in mTypes.Reverse().Where(info => !info.View))
            {
                if (schema.Contains(info.Table))
                {
                    if (info.Obsolete || updateModes.GetUpdateMode(info.EntityType) == UpdateMode.Recreate)
                    {
                        if (connection.GetLanguageSpecifics().DropColumnSupported ||
                            Array.Find(mTypes, i => i != info &&
                                               i.DependsOn.Find(i1 => i1 == info.EntityType) != null) == null)
                        {
                            InvokeAttribute<OnEntityDropAttribute>(info, connection);
                            RaiseDrop(info.Table);
                            ActionController.Drop(connection, info);
                        }
                    }
                    else
                    {
                        if (connection.GetLanguageSpecifics().DropColumnSupported)
                        {
                            List<TableDescriptor.ColumnInfo> dropColumns = null;
                            foreach (PropertyInfo property in info.EntityType.GetProperties())
                            {
                                ObsoleteEntityPropertyAttribute attribute = property.GetCustomAttribute<ObsoleteEntityPropertyAttribute>();

                                if (attribute != null)
                                {
                                    var policy = info.NamingPolicy;
                                    if (policy == EntityNamingPolicy.Default)
                                        policy = AllEntities.Inst.NamingPolicy[mScope];

                                    string field = attribute.Field ?? EntityNameConvertor.ConvertName(property.Name, policy);

                                    if (schema.Contains(info.Table, field))
                                    {
                                        if (dropColumns == null)
                                            dropColumns = new List<TableDescriptor.ColumnInfo>();
                                        dropColumns.Add(new TableDescriptor.ColumnInfo()
                                        {
                                            Name = field,
                                            ForeignTable = attribute.ForeignKey ? mDummyTable : null,
                                            Sorted = attribute.Sorted,
                                        });

                                        InvokeAttribute<OnEntityPropertyDropAttribute>(property, connection);
                                    }
                                }
                            }
                            if (dropColumns != null)
                                ActionController.DropColumns(connection, info, new TableDescriptor(info.Table), dropColumns.ToArray());
                        }
                    }
                }
            }

            //create and/or update tables
            foreach (EntityFinder.EntityTypeInfo info in mTypes.Where(info => !info.View))
            {
                if (info.Obsolete)
                    continue;

                if (!schema.Contains(info.Table) ||
                     updateModes.GetUpdateMode(info.EntityType) == UpdateMode.Recreate)
                {
                    RaiseCreate(info.Table);
                    ActionController.Create(connection, info);
                    InvokeAttribute<OnEntityCreateAttribute>(info, connection);
                }
                else
                {
                    List<TableDescriptor.ColumnInfo> addColumns = null;
                    List<OnEntityPropertyCreateAttribute> delegates = null;

                    TableDescriptor descriptor = AllEntities.Inst[info.EntityType].TableDescriptor;

                    foreach (TableDescriptor.ColumnInfo column in descriptor)
                    {
                        if (!schema.Contains(info.Table, column.Name))
                        {
                            if (addColumns == null)
                                addColumns = new List<TableDescriptor.ColumnInfo>();
                            if (delegates == null)
                                delegates = new List<OnEntityPropertyCreateAttribute>();
                            addColumns.Add(column);
                        }
                    }
                    if (addColumns != null)
                    {
                        ActionController.AddColumns(connection, info, new TableDescriptor(info.Table), addColumns.ToArray());
                        RaiseUpdate(info.Table);

                        for (int i = 0; i < addColumns.Count; i++)
                            InvokeAttribute<OnEntityPropertyCreateAttribute>(addColumns[i], connection);
                    }
                }
            }

            foreach (EntityFinder.EntityTypeInfo info in mTypes.Where(info => info.View))
            {
                if (info.Obsolete)
                    continue;
                RaiseCreate(info.Table);
                ActionController.Create(connection, info);
                InvokeAttribute<OnEntityCreateAttribute>(info, connection);
            }
        }

        private void RaiseCreate(string table)
        {
            if (OnAction != null)
            {
                CreateEntityControllerEventArgs args = new CreateEntityControllerEventArgs(CreateEntityControllerEventArgs.Action.Create, table);
                OnAction.Invoke(this, args);
            }
        }

        private void RaiseDrop(string table)
        {
            if (OnAction != null)
            {
                CreateEntityControllerEventArgs args = new CreateEntityControllerEventArgs(CreateEntityControllerEventArgs.Action.Drop, table);
                OnAction.Invoke(this, args);
            }
        }

        private void RaiseUpdate(string table)
        {
            if (OnAction != null)
            {
                CreateEntityControllerEventArgs args = new CreateEntityControllerEventArgs(CreateEntityControllerEventArgs.Action.Update, table);
                OnAction.Invoke(this, args);
            }
        }
    }
}
