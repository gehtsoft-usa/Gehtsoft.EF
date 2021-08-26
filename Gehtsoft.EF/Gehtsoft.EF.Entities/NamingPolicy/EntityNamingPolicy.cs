namespace Gehtsoft.EF.Entities
{
#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved
    /// <summary>
    /// The naming policies for the table and column names.
    ///
    /// The policy defines how the name will be converted.
    ///
    /// Use <see cref="Gehtsoft.EF.Db.SqlDb.EntityQueries.AllEntities.NamingPolicy">AllEntities.NamingPolicy</see> to set the naming
    /// policy.
    /// </summary>
    public enum EntityNamingPolicy
    {
        /// <summary>
        /// Default naming policy.
        ///
        /// The default policy is to use the names as is.
        ///
        /// The last word in the entity name is considered an English noun and
        /// converted to proper plural form, e.g. the entity class `Apple`
        /// will have table name `Apples`.
        /// </summary>
        Default,
        /// <summary>
        /// The naming policy compatible with pre 1.2 version of EF library.
        /// In this naming policy the table name is an entity name as is and
        /// the column name is a lower case entity name.
        /// </summary>
        BackwardCompatibility,
        /// <summary>
        /// The table and column names are used as is.
        /// </summary>
        AsIs,
        /// <summary>
        /// The table and column names are converted to lowercase.
        /// </summary>
        LowerCase,
        /// <summary>
        /// The table and column names are converted to uppercase.
        /// </summary>
        UpperCase,
        /// <summary>
        /// The table and column names have first character converted to lower case and the rest used as is.
        /// </summary>
        LowerFirstCharacter,
        /// <summary>
        /// The table and column names have first character converted to upper case and the rest used as is.
        /// </summary>
        UpperFirstCharacter,
        /// <summary>
        /// The table and column names are converted to lower case. In all locations where there the case changed, an underscore will be added.
        ///
        /// For example the name `LongComplexName` will be changed to `long_complex_name`.
        /// </summary>
        LowerCaseWithUnderscores,
        /// <summary>
        /// The table and column names are converted to lower case. In all locations where there the case changed, an underscore will be added.
        ///
        /// For example the name `LongComplexName` will be changed to `LONG_COMPLEX_NAME`.
        /// </summary>
        UpperCaseWithUnderscopes,
    }
}
