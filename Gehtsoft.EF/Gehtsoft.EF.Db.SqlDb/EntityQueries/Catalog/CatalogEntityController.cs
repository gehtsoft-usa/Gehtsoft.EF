using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.Catalog;
using Gehtsoft.EF.Db.SqlDb.Catalog.Diff;
using Gehtsoft.EF.Db.SqlDb.Catalog.Store;
using Gehtsoft.EF.Db.SqlDb.InstanceLock;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog
{
    /// <summary>
    /// Create/drop/update tables by diffing the declared entity model against the EF-owned schema
    /// <b>catalogue</b> (see <see cref="CatalogStore"/>), instead of introspecting the live database like
    /// <see cref="CreateEntityController"/> does. Mirrors that controller's surface, adding a DB
    /// <c>version</c> argument to the catalogue-writing entry points.
    ///
    /// <para>Version semantics: the version passed to <see cref="UpdateTables(SqlDbConnection, string, CreateEntityController.UpdateMode, IDictionary{Type, CreateEntityController.UpdateMode})"/>
    /// is the "last version at which each table has its current descriptor". After a successful run every
    /// live table carries that version, so it is the DB's single current version. A scope-level guard runs
    /// before any DDL: a lower version than the applied one is refused (regression); the same version with
    /// any model change is refused (the developer changed the model without bumping the version); the same
    /// version with no change is a clean no-op; a higher version applies the diff.</para>
    ///
    /// <para>v1 is <b>greenfield</b>: the catalogue is born with the database (first contact diffs the
    /// model against "nothing" - a full create). Migrating an existing, mismatched database onto the
    /// catalogue is a later phase; <see cref="CreateEntityController"/> stays as the introspection path
    /// until then.</para>
    ///
    /// <para>The whole read-guard-diff-apply runs under an <see cref="IDbInstanceLock"/> so concurrent
    /// processes cannot race the catalogue.</para>
    ///
    /// NOTE: Phase 3 through <b>increment 3</b> handles: table create; column add/drop; index reconcile
    /// (single-column Sorted, composite, JSON); the <c>OnEntity*</c> hooks; <c>Recreate</c> (drop+create,
    /// FK-guarded) and <c>CreateNew</c> (≡ <c>Update</c>, as in <see cref="CreateEntityController"/>);
    /// obsolete-entity drop → tombstone; and views (drop+recreate). A column <i>definition</i> change is
    /// refused (<see cref="EfExceptionCode.CatalogColumnAlterNotSupported"/>) and routed to a patch - there
    /// is deliberately no portable in-place column modify. Still to come: unique single-column index
    /// reconcile, geometry/spatial index (post-parity geo), dynamic properties, coded-patch replay, and
    /// the parity gate.
    /// </summary>
    public class CatalogEntityController
    {
        private readonly IEnumerable<Assembly> mAssemblies;
        private readonly string mScope;
        private readonly CatalogStore mStore = new CatalogStore();
        private readonly CatalogSerializer mSerializer = new CatalogSerializer();

        // Only the name (and FK/sorted flags) matter when reconstructing a dropped column for the ALTER
        // builder, so a shared throw-away descriptor is enough for the foreign-key marker.
        private static readonly TableDescriptor mDummyTable = new TableDescriptor("dummytable");

        /// <summary>
        /// How long to wait for the instance lock before failing. Defaults to 30 seconds.
        /// </summary>
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The lease duration for the portable lock fallback (null = the connection's default).
        /// </summary>
        public TimeSpan? LockLease { get; set; }

        /// <summary>
        /// The event raised when an action (create/drop/update) is performed on a table.
        /// </summary>
        public event EventHandler<CreateEntityControllerEventArgs> OnAction;

        [DocgenIgnore]
        public CatalogEntityController(Type findNearThisType, string scope = null) :
               this(findNearThisType.GetTypeInfo().Assembly, scope)
        {
        }

        /// <summary>
        /// Constructor to search entities in one assembly.
        /// </summary>
        public CatalogEntityController(Assembly entityAssembly, string scope = null) :
               this(new Assembly[] { entityAssembly }, scope)
        {
        }

        /// <summary>
        /// Constructor to search entities in multiple assemblies.
        /// </summary>
        public CatalogEntityController(IEnumerable<Assembly> assemblies, string scope = null)
        {
            mAssemblies = assemblies;
            mScope = scope;
        }

        private string NormalizedScope => mScope ?? string.Empty;

        // ---- the DDL action surface (injectable, so decision logic can be unit-tested without a DB) ----

        internal interface ICatalogControllerAction
        {
            void Create(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType);
            void Drop(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType);
            void AddColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns);
            void DropColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns);
            void CreateIndex(SqlDbConnection connection, TableDescriptor descriptor, CompositeIndex index);
            void DropIndex(SqlDbConnection connection, TableDescriptor descriptor, string logicalName);
            void CreateDynamicPropertiesTable(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType);
            void DropDynamicPropertiesTable(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType);
        }

        private sealed class CatalogControllerAction : ICatalogControllerAction
        {
            private static readonly Type gViewCreationMetata = typeof(IViewCreationMetadata);

            public void Create(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType)
            {
                EntityQuery query = null;
                try
                {
                    if (!entityType.View)
                        query = connection.GetCreateEntityQuery(entityType.EntityType);
                    else if (entityType.Metadata != null && gViewCreationMetata.IsAssignableFrom(entityType.Metadata))
                        query = connection.GetCreateViewQuery(entityType.EntityType);
                    query?.Execute();
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
                    else if (entityType.Metadata != null &&
                             gViewCreationMetata.IsAssignableFrom(entityType.Metadata) &&
                             connection.DoesObjectExist(entityType.Table, null, "view"))
                        query = connection.GetDropViewQuery(entityType.EntityType);
                    query?.Execute();
                }
                finally
                {
                    query?.Dispose();
                }
            }

            public void AddColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns)
            {
                var builder = connection.GetAlterTableQueryBuilder();
                builder.SetTable(td, columns, null);
                foreach (var queryText in builder.GetQueries())
                    using (var query = connection.GetQuery(queryText))
                        query.ExecuteNoData();
            }

            public void DropColumns(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType, TableDescriptor td, TableDescriptor.ColumnInfo[] columns)
            {
                var builder = connection.GetAlterTableQueryBuilder();
                builder.SetTable(td, null, columns);
                foreach (var queryText in builder.GetQueries())
                    using (var query = connection.GetQuery(queryText))
                        query.ExecuteNoData();
            }

            public void CreateIndex(SqlDbConnection connection, TableDescriptor descriptor, CompositeIndex index)
            {
                CreateIndexBuilder builder = connection.GetCreateIndexBuilder(descriptor, index);
                builder.PrepareQuery();
                if (string.IsNullOrEmpty(builder.Query))
                    return;
                using (var query = connection.GetQuery(builder))
                    query.ExecuteNoData();
            }

            public void DropIndex(SqlDbConnection connection, TableDescriptor descriptor, string logicalName)
            {
                using (var query = connection.GetQuery(connection.GetDropIndexBuilder(descriptor, logicalName)))
                    query.ExecuteNoData();
            }

            public void CreateDynamicPropertiesTable(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType)
            {
                TableDescriptor propsTable = AllEntities.Inst[entityType.EntityType].DynamicPropertiesTable;
                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(propsTable)))
                    query.ExecuteNoData();
            }

            public void DropDynamicPropertiesTable(SqlDbConnection connection, EntityFinder.EntityTypeInfo entityType)
            {
                // The entity no longer carries dynamic properties, so rebuild the fixed side-table
                // descriptor (name + layout) from the owner table to locate and drop it.
                TableDescriptor ownerTable = AllEntities.Inst[entityType.EntityType].TableDescriptor;
                TableDescriptor propsTable = DynamicPropertiesTableBuilder.Build(ownerTable, null);
                using (var query = connection.GetQuery(connection.GetDropTableBuilder(propsTable)))
                    query.ExecuteNoData();
            }
        }

        internal ICatalogControllerAction ActionController { get; set; } = new CatalogControllerAction();

        // ---- discovery (same rules as CreateEntityController) ----

        private EntityFinder.EntityTypeInfo[] LoadTypes(bool includeObsolete)
        {
            EntityFinder.EntityTypeInfo[] types = EntityFinder.FindEntities(mAssemblies, mScope, includeObsolete);
            foreach (EntityFinder.EntityTypeInfo type in types)
            {
                if (type.Table == null)
                {
                    var namingPolicy = (type.NamingPolicy == EntityNamingPolicy.Default ? AllEntities.Inst.NamingPolicy[type.Scope] : type.NamingPolicy);
                    type.Table = EntityNameConvertor.ConvertTableName(type.EntityType.Name, namingPolicy == EntityNamingPolicy.BackwardCompatibility ? EntityNamingPolicy.AsIs : namingPolicy);
                }
            }
            EntityFinder.ArrageEntities(types);
            return types;
        }

        private CatalogTableDto DesiredDto(EntityFinder.EntityTypeInfo info)
        {
            EntityDescriptor entityDescriptor = AllEntities.Inst[info.EntityType];
            TableDescriptor descriptor = entityDescriptor.TableDescriptor;

            List<CompositeIndex> composite = null;
            if (descriptor.Metadata is ICompositeIndexMetadata metadata)
            {
                composite = new List<CompositeIndex>();
                foreach (CompositeIndex index in metadata.Indexes)
                    composite.Add(index);
            }
            CatalogTableDto dto = mSerializer.FromDescriptor(descriptor, composite);
            dto.HasDynamicProperties = entityDescriptor.HasDynamicProperties;
            return dto;
        }

        // Invokes an OnEntity* action attribute on the target (entity type or added column), if present -
        // same mechanism as CreateEntityController.
        private static void InvokeAttribute<T>(object target, SqlDbConnection connection)
            where T : OnEntityActionAttribute
        {
            OnEntityActionAttribute attribute = null;
            if (target is EntityFinder.EntityTypeInfo typeInfo)
                attribute = typeInfo.EntityType.GetCustomAttribute<T>();
            else if (target is PropertyInfo propertyInfo)
                attribute = propertyInfo.GetCustomAttribute<T>();
            else if (target is TableDescriptor.ColumnInfo columnInfo)
                attribute = columnInfo.PropertyAccessor?.GetCustomAttribute<T>();

            attribute?.Invoke(connection);
        }

        // ---- version parsing / comparison (shares the EfPatch major.minor.patch scheme) ----

        private static long VersionKey(string version)
        {
            if (string.IsNullOrEmpty(version))
                return 0;
            string[] parts = version.Split('.');
            return (long)ParsePart(parts, 0) * 10000000 + (long)ParsePart(parts, 1) * 10000 + ParsePart(parts, 2);
        }

        private static int ParsePart(string[] parts, int index)
        {
            if (index >= parts.Length)
                return 0;
            if (!int.TryParse(parts[index], out int value))
                throw new ArgumentException($"Invalid version segment '{parts[index]}'");
            return value;
        }

        private static int CompareVersion(string a, string b) => VersionKey(a).CompareTo(VersionKey(b));

        private IDbInstanceLock AcquireLock(SqlDbConnection connection)
            => connection.AcquireInstanceLock("ef_catalog_update:" + NormalizedScope, LockTimeout, LockLease);

        // ---- public surface ----

        /// <summary>
        /// Creates all (non-view) tables unconditionally and records them in the catalogue at
        /// <paramref name="version"/>. Use for a fresh database.
        /// </summary>
        public void CreateTables(SqlDbConnection connection, string version)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            using (AcquireLock(connection))
            {
                mStore.EnsureBootstrapped(connection);
                EntityFinder.EntityTypeInfo[] types = LoadTypes(includeObsolete: false);
                foreach (EntityFinder.EntityTypeInfo info in types)
                {
                    if (info.View)
                        continue;
                    RaiseCreate(info.Table);
                    ActionController.Create(connection, info);
                    InvokeAttribute<OnEntityCreateAttribute>(info, connection);
                    mStore.WriteApplied(connection, mScope, info.Table, version, DesiredDto(info));
                }
            }
        }

        /// <summary>Creates tables (async version).</summary>
        public Task CreateTablesAsync(SqlDbConnection connection, string version) => Task.Run(() => CreateTables(connection, version));

        /// <summary>
        /// Drops all discovered (including obsolete) tables in reverse dependency order and tombstones
        /// them in the catalogue.
        /// </summary>
        public void DropTables(SqlDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            using (AcquireLock(connection))
            {
                mStore.EnsureBootstrapped(connection);
                EntityFinder.EntityTypeInfo[] types = LoadTypes(includeObsolete: true);
                string currentVersion = mStore.ReadCurrentVersion(connection, mScope);
                for (int i = types.Length - 1; i >= 0; i--)
                {
                    EntityFinder.EntityTypeInfo info = types[i];
                    InvokeAttribute<OnEntityDropAttribute>(info, connection);
                    RaiseDrop(info.Table);
                    ActionController.Drop(connection, info);
                    mStore.WriteTombstone(connection, mScope, info.Table, currentVersion);
                }
            }
        }

        /// <summary>Drops tables (async version).</summary>
        public Task DropTablesAsync(SqlDbConnection connection) => Task.Run(() => DropTables(connection));

        /// <summary>Update tables (async version).</summary>
        public Task UpdateTablesAsync(SqlDbConnection connection, string version, CreateEntityController.UpdateMode defaultUpdateMode, IDictionary<Type, CreateEntityController.UpdateMode> individualUpdateModes = null)
            => Task.Run(() => UpdateTables(connection, version, defaultUpdateMode, individualUpdateModes));

        /// <summary>
        /// Reconciles the scope to the declared model by diffing against the catalogue and applying the
        /// difference, then records the new state at <paramref name="version"/>. Obsolete entities are
        /// dropped and tombstoned; <c>Recreate</c> tables are dropped and recreated (guarded against
        /// breaking an active foreign key); views are dropped and recreated. <c>Update</c> and
        /// <c>CreateNew</c> behave identically (matching <see cref="CreateEntityController"/>, where only
        /// <c>Recreate</c> is a distinct mode). See the class remarks for the version guard.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="version">The DB version being applied.</param>
        /// <param name="defaultUpdateMode">The default update mode.</param>
        /// <param name="individualUpdateModes">Per-type update modes.</param>
        public void UpdateTables(SqlDbConnection connection, string version, CreateEntityController.UpdateMode defaultUpdateMode, IDictionary<Type, CreateEntityController.UpdateMode> individualUpdateModes = null)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            using (AcquireLock(connection))
            {
                mStore.EnsureBootstrapped(connection);
                EntityFinder.EntityTypeInfo[] types = LoadTypes(includeObsolete: true);

                // A table that is being dropped or recreated must not orphan an active foreign key from a
                // table that survives - same guard as CreateEntityController.
                foreach (EntityFinder.EntityTypeInfo info in types)
                {
                    if (info.View)
                        continue;
                    if (info.Obsolete || Mode(info, defaultUpdateMode, individualUpdateModes) == CreateEntityController.UpdateMode.Recreate)
                    {
                        EntityFinder.EntityTypeInfo dependent = FindActiveDependent(types, info, defaultUpdateMode, individualUpdateModes);
                        if (dependent != null)
                            throw new EfSqlException(EfExceptionCode.CannotRecreateTable, info.Table, dependent.Table);
                    }
                }

                IReadOnlyDictionary<string, CatalogTableDto> stored = mStore.ReadAppliedForScope(connection, mScope);
                string currentVersion = mStore.ReadCurrentVersion(connection, mScope);

                // Pre-pass: compute the per-table plan and whether the run changes anything (views excluded
                // - they are always recreated on apply and are not catalogued).
                var desiredByType = new Dictionary<Type, CatalogTableDto>();
                var changesByType = new Dictionary<Type, IReadOnlyList<CatalogChange>>();
                bool anyChange = false;
                foreach (EntityFinder.EntityTypeInfo info in types)
                {
                    if (info.View)
                        continue;
                    if (info.Obsolete)
                    {
                        if (stored.ContainsKey(info.Table))
                            anyChange = true;
                        continue;
                    }
                    CatalogTableDto desired = DesiredDto(info);
                    desiredByType[info.EntityType] = desired;
                    if (Mode(info, defaultUpdateMode, individualUpdateModes) == CreateEntityController.UpdateMode.Recreate)
                    {
                        anyChange = true;
                        continue;
                    }
                    stored.TryGetValue(info.Table, out CatalogTableDto storedDto);
                    IReadOnlyList<CatalogChange> changes = CatalogDiff.Compare(desired, storedDto);
                    changesByType[info.EntityType] = changes;
                    if (changes.Count > 0)
                        anyChange = true;
                }

                // Scope-level version guard, before any DDL.
                if (currentVersion != null)
                {
                    int cmp = CompareVersion(version, currentVersion);
                    if (cmp < 0)
                        throw new EfSqlException(EfExceptionCode.CatalogVersionRegressed, NormalizedScope, version ?? string.Empty, currentVersion);
                    if (cmp == 0)
                    {
                        if (anyChange)
                            throw new EfSqlException(EfExceptionCode.CatalogModelChangedWithoutVersionBump, NormalizedScope, version ?? string.Empty);
                        return; // clean, idempotent re-run
                    }
                }

                // Drop phase (reverse dependency order): obsolete tables are dropped+tombstoned; recreate
                // targets are dropped now and recreated below.
                for (int i = types.Length - 1; i >= 0; i--)
                {
                    EntityFinder.EntityTypeInfo info = types[i];
                    if (info.View || !stored.ContainsKey(info.Table))
                        continue;
                    if (info.Obsolete)
                    {
                        InvokeAttribute<OnEntityDropAttribute>(info, connection);
                        RaiseDrop(info.Table);
                        ActionController.Drop(connection, info);
                        mStore.WriteTombstone(connection, mScope, info.Table, version);
                    }
                    else if (Mode(info, defaultUpdateMode, individualUpdateModes) == CreateEntityController.UpdateMode.Recreate)
                    {
                        RaiseDrop(info.Table);
                        ActionController.Drop(connection, info);
                    }
                }

                // Create/update phase (dependency order), stamping every live table with the version.
                foreach (EntityFinder.EntityTypeInfo info in types)
                {
                    if (info.View || info.Obsolete)
                        continue;
                    CatalogTableDto desired = desiredByType[info.EntityType];
                    if (Mode(info, defaultUpdateMode, individualUpdateModes) == CreateEntityController.UpdateMode.Recreate)
                    {
                        RaiseCreate(info.Table);
                        ActionController.Create(connection, info);
                        InvokeAttribute<OnEntityCreateAttribute>(info, connection);
                        mStore.WriteApplied(connection, mScope, info.Table, version, desired);
                        continue;
                    }
                    IReadOnlyList<CatalogChange> changes = changesByType[info.EntityType];
                    if (changes.Count == 0)
                    {
                        mStore.AdvanceVersion(connection, mScope, info.Table, version);
                        continue;
                    }
                    ApplyChanges(connection, info, changes);
                    mStore.WriteApplied(connection, mScope, info.Table, version, desired);
                }

                // Views: always dropped and recreated (not diffed / not catalogued).
                foreach (EntityFinder.EntityTypeInfo info in types)
                {
                    if (!info.View)
                        continue;
                    if (info.Obsolete)
                    {
                        InvokeAttribute<OnEntityDropAttribute>(info, connection);
                        RaiseDrop(info.Table);
                        ActionController.Drop(connection, info);
                        continue;
                    }
                    ActionController.Drop(connection, info);
                    RaiseCreate(info.Table);
                    ActionController.Create(connection, info);
                    InvokeAttribute<OnEntityCreateAttribute>(info, connection);
                }
            }
        }

        private static CreateEntityController.UpdateMode Mode(EntityFinder.EntityTypeInfo info, CreateEntityController.UpdateMode defaultMode, IDictionary<Type, CreateEntityController.UpdateMode> individual)
        {
            if (individual != null && individual.TryGetValue(info.EntityType, out CreateEntityController.UpdateMode mode))
                return mode;
            return defaultMode;
        }

        // A surviving (non-obsolete, non-recreate) table that holds an active foreign key to the table
        // being dropped/recreated, or null if none - dropping would break its constraint.
        private static EntityFinder.EntityTypeInfo FindActiveDependent(EntityFinder.EntityTypeInfo[] types, EntityFinder.EntityTypeInfo target, CreateEntityController.UpdateMode defaultMode, IDictionary<Type, CreateEntityController.UpdateMode> individual)
        {
            foreach (EntityFinder.EntityTypeInfo other in types)
            {
                if (other == target || other.Obsolete || other.View)
                    continue;
                if (Mode(other, defaultMode, individual) == CreateEntityController.UpdateMode.Recreate)
                    continue;
                if (HasActiveForeignKey(other.EntityType, target.EntityType))
                    return other;
            }
            return null;
        }

        private static bool HasActiveForeignKey(Type dependentType, Type referencedType)
        {
            foreach (PropertyInfo property in dependentType.GetProperties())
            {
                if (property.PropertyType != referencedType)
                    continue;
                if (property.GetCustomAttribute<ObsoleteEntityPropertyAttribute>() != null)
                    continue;
                EntityPropertyAttribute attr = property.GetCustomAttribute<EntityPropertyAttribute>();
                if (attr != null && attr.ForeignKey)
                    return true;
            }
            return false;
        }

        private void ApplyChanges(SqlDbConnection connection, EntityFinder.EntityTypeInfo info, IReadOnlyList<CatalogChange> changes)
        {
            TableDescriptor descriptor = AllEntities.Inst[info.EntityType].TableDescriptor;
            var addColumns = new List<TableDescriptor.ColumnInfo>();
            var dropColumns = new List<CatalogColumnDto>();
            var addIndexes = new List<CompositeIndex>();
            var dropIndexes = new List<string>();
            bool addDynamicProperties = false;
            bool dropDynamicProperties = false;

            foreach (CatalogChange change in changes)
            {
                switch (change.Kind)
                {
                    case CatalogChangeKind.CreateTable:
                        // A CreateTable is exclusive: it carries the whole table (columns + its indexes).
                        RaiseCreate(info.Table);
                        ActionController.Create(connection, info);
                        InvokeAttribute<OnEntityCreateAttribute>(info, connection);
                        return;

                    case CatalogChangeKind.AddColumn:
                        addColumns.Add(ResolveColumn(descriptor, change.Column));
                        break;

                    case CatalogChangeKind.DropColumn:
                        dropColumns.Add(change.Column);
                        break;

                    case CatalogChangeKind.AlterColumn:
                        // No portable in-place column modify exists (a deliberate design choice, because
                        // column-change semantics diverge sharply per driver). Refuse and route the change
                        // through a coded patch, as CreateEntityController does for complex changes.
                        throw new EfSqlException(EfExceptionCode.CatalogColumnAlterNotSupported, info.Table, change.Column.Name);

                    case CatalogChangeKind.AddIndex:
                        addIndexes.Add(SingleColumnIndex(RequirePlainIndex(change)));
                        break;

                    case CatalogChangeKind.DropIndex:
                        dropIndexes.Add(RequirePlainIndex(change));
                        break;

                    case CatalogChangeKind.AddCompositeIndex:
                        addIndexes.Add(ResolveCompositeIndex(descriptor, change.IndexName));
                        break;

                    case CatalogChangeKind.DropCompositeIndex:
                        dropIndexes.Add(change.IndexName);
                        break;

                    case CatalogChangeKind.AddJsonIndex:
                        addIndexes.Add(ResolveJsonIndex(descriptor, change.ColumnName, change.IndexName));
                        break;

                    case CatalogChangeKind.DropJsonIndex:
                        dropIndexes.Add(change.IndexName);
                        break;

                    case CatalogChangeKind.AddDynamicPropertiesTable:
                        addDynamicProperties = true;
                        break;

                    case CatalogChangeKind.DropDynamicPropertiesTable:
                        dropDynamicProperties = true;
                        break;

                    default:
                        throw new NotSupportedException(
                            $"CatalogEntityController does not yet apply change kind {change.Kind}; geometry/spatial reconcile arrives with the post-parity geo increment.");
                }
            }

            bool changed = false;

            // The side table references the owner PK, so it is dropped first and created last.
            if (dropDynamicProperties)
            {
                ActionController.DropDynamicPropertiesTable(connection, info);
                changed = true;
            }

            // Fixed order matching the diff: drop indexes -> drop columns -> add columns -> add indexes.
            if (dropIndexes.Count > 0)
            {
                foreach (string logicalName in dropIndexes)
                    ActionController.DropIndex(connection, descriptor, logicalName);
                changed = true;
            }
            if (dropColumns.Count > 0)
            {
                var columnInfos = new TableDescriptor.ColumnInfo[dropColumns.Count];
                for (int i = 0; i < dropColumns.Count; i++)
                    columnInfos[i] = ReconstructDroppedColumn(dropColumns[i]);
                ActionController.DropColumns(connection, info, new TableDescriptor(info.Table), columnInfos);
                changed = true;
                foreach (CatalogColumnDto dropped in dropColumns)
                {
                    PropertyInfo property = FindObsoleteProperty(info, dropped.Name);
                    if (property != null)
                        InvokeAttribute<OnEntityPropertyDropAttribute>(property, connection);
                }
            }
            if (addColumns.Count > 0)
            {
                ActionController.AddColumns(connection, info, new TableDescriptor(info.Table), addColumns.ToArray());
                changed = true;
                foreach (TableDescriptor.ColumnInfo column in addColumns)
                    InvokeAttribute<OnEntityPropertyCreateAttribute>(column, connection);
            }
            if (addIndexes.Count > 0)
            {
                foreach (CompositeIndex index in addIndexes)
                    ActionController.CreateIndex(connection, descriptor, index);
                changed = true;
            }
            if (addDynamicProperties)
            {
                ActionController.CreateDynamicPropertiesTable(connection, info);
                changed = true;
            }

            if (changed)
                RaiseUpdate(info.Table);
        }

        // Single-column unique-index changes have no portable, uniqueness-preserving reconcile path
        // (CompositeIndex carries no uniqueness), so they are refused for now; the plain (Sorted) case
        // returns the column name that both names and keys the index.
        private static string RequirePlainIndex(CatalogChange change)
        {
            if (change.Unique)
                throw new NotSupportedException(
                    $"CatalogEntityController does not yet reconcile the unique single-column index on {change.TableName}.{change.ColumnName}; it arrives in a later increment.");
            return change.ColumnName;
        }

        private static CompositeIndex SingleColumnIndex(string columnName)
        {
            var index = new CompositeIndex(columnName);
            index.Add(columnName);
            return index;
        }

        // An added composite/JSON index is present in the model, so resolve the real CompositeIndex from
        // the descriptor rather than rebuilding it from the catalogued form.
        private static CompositeIndex ResolveCompositeIndex(TableDescriptor descriptor, string name)
        {
            if (descriptor.Metadata is ICompositeIndexMetadata metadata)
                foreach (CompositeIndex index in metadata.Indexes)
                    if (index.Name == name)
                        return index;
            throw new EfSqlException(EfExceptionCode.TypeIsUnsupported, name);
        }

        private static CompositeIndex ResolveJsonIndex(TableDescriptor descriptor, string columnName, string indexName)
        {
            foreach (TableDescriptor.ColumnInfo column in descriptor)
                if (column.Name == columnName && column.Json != null)
                    foreach (JsonIndexDefinition def in column.Json.Indexes)
                        if (def.Name == indexName)
                            return CompositeIndex.ForJson(def.Name, column.Name, def.Path, def.DbType);
            throw new EfSqlException(EfExceptionCode.TypeIsUnsupported, indexName);
        }

        // Resolves a diff column back to the model's ColumnInfo (matched by SQL name or property id).
        private static TableDescriptor.ColumnInfo ResolveColumn(TableDescriptor descriptor, CatalogColumnDto column)
        {
            foreach (TableDescriptor.ColumnInfo candidate in descriptor)
                if (candidate.Name == column.Name || candidate.ID == column.Id)
                    return candidate;
            throw new EfSqlException(EfExceptionCode.TypeIsUnsupported, column.Name);
        }

        // A dropped column is gone from the model, so rebuild the little the ALTER builder needs from the
        // catalogued form: the SQL name, the sorted flag, and whether it carried a foreign key.
        private static TableDescriptor.ColumnInfo ReconstructDroppedColumn(CatalogColumnDto column)
            => new TableDescriptor.ColumnInfo
            {
                Name = column.Name,
                Sorted = column.Sorted,
                ForeignTable = column.ForeignTable != null ? mDummyTable : null,
            };

        // Finds the [ObsoleteEntityProperty] property whose field maps to a dropped column, so its
        // OnEntityPropertyDrop hook can fire (the column is not in the model's descriptor any more).
        private PropertyInfo FindObsoleteProperty(EntityFinder.EntityTypeInfo info, string columnName)
        {
            EntityNamingPolicy policy = info.NamingPolicy == EntityNamingPolicy.Default
                ? AllEntities.Inst.NamingPolicy[mScope]
                : info.NamingPolicy;
            foreach (PropertyInfo property in info.EntityType.GetProperties())
            {
                ObsoleteEntityPropertyAttribute attribute = property.GetCustomAttribute<ObsoleteEntityPropertyAttribute>();
                if (attribute == null)
                    continue;
                string field = attribute.Field ?? EntityNameConvertor.ConvertName(property.Name, policy);
                if (string.Equals(field, columnName, StringComparison.OrdinalIgnoreCase))
                    return property;
            }
            return null;
        }

        private void RaiseCreate(string table) => Raise(CreateEntityControllerEventArgs.Action.Create, table);

        private void RaiseDrop(string table) => Raise(CreateEntityControllerEventArgs.Action.Drop, table);

        private void RaiseUpdate(string table) => Raise(CreateEntityControllerEventArgs.Action.Update, table);

        private void Raise(CreateEntityControllerEventArgs.Action action, string table)
        {
            if (OnAction != null)
                OnAction.Invoke(this, new CreateEntityControllerEventArgs(action, table));
        }
    }
}
