using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// Codes for `EfSqlException`
    /// </summary>
    public enum EfExceptionCode
    {
        NotEntity,
        NoPrimaryKeyInTable,
        NoTableToConnect,
        NoColumnToConnect,
        NoTableInQuery,
        InvalidOperation,
        PropertyNotFound,
        ColumnNotFound,
        IncorrectJoin,
        NestingTransactionsNotSupported,
        FeatureNotSupported,
        WhereBracketIsEmpty,
        WrongOperator,
        UnknownOperator,
        TypeIsUnsupported,
        CannotRecreateTable,
        DynamicPropertiesBagIsNotNew,
        DynamicPropertiesBagIsNew,
        DynamicPropertiesAttributeWithoutOwner,
        DynamicPropertiesOwnerWithoutAttribute,
        GeometryCodecNotFound,
        LockTimeout,
        CatalogFormatTooNew,
        CatalogModelChangedWithoutVersionBump,
        CatalogVersionRegressed,
        CatalogColumnAlterNotSupported,
        CatalogOrphanPatchHistory,
        SchemaUpdateRequired,
        CatalogScopeAlreadyAdopted,
        CatalogOrphanScope,
        CatalogColumnDropWouldLoseData,
        CatalogDynamicPropertiesDropWouldLoseData,
    }

    [ExcludeFromCodeCoverage]
    internal class EfExceptionMessages
    {
        public static EfExceptionMessages Inst { get; } = new EfExceptionMessages();

        public string this[EfExceptionCode code]
        {
            get
            {
                switch (code)
                {
                    case EfExceptionCode.NoPrimaryKeyInTable:
                        return "The table {0} has no primary key";

                    case EfExceptionCode.NoTableToConnect:
                        return "Cannot find a table in the query to connect the table specified";

                    case EfExceptionCode.NoColumnToConnect:
                        return "Cannot find a column to connect the table specified";

                    case EfExceptionCode.NoTableInQuery:
                        return "The table requested is not found in the query";

                    case EfExceptionCode.NotEntity:
                        return "Type {0} is not an entity";

                    case EfExceptionCode.ColumnNotFound:
                        return "The column or property {0} is not found";

                    case EfExceptionCode.PropertyNotFound:
                        return "The property {0} is not found";

                    case EfExceptionCode.IncorrectJoin:
                        return "The join operation requested is incorrect";

                    case EfExceptionCode.InvalidOperation:
                        return "Operation requested is not supported";

                    case EfExceptionCode.NestingTransactionsNotSupported:
                        return "Nesting transactions aren't supported";

                    case EfExceptionCode.FeatureNotSupported:
                        return "Requested feature isn't supported";

                    case EfExceptionCode.WhereBracketIsEmpty:
                        return "Bracket group in where is empty";

                    case EfExceptionCode.WrongOperator:
                        return "The incorrect operator is chosen for this argument";

                    case EfExceptionCode.UnknownOperator:
                        return "The operator is unknown or unsupported by the target platform";

                    case EfExceptionCode.TypeIsUnsupported:
                        return "The data type {0} isn't supported";

                    case EfExceptionCode.CannotRecreateTable:
                        return "Cannot recreate table {0} because table {1} depends on it and is not set to be dropped or recreated";

                    case EfExceptionCode.DynamicPropertiesBagIsNotNew:
                        return "The dynamic properties bag must be a new bag to be inserted (create it with InitializeDynamicProperties on a new entity)";
                    case EfExceptionCode.DynamicPropertiesBagIsNew:
                        return "The dynamic properties bag is a new bag; a new bag can only be inserted, not updated (load the entity's dynamic properties before updating)";

                    case EfExceptionCode.DynamicPropertiesAttributeWithoutOwner:
                        return "The entity {0} is marked with [DynamicProperties] but does not implement IDynamicPropertiesOwner. Add the interface and a 'public DynamicPropertyBag DynamicProperties {{ get; private set; }}' property, or remove the attribute.";

                    case EfExceptionCode.DynamicPropertiesOwnerWithoutAttribute:
                        return "The entity {0} implements IDynamicPropertiesOwner but is not marked with [DynamicProperties]. Add the [DynamicProperties] attribute, or remove the interface.";

                    case EfExceptionCode.GeometryCodecNotFound:
                        return "No registered geometry codec can handle the property type {0}. Reference the Gehtsoft.EF.Geo.NetTopologySuite module (or register a codec via GeometryCodecs.Factory), or declare the property as byte[] (WKB).";

                    case EfExceptionCode.LockTimeout:
                        return "Could not acquire the instance lock {0} within the timeout";

                    case EfExceptionCode.CatalogFormatTooNew:
                        return "The catalogue entry for table {0} was written in schema format version {1}, which is newer than this build supports ({2}). Refusing to touch the database; upgrade the framework.";

                    case EfExceptionCode.CatalogModelChangedWithoutVersionBump:
                        return "The entity model differs from the catalogued schema for scope {0}, but the DB version {1} was not changed. Bump the version passed to UpdateTables when the model changes.";

                    case EfExceptionCode.CatalogVersionRegressed:
                        return "The DB version {1} passed to UpdateTables for scope {0} is older than the version {2} already applied. Refusing to reconcile a newer database with an older build.";

                    case EfExceptionCode.CatalogColumnAlterNotSupported:
                        return "The definition of column {1} on table {0} changed, which cannot be altered in place portably. Apply the change with an IEfPatch (structural convergence handles the rest).";

                    case EfExceptionCode.CatalogOrphanPatchHistory:
                        return "Scope {0} has no catalogue yet but an existing patch history (last applied {1}). The database was managed before the catalogue; adopt the scope first (AdoptExistingScope) instead of reconciling it as greenfield.";

                    case EfExceptionCode.SchemaUpdateRequired:
                        return "The database schema for table {0} does not match the model, but updates were disallowed (failIfUpdateNeeded). Reconcile the schema or use the ReconcileToModel adoption mode.";

                    case EfExceptionCode.CatalogScopeAlreadyAdopted:
                        return "Scope {0} is already catalogued (current version {1}); AdoptExistingScope only seeds a scope that has no catalogue yet.";

                    case EfExceptionCode.CatalogOrphanScope:
                        return "Scope {0} has no catalogue yet but its tables already exist. The database was managed before the catalogue; adopt the scope first (AdoptExistingScope) instead of updating it as greenfield.";

                    case EfExceptionCode.CatalogColumnDropWouldLoseData:
                        return "Updating table {0} would drop column {1}, which is not marked [ObsoleteEntityProperty]; this would lose its data. Mark the property [ObsoleteEntityProperty], set the controller's DataLossPolicy to Drop, or handle it with an IEfPatch.";

                    case EfExceptionCode.CatalogDynamicPropertiesDropWouldLoseData:
                        return "Updating table {0} would drop its dynamic-properties side table, losing all dynamic-property values. Set the controller's DataLossPolicy to Drop to allow it, or handle the migration with an IEfPatch.";

                    default:
                        return $"Unknown exception {code}";
                }
            }
        }
    }

    /// <summary>
    /// EF exception
    /// </summary>
    [Serializable]
    [ExcludeFromCodeCoverage]
    public class EfSqlException : Exception
    {
        /// <summary>
        /// The code of the error
        /// </summary>
        public EfExceptionCode ErrorCode { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="code"></param>
        /// <param name="args"></param>
        public EfSqlException(EfExceptionCode code, params object[] args) : base(string.Format(EfExceptionMessages.Inst[code], args))
        {
            ErrorCode = code;
        }

        protected EfSqlException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}