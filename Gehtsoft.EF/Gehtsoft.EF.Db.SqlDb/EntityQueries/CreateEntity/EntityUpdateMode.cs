namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// <para>The mode of a table update operation.</para>
    /// <para>Shared by [clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog.CatalogEntityController]CatalogEntityController[/clink]
    /// and the obsolete [clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntityController]CreateEntityController[/clink].</para>
    /// </summary>
    public enum EntityUpdateMode
    {
        /// <summary>
        /// Drop and re-create the table.
        /// </summary>
        Recreate,

        /// <summary>
        /// Create new tables and update existing ones (add/drop columns, reconcile indexes).
        /// </summary>
        Update,

        /// <summary>
        /// Only create tables that do not exist yet.
        /// </summary>
        CreateNew,
    }
}
