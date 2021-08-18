using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public delegate void EntityActionDelegate(SqlDbConnection connection);
    public delegate Task EntityActionAsyncDelegate(SqlDbConnection connection);

    public class CreateEntityControllerEventArgs : EventArgs
    {
        public enum Action
        {
            Create,
            Drop,
            Update,
            Processing,
        }

        public Action EventAction { get; set; }

        public string Table { get; set; }

        public CreateEntityControllerEventArgs(Action action, string table)
        {
            EventAction = action;
            Table = table;
        }
    }

    public delegate void CreateEntityControllerEventDelegate(object sender, CreateEntityControllerEventArgs args);

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
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

    [AttributeUsage(AttributeTargets.Class)]
    public class OnEntityCreateAttribute : OnEntityActionAttribute
    {
        public OnEntityCreateAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class OnEntityDropAttribute : OnEntityActionAttribute
    {
        public OnEntityDropAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class OnEntityPropertyCreateAttribute : OnEntityActionAttribute
    {
        public OnEntityPropertyCreateAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class OnEntityPropertyDropAttribute : OnEntityActionAttribute
    {
        public OnEntityPropertyDropAttribute(Type containerType, string delegateName) : base(containerType, delegateName)
        {
        }
    }

    public class CreateEntityController
    {
        private readonly IEnumerable<Assembly> mAssemblies;
        private readonly string mScope;
        private EntityFinder.EntityTypeInfo[] mTypes;

        public event EventHandler<CreateEntityControllerEventArgs> OnAction;

        public CreateEntityController(Type findNearThisType, string scope = null) :
               this(findNearThisType.GetTypeInfo().Assembly, scope)
        {
        }

        public CreateEntityController(Assembly entityAssembly, string scope = null) :
               this(new Assembly[] { entityAssembly }, scope)
        {
        }

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

        public void DropTables(SqlDbConnection connection) => DropTablesCore(connection, false).GetAwaiter().GetResult();

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

        public void CreateTables(SqlDbConnection connection) => CreateTablesCore(connection, false).GetAwaiter().GetResult();

        public Task CreateTablesAsync(SqlDbConnection connection) => CreateTablesCore(connection, true).AsTask();

        public enum UpdateMode
        {
            Recreate,
            Update,
            CreateNew,
        }

        private readonly static TableDescriptor mDummyTable = new TableDescriptor("dummytable");

        public Task UpdateTablesAsync(SqlDbConnection connection, UpdateMode defaultUpdateMode, IDictionary<Type, UpdateMode> individualUpdateModes = null)
            => UpdateTablesCore(false, connection, defaultUpdateMode, individualUpdateModes);
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
