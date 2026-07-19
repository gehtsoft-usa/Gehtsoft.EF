using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// <para>Obsolete pass-through to [clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog.CatalogEntityController]CatalogEntityController[/clink]; use that controller instead.</para>
    /// <para>[clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog.CatalogEntityController]CatalogEntityController[/clink]
    /// reconciles the entity model against an EF-owned schema catalogue instead of introspecting the live
    /// database. This type is retained only as a source-compatible pass-through to the internal
    /// introspection-based implementation, so existing callers keep compiling. New code should not use it.</para>
    /// </summary>
    [Obsolete("Use CatalogEntityController, which reconciles the model against an EF-owned schema catalogue instead of introspecting the live database. CreateEntityController is kept only as a source-compatible pass-through.")]
    public class CreateEntityController
    {
        private readonly CreateEntityControllerInternal mInner;

        /// <summary>
        /// <para>The mode of update operation.</para>
        /// <para>Prefer [clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.EntityUpdateMode]EntityUpdateMode[/clink]
        /// with [clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog.CatalogEntityController]CatalogEntityController[/clink].</para>
        /// </summary>
        public enum UpdateMode
        {
            /// <summary>Drop and create table.</summary>
            Recreate,
            /// <summary>Create new tables, update tables where columns are added or dropped.</summary>
            Update,
            /// <summary>Only creates new tables.</summary>
            CreateNew,
        }

        /// <summary>
        /// The event raised when an action (create/drop/update) is performed on a table.
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
        public CreateEntityController(Assembly entityAssembly, string scope = null) :
               this(new Assembly[] { entityAssembly }, scope)
        {
        }

        /// <summary>
        /// Constructor to search entities in multiple assemblies.
        /// </summary>
        public CreateEntityController(IEnumerable<Assembly> assemblies, string scope = null)
        {
            mInner = new CreateEntityControllerInternal(assemblies, scope);
            mInner.OnAction += (sender, args) => OnAction?.Invoke(this, args);
        }

        /// <summary>Drop tables.</summary>
        public void DropTables(SqlDbConnection connection) => mInner.DropTables(connection);

        /// <summary>Drop tables (async version).</summary>
        public Task DropTablesAsync(SqlDbConnection connection) => mInner.DropTablesAsync(connection);

        /// <summary>Creates tables.</summary>
        public void CreateTables(SqlDbConnection connection) => mInner.CreateTables(connection);

        /// <summary>Creates tables (async version).</summary>
        public Task CreateTablesAsync(SqlDbConnection connection) => mInner.CreateTablesAsync(connection);

        /// <summary>Update tables.</summary>
        public void UpdateTables(SqlDbConnection connection, UpdateMode defaultUpdateMode, IDictionary<Type, UpdateMode> individualUpdateModes = null, bool failIfUpdateNeeded = false)
            => mInner.UpdateTables(connection, Map(defaultUpdateMode), Map(individualUpdateModes), failIfUpdateNeeded);

        /// <summary>Update tables (async version).</summary>
        public Task UpdateTablesAsync(SqlDbConnection connection, UpdateMode defaultUpdateMode, IDictionary<Type, UpdateMode> individualUpdateModes = null, bool failIfUpdateNeeded = false)
            => mInner.UpdateTablesAsync(connection, Map(defaultUpdateMode), Map(individualUpdateModes), failIfUpdateNeeded);

        private static EntityUpdateMode Map(UpdateMode mode)
        {
            switch (mode)
            {
                case UpdateMode.Recreate:
                    return EntityUpdateMode.Recreate;
                case UpdateMode.CreateNew:
                    return EntityUpdateMode.CreateNew;
                default:
                    return EntityUpdateMode.Update;
            }
        }

        private static IDictionary<Type, EntityUpdateMode> Map(IDictionary<Type, UpdateMode> modes)
        {
            if (modes == null)
                return null;
            var result = new Dictionary<Type, EntityUpdateMode>();
            foreach (KeyValuePair<Type, UpdateMode> kv in modes)
                result[kv.Key] = Map(kv.Value);
            return result;
        }
    }
}
