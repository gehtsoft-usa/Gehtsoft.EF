using System;
using System.Collections.Generic;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Catalog;
using Gehtsoft.EF.Db.SqlDb.Catalog.Store;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Test.Catalog
{
    /// <summary>
    /// Shared helpers for tests that drive <see cref="CatalogEntityController"/> on a live or in-memory
    /// database.
    /// </summary>
    internal static class CatalogTestSupport
    {
        private static readonly CatalogSerializer Serializer = new CatalogSerializer();

        /// <summary>
        /// Resets the catalogue to a clean slate: drops <c>ef_catalog</c> then re-creates the infrastructure.
        /// This is needed on a shared live database because <c>DropTables</c> only appends tombstones
        /// (append-only history), so catalogue state otherwise persists between tests. Safe because the test
        /// assembly disables parallelization, so no other class touches <c>ef_catalog</c> concurrently.
        /// On a fresh in-memory database the drop is skipped and only the infrastructure is created.
        /// </summary>
        public static SqlDbConnection ResetCatalog(SqlDbConnection connection, Assembly assembly)
        {
            if (connection.DoesObjectExist("ef_catalog", null, "table"))
                using (var drop = connection.GetDropEntityQuery<EfCatalogRecord>())
                    drop.Execute();
            new CatalogEntityController(assembly).EnsureCatalogInfrastructure(connection);
            return connection;
        }

        /// <summary>
        /// Seeds the catalogue with the schema shape of <paramref name="modelType"/> under
        /// (<paramref name="scope"/>, <paramref name="table"/>) at <paramref name="version"/>. Use it to
        /// establish the "before" state of an incremental migration: a later
        /// <c>UpdateTables</c> on that scope then diffs the current model against this recorded shape.
        /// </summary>
        public static void Seed(SqlDbConnection connection, string scope, string table, Type modelType, string version)
        {
            EntityDescriptor ed = AllEntities.Inst[modelType];
            TableDescriptor descriptor = ed.TableDescriptor;

            List<CompositeIndex> composite = null;
            if (descriptor.Metadata is ICompositeIndexMetadata metadata)
            {
                composite = new List<CompositeIndex>();
                foreach (CompositeIndex index in metadata.Indexes)
                    composite.Add(index);
            }

            CatalogTableDto dto = Serializer.FromDescriptor(descriptor, composite);
            dto.HasDynamicProperties = ed.HasDynamicProperties;
            dto.Scope = scope;
            new CatalogStore().WriteApplied(connection, scope, table, version, dto);
        }
    }
}
