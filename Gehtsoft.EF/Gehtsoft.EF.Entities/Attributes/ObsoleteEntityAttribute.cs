using System;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The attribute to mark an obsolete entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ObsoleteEntityAttribute : Attribute
    {
        /// <summary>
        /// The name of the entity scope.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// The name of the table name.
        ///
        /// If no name is set, the entity table name will be
        /// created according applicable <see cref="NamingPolicy"/>.
        /// </summary>
        public string Table { get; set; }

        /// <summary>
        /// The naming policy.
        ///
        /// If no naming policy is explicitly set,
        /// the naming policy associated with the scope or
        /// default naming policy will be used.
        ///
        /// The default naming policy is defined via [clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.AllEntities.NamingPolicy.R38]AllEntities.NamingPolicy[/clink].
        /// </summary>
        public EntityNamingPolicy NamingPolicy { get; set; } = EntityNamingPolicy.Default;

        /// <summary>
        /// The flag indicating whether the entity is associated with a view.
        ///
        /// If the flag is `true` the entity is a view.
        ///
        /// If the flag is `false` (default) the entity is a table.
        /// </summary>
        public bool View { get; set; }

        /// <summary>
        /// The optional metadata object.
        ///
        /// The metadata object may be used to provide additional information, for example:
        /// * The metadata implements [clink=Gehtsoft.EF.Db.SqlDb.Metadata.ICompositeIndexMetadata]ICompositeIndexMetadata[/clink]
        ///   to provide information about composite indexes.
        /// * The metadata implements [clink=Gehtsoft.EF.Db.SqlDb.Metadata.IViewCreationMetadata]IViewCreationMetadata[/clink]
        ///   to provide select query for automatic view creation.
        /// </summary>
        public Type Metadata { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ObsoleteEntityAttribute() : base()
        {
            Scope = null;
        }
    }
}
