using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The delegate type of the action called when entity is created.
    ///
    /// The delegate prototype is:
    /// ```cs
    /// public delegate void EntityActionDelegate(SqlDbConnection connection);
    /// ```
    ///
    /// The action is invoked only when <see cref="CreateEntityController"/> is used
    /// to create the entity.
    ///
    /// To set the action use <see cref="OnEntityCreateAttribute"/>, <see cref="OnEntityDropAttribute"/>,
    /// <see cref="OnEntityPropertyCreateAttribute"/> or <see cref="OnEntityPropertyDropAttribute"/>.
    /// </summary>
    /// <param name="connection"></param>
    public delegate void EntityActionDelegate(SqlDbConnection connection);

    /// <summary>
    /// The delegate type of the action called when entity is created (asynchronous version).
    ///
    /// The delegate prototype is:
    /// ```cs
    /// public delegate Task EntityActionAsyncDelegate(SqlDbConnection connection);
    /// ```
    ///
    /// The action is invoked only when <see cref="CreateEntityController"/> is used
    /// to create the entity.
    ///
    /// To set the action use <see cref="OnEntityCreateAttribute"/>, <see cref="OnEntityDropAttribute"/>,
    /// <see cref="OnEntityPropertyCreateAttribute"/> or <see cref="OnEntityPropertyDropAttribute"/>.
    /// </summary>
    /// <param name="connection"></param>
    /// <returns></returns>
    public delegate Task EntityActionAsyncDelegate(SqlDbConnection connection);

    /// <summary>
    /// Event arguments for `OnAction` event of `CreateEntityController`
    ///
    /// See also <see cref="CreateEntityController.OnAction"/>
    /// </summary>
    public class CreateEntityControllerEventArgs : EventArgs
    {
        /// <summary>
        /// The action type.
        /// </summary>
        public enum Action
        {
            Create,
            Drop,
            Update,
            Processing,
        }

        /// <summary>
        /// The action.
        /// </summary>
        public Action EventAction { get; set; }

        /// <summary>
        /// The table name.
        /// </summary>
        public string Table { get; set; }

        [DocgenIgnore]
        public CreateEntityControllerEventArgs(Action action, string table)
        {
            EventAction = action;
            Table = table;
        }
    }

    /// <summary>
    /// The base class for attribute to set the action when entity or property is created by `CreateEntityController`.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    [DocgenIgnore]
    public abstract class OnEntityActionAttribute : Attribute
    {
        private readonly Type mType;
        private readonly string mName;
        private EntityActionDelegate mAction;
        private EntityActionAsyncDelegate mAsyncAction;
        private bool mInit = false;

        private void Initialize()
        {
            mInit = true;
            mAction = null;
            mAsyncAction = null;

            MethodInfo mi = mType.GetTypeInfo().GetMethod(mName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
                throw new ArgumentException($"Action method {mName} isn't found");

            var parameters = mi.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(SqlDbConnection))
                throw new ArgumentException("Action method should have only one parameter and this parameter must accepts Sql connection");

            if (mi.ReturnType == typeof(void))
            {
                mAction = mi.CreateDelegate(typeof(EntityActionDelegate)) as EntityActionDelegate;
            }
            else if (mi.ReturnType == typeof(Task))
            {
                mAsyncAction = mi.CreateDelegate(typeof(EntityActionAsyncDelegate)) as EntityActionAsyncDelegate;
            }
            else
            {
                throw new ArgumentException("Action method should either return void or Task");
            }

            if (mAction == null && mAsyncAction == null)
                throw new ArgumentException("Delegate signature does not match");
        }

        protected EntityActionAsyncDelegate AsyncAction
        {
            get
            {
                if (!mInit)
                    Initialize();

                return mAsyncAction;
            }
        }

        protected EntityActionDelegate Action
        {
            get
            {
                if (!mInit)
                    Initialize();

                return mAction;
            }
        }

        protected OnEntityActionAttribute(Type containerType, string delegateName)
        {
            mType = containerType;
            mName = delegateName;
        }

        public void Invoke(SqlDbConnection connection)
        {
            if (!mInit)
                Initialize();
            if (mAction != null)
                mAction(connection);
            else if (mAsyncAction != null)
                mAsyncAction(connection).GetAwaiter().GetResult();
        }

        public async Task InvokeAsync(SqlDbConnection connection)
        {
            if (!mInit)
                Initialize();

            if (mAction != null)
            {
                mAction(connection);
            }
            else if (mAsyncAction != null)
            {
                await mAsyncAction(connection);
            }
        }
    }

    /// <summary>
    /// The attribute to set the action to be called when entity is created.
    ///
    /// The action will be called only if the entity is created using
    /// <see cref="CreateEntityController"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class OnEntityCreateAttribute : OnEntityActionAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="containerType">The type that consists of action method.</param>
        /// <param name="delegateName">The action method name. The method should match either <see cref="EntityActionDelegate"/> or <see cref="EntityActionAsyncDelegate"/></param>
        public OnEntityCreateAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

    /// <summary>
    /// The attribute to set the action to be called when entity is dropped.
    ///
    /// The action will be called only if the entity is dropped using
    /// <see cref="CreateEntityController"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class OnEntityDropAttribute : OnEntityActionAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="containerType">The type that consists of action method.</param>
        /// <param name="delegateName">The action method name. The method should match either <see cref="EntityActionDelegate"/> or <see cref="EntityActionAsyncDelegate"/></param>
        public OnEntityDropAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

    /// <summary>
    /// The attribute to set the action to be called when property is created.
    ///
    /// The action will be called only if the entity is created using
    /// <see cref="CreateEntityController"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OnEntityPropertyCreateAttribute : OnEntityActionAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="containerType">The type that consists of action method.</param>
        /// <param name="delegateName">The action method name. The method should match either <see cref="EntityActionDelegate"/> or <see cref="EntityActionAsyncDelegate"/></param>
        public OnEntityPropertyCreateAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

    /// <summary>
    /// The attribute to set the action to be called when property is dropped.
    ///
    /// The action will be called only if the property is dropped using
    /// <see cref="CreateEntityController"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OnEntityPropertyDropAttribute : OnEntityActionAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="containerType">The type that consists of action method.</param>
        /// <param name="delegateName">The action method name. The method should match either <see cref="EntityActionDelegate"/> or <see cref="EntityActionAsyncDelegate"/></param>
        public OnEntityPropertyDropAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

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
            if (mTypes == null)
            {
                mTypes = EntityFinder.FindEntities(mAssemblies, mScope, includeObsolete);
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
        }

        protected async ValueTask DropTablesCore(SqlDbConnection connection, bool asyncCall)
        {
            LoadTypes();
            foreach (EntityFinder.EntityTypeInfo info in mTypes.Reverse())
            {
                OnEntityDropAttribute attribute = info.EntityType.GetTypeInfo().GetCustomAttribute<OnEntityDropAttribute>();
                if (attribute != null)
                {
                    RaiseProcessing(info.Table);
                    if (asyncCall)
                        await attribute.InvokeAsync(connection);
                    else
                        attribute.Invoke(connection);
                }

                bool view = info.View && info.Metadata != null && typeof(IViewCreationMetadata).IsAssignableFrom(info.Metadata);

                if (info.View && !view)
                    continue;

                using (EntityQuery drop = view ? connection.GetDropViewQuery(info.EntityType) :
                                                 connection.GetDropEntityQuery(info.EntityType))
                {
                    RaiseDrop(info.Table);
                    if (asyncCall)
                        await drop.ExecuteAsync();
                    else
                        drop.Execute();
                }
            }
        }

        /// <summary>
        /// Drop tables.
        /// </summary>
        /// <param name="connection"></param>
        public void DropTables(SqlDbConnection connection) => DropTablesCore(connection, false).GetAwaiter().GetResult();

        /// <summary>
        /// Drop tables (async version).
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public Task DropTablesAsync(SqlDbConnection connection) => DropTablesCore(connection, true).AsTask();

        private async ValueTask CreateTablesCore(SqlDbConnection connection, bool asyncCall)
        {
            LoadTypes();
            foreach (EntityFinder.EntityTypeInfo info in mTypes)
            {
                bool view = info.View && info.Metadata != null && typeof(IViewCreationMetadata).IsAssignableFrom(info.Metadata);

                if (info.View && !view)
                    continue;

                using (EntityQuery create = view ? connection.GetCreateViewQuery(info.EntityType) :
                                                    connection.GetCreateEntityQuery(info.EntityType))
                {
                    RaiseCreate(info.Table);
                    if (asyncCall)
                        await create.ExecuteAsync();
                    else
                        create.Execute();
                }

                OnEntityCreateAttribute attribute = info.EntityType.GetTypeInfo().GetCustomAttribute<OnEntityCreateAttribute>();
                if (attribute != null)
                {
                    RaiseProcessing(info.Table);
                    if (asyncCall)
                        await attribute.InvokeAsync(connection);
                    else
                        attribute.Invoke(connection);
                }
            }
        }

        /// <summary>
        /// Creates tables.
        /// </summary>
        /// <param name="connection"></param>
        public void CreateTables(SqlDbConnection connection) => CreateTablesCore(connection, false).GetAwaiter().GetResult();

        /// <summary>
        /// Creates tables (async version).
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public Task CreateTablesAsync(SqlDbConnection connection) => CreateTablesCore(connection, true).AsTask();

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
            => UpdateTablesCore(false, connection, defaultUpdateMode, individualUpdateModes);

        /// <summary>
        /// Update tables.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="defaultUpdateMode">The default mode for update.</param>
        /// <param name="individualUpdateModes">Update modes for individual types.</param>
        public void UpdateTables(SqlDbConnection connection, UpdateMode defaultUpdateMode, IDictionary<Type, UpdateMode> individualUpdateModes = null)
            => UpdateTablesCore(true, connection, defaultUpdateMode, individualUpdateModes).ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task UpdateTablesCore(bool sync, SqlDbConnection connection, UpdateMode defaultUpdateMode, IDictionary<Type, UpdateMode> individualUpdateModes = null)
        {
            LoadTypes(true);
            TableDescriptor[] schema = null;

            if (sync)
                schema = connection.Schema();
            else
                schema = await connection.SchemaAsync();

            foreach (EntityFinder.EntityTypeInfo info in mTypes.Where(info => info.View))
            {
                if (info.Metadata != null && typeof(IViewCreationMetadata).IsAssignableFrom(info.Metadata))
                {
                    OnEntityDropAttribute attribute = info.EntityType.GetTypeInfo().GetCustomAttribute<OnEntityDropAttribute>();
                    if (attribute != null)
                    {
                        RaiseProcessing(info.Table);
                        if (sync)
                            attribute.Invoke(connection);
                        else
                            await attribute.InvokeAsync(connection);
                    }
                    using (EntityQuery drop = connection.GetDropViewQuery(info.EntityType))
                    {
                        RaiseDrop(info.Table);
                        if (sync)
                            drop.Execute();
                        else
                            await drop.ExecuteAsync();
                    }
                }
            }

            //drop obsolete tables and/or columns and tables which are forced to be recreated
            foreach (EntityFinder.EntityTypeInfo info in mTypes.Reverse().Where(info => !info.View))
            {
                if (schema.Contains(info.Table))
                {
                    if (info.Obsolete ||
                        defaultUpdateMode == UpdateMode.Recreate ||
                        (individualUpdateModes != null && individualUpdateModes.ContainsKey(info.EntityType) &&
                         individualUpdateModes[info.EntityType] == UpdateMode.Recreate))
                    {
                        OnEntityDropAttribute attribute = info.EntityType.GetTypeInfo().GetCustomAttribute<OnEntityDropAttribute>();
                        if (attribute != null)
                        {
                            RaiseProcessing(info.Table);
                            if (sync)
                                attribute.Invoke(connection);
                            else
                                await attribute.InvokeAsync(connection);
                        }
                        using (EntityQuery drop = connection.GetDropEntityQuery(info.EntityType))
                        {
                            RaiseDrop(info.Table);
                            if (sync)
                                drop.Execute();
                            else
                                await drop.ExecuteAsync();
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
                                if (attribute != null && schema.Contains(info.Table, attribute.Field))
                                {
                                    if (dropColumns == null)
                                        dropColumns = new List<TableDescriptor.ColumnInfo>();
                                    dropColumns.Add(new TableDescriptor.ColumnInfo()
                                    {
                                        Name = attribute.Field,
                                        ForeignTable = attribute.ForeignKey ? mDummyTable : null,
                                        Sorted = attribute.Sorted,
                                    });

                                    OnEntityPropertyDropAttribute attribute1 = property.GetCustomAttribute<OnEntityPropertyDropAttribute>();
                                    if (attribute1 != null)
                                    {
                                        RaiseProcessing(info.Table);
                                        if (sync)
                                            attribute1.Invoke(connection);
                                        else
                                            await attribute1.InvokeAsync(connection);
                                    }
                                }
                            }
                            if (dropColumns != null)
                            {
                                AlterTableQueryBuilder builder = connection.GetAlterTableQueryBuilder();
                                builder.SetTable(new TableDescriptor(info.Table), null, dropColumns.ToArray());
                                RaiseUpdate(info.Table);
                                foreach (string queryText in builder.GetQueries())
                                {
                                    using (SqlDbQuery query = connection.GetQuery(queryText))
                                    {
                                        if (sync)
                                            query.ExecuteNoData();
                                        else
                                            await query.ExecuteNoDataAsync();
                                    }
                                }
                            }
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
                    defaultUpdateMode == UpdateMode.Recreate ||
                    (individualUpdateModes != null && individualUpdateModes.ContainsKey(info.EntityType) && individualUpdateModes[info.EntityType] == UpdateMode.Recreate))
                {
                    using (EntityQuery create = connection.GetCreateEntityQuery(info.EntityType))
                    {
                        RaiseCreate(info.Table);
                        if (sync)
                            create.Execute();
                        else
                            await create.ExecuteAsync();
                    }
                    OnEntityCreateAttribute attribute = info.EntityType.GetTypeInfo().GetCustomAttribute<OnEntityCreateAttribute>();
                    if (attribute != null)
                    {
                        RaiseProcessing(info.Table);
                        if (sync)
                            attribute.Invoke(connection);
                        else
                            await attribute.InvokeAsync(connection);
                    }
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
                            OnEntityPropertyCreateAttribute attribute1 = column.PropertyAccessor.GetCustomAttribute<OnEntityPropertyCreateAttribute>();
                            if (attribute1 != null)
                                delegates.Add(attribute1);
                        }
                    }
                    if (addColumns != null)
                    {
                        AlterTableQueryBuilder builder = connection.GetAlterTableQueryBuilder();
                        builder.SetTable(new TableDescriptor(info.Table), addColumns.ToArray(), null);
                        RaiseUpdate(info.Table);
                        foreach (string queryText in builder.GetQueries())
                        {
                            using (SqlDbQuery query = connection.GetQuery(queryText))
                            {
                                if (sync)
                                    query.ExecuteNoData();
                                else
                                    await query.ExecuteNoDataAsync();
                            }
                        }
                        if (delegates != null)
                        {
                            RaiseProcessing(info.Table);
                            foreach (var action in delegates)
                            {
                                if (sync)
                                    action.Invoke(connection);
                                else
                                    await action.InvokeAsync(connection);
                            }
                        }
                    }
                }
            }

            foreach (EntityFinder.EntityTypeInfo info in mTypes.Where(info => info.View))
            {
                using (EntityQuery create = connection.GetCreateViewQuery(info.EntityType))
                {
                    RaiseCreate(info.Table);
                    if (sync)
                        create.Execute();
                    else
                        await create.ExecuteAsync();
                }

                OnEntityCreateAttribute attribute = info.EntityType.GetTypeInfo().GetCustomAttribute<OnEntityCreateAttribute>();
                if (attribute != null)
                {
                    RaiseProcessing(info.Table);
                    if (sync)
                        attribute.Invoke(connection);
                    else
                        await attribute.InvokeAsync(connection);
                }
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

        private void RaiseProcessing(string table)
        {
            if (OnAction != null)
            {
                CreateEntityControllerEventArgs args = new CreateEntityControllerEventArgs(CreateEntityControllerEventArgs.Action.Processing, table);
                OnAction.Invoke(this, args);
            }
        }
    }
}
