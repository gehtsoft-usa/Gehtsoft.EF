using System.Text.Json.Serialization;

namespace Gehtsoft.EF.Db.SqlDb.Catalog
{
    /// <summary>
    /// The versioned envelope persisted in the catalogue: a monotonic
    /// <see cref="SchemaFormatVersion"/> plus one table's <see cref="CatalogTableDto"/>.
    ///
    /// The version is the framework's schema-format capability version, bumped whenever the framework
    /// learns a new DDL-relevant construct. It exists so the update process (the consumer) can enforce
    /// the safety gate: an older updater must REFUSE to touch a database whose catalogue was written by
    /// a more capable framework version. <see cref="CatalogSerializer.Deserialize"/> sets
    /// <see cref="IsNewerThanSupported"/> for exactly that check.
    /// </summary>
    public sealed class CatalogSnapshot
    {
        /// <summary>
        /// The schema-format version carried by the blob. On serialize the current build's version is
        /// always written; on deserialize this is the version read from the blob.
        /// </summary>
        public int SchemaFormatVersion { get; set; }

        /// <summary>The table payload.</summary>
        public CatalogTableDto Table { get; set; }

        /// <summary>
        /// Set by <see cref="CatalogSerializer.Deserialize"/> when the blob's
        /// <see cref="SchemaFormatVersion"/> exceeds this build's
        /// <see cref="CatalogSerializer.CurrentSchemaFormatVersion"/>. The consumer must refuse to
        /// apply DDL in that case. Not part of the serialized form.
        /// </summary>
        [JsonIgnore]
        public bool IsNewerThanSupported { get; set; }
    }
}
