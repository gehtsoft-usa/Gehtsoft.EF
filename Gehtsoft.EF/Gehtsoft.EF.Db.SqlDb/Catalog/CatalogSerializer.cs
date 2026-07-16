using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.Catalog
{
    /// <summary>
    /// Maps the runtime <see cref="TableDescriptor"/> into the catalogue <see cref="CatalogTableDto"/>
    /// and (de)serializes the versioned <see cref="CatalogSnapshot"/> envelope as plain JSON text.
    ///
    /// Reading is schema-tolerant: missing members take their declared defaults and an explicit
    /// upgrade-on-read chain applies deliberate older-to-current transforms. There is no forward
    /// tolerance - a blob written by a newer, more capable framework is parsed but flagged
    /// (<see cref="CatalogSnapshot.IsNewerThanSupported"/>) so the update process refuses to act on it.
    /// </summary>
    public sealed class CatalogSerializer
    {
        /// <summary>
        /// This build's schema-format capability version. Bump it (by one) whenever the framework
        /// learns a new DDL-relevant construct that changes what the catalogue must record.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>This build's maximum supported schema-format version (the refuse-gate input).</summary>
        public int CurrentSchemaFormatVersion => CurrentVersion;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        /// <summary>
        /// Serializes a snapshot to JSON text, always stamping this build's
        /// <see cref="CurrentVersion"/> regardless of the version carried by
        /// <paramref name="snapshot"/>.
        /// </summary>
        /// <param name="snapshot">The snapshot to serialize.</param>
        public string Serialize(CatalogSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            CatalogSnapshot envelope = new CatalogSnapshot
            {
                SchemaFormatVersion = CurrentVersion,
                Table = snapshot.Table,
            };
            return JsonSerializer.Serialize(envelope, Options);
        }

        /// <summary>
        /// Deserializes a snapshot from JSON text, applying upgrade-on-read for older versions and
        /// flagging a newer-than-supported blob. Never partially applies a newer shape: unknown
        /// members are ignored and the consumer must consult
        /// <see cref="CatalogSnapshot.IsNewerThanSupported"/> before acting.
        /// </summary>
        /// <param name="json">The catalogue blob.</param>
        public CatalogSnapshot Deserialize(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            CatalogSnapshot snapshot = JsonSerializer.Deserialize<CatalogSnapshot>(json, Options);
            if (snapshot == null)
                throw new ArgumentException("The catalogue blob is empty or null-valued.", nameof(json));
            if (snapshot.Table == null)
                snapshot.Table = new CatalogTableDto();

            UpgradeOnRead(snapshot);
            snapshot.IsNewerThanSupported = snapshot.SchemaFormatVersion > CurrentVersion;
            return snapshot;
        }

        // Applies deliberate older-to-current transforms, oldest first. v1 is the first format
        // version, so there is nothing to upgrade yet; absent members are covered by member defaults.
        // Future transforms add a case per superseded version here (falling through to current).
        private static void UpgradeOnRead(CatalogSnapshot snapshot)
        {
            switch (snapshot.SchemaFormatVersion)
            {
                default:
                    break;
            }
        }

        /// <summary>
        /// Maps a runtime table descriptor and its composite indexes into the catalogue DTO.
        /// </summary>
        /// <param name="table">The table descriptor.</param>
        /// <param name="compositeIndexes">The composite indexes declared on the table (may be `null`/empty).</param>
        public CatalogTableDto FromDescriptor(TableDescriptor table, IReadOnlyList<CompositeIndex> compositeIndexes)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            CatalogTableDto dto = new CatalogTableDto
            {
                Name = table.Name,
                Scope = table.Scope,
                View = table.View,
                Obsolete = table.Obsolete,
            };

            for (int i = 0; i < table.Count; i++)
                dto.Columns.Add(MapColumn(table[i]));

            if (compositeIndexes != null)
                for (int i = 0; i < compositeIndexes.Count; i++)
                    dto.CompositeIndexes.Add(MapCompositeIndex(compositeIndexes[i]));

            return dto;
        }

        private static CatalogColumnDto MapColumn(TableDescriptor.ColumnInfo column)
        {
            return new CatalogColumnDto
            {
                Id = column.ID,
                Name = column.Name,
                DbType = column.DbType.ToString(),
                Size = column.Size,
                Precision = column.Precision,
                PrimaryKey = column.PrimaryKey,
                Autoincrement = column.Autoincrement,
                Sorted = column.Sorted,
                Unique = column.Unique,
                Nullable = column.Nullable,
                ForeignTable = column.ForeignTable?.Name,
                IgnoreRead = column.IgnoreRead,
                Default = MapDefault(column.DefaultValue),
                Geometry = MapGeometry(column.Geometry),
                Json = MapJson(column.Json),
            };
        }

        private static CatalogColumnDefault MapDefault(object value)
        {
            if (value == null)
                return null;
            return new CatalogColumnDefault
            {
                TypeName = value.GetType().Name,
                Value = FormatInvariant(value),
            };
        }

        // Formats a supported primitive default with the invariant culture (round-trippable text).
        private static string FormatInvariant(object value)
        {
            switch (value)
            {
                case DateTime dt:
                    return dt.ToString("o", CultureInfo.InvariantCulture);
                case Guid g:
                    return g.ToString("D");
                case IFormattable f:
                    return f.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static CatalogGeometryDto MapGeometry(GeometryColumnMetadata geometry)
        {
            if (geometry == null)
                return null;

            CatalogGeometryDto dto = new CatalogGeometryDto
            {
                Srid = geometry.Srid,
                Subtype = geometry.Subtype.ToString(),
                HasZ = geometry.HasZ,
                HasM = geometry.HasM,
                Nullable = geometry.Nullable,
            };

            IReadOnlyList<SpatialIndexDefinition> indexes = geometry.Indexes;
            for (int i = 0; i < indexes.Count; i++)
            {
                SpatialIndexDefinition source = indexes[i];
                dto.Indexes.Add(new CatalogSpatialIndexDto
                {
                    Name = source.Name,
                    HasBoundingBox = source.HasBoundingBox,
                    MinX = source.MinX,
                    MinY = source.MinY,
                    MaxX = source.MaxX,
                    MaxY = source.MaxY,
                    Tolerance = source.Tolerance,
                });
            }
            return dto;
        }

        private static CatalogJsonDto MapJson(JsonColumnMetadata json)
        {
            if (json == null)
                return null;

            CatalogJsonDto dto = new CatalogJsonDto
            {
                ClrType = json.ClrType?.FullName,
            };

            IReadOnlyList<JsonIndexDefinition> indexes = json.Indexes;
            for (int i = 0; i < indexes.Count; i++)
            {
                JsonIndexDefinition source = indexes[i];
                dto.Indexes.Add(new CatalogJsonIndexDto
                {
                    Name = source.Name,
                    Path = source.Path,
                    DbType = source.DbType.ToString(),
                    Unique = source.Unique,
                });
            }
            return dto;
        }

        private static CatalogCompositeIndexDto MapCompositeIndex(CompositeIndex index)
        {
            CatalogCompositeIndexDto dto = new CatalogCompositeIndexDto
            {
                Name = index.Name,
            };

            if (index.ExcludeFor != null)
            {
                dto.ExcludeFor = new List<string>();
                for (int i = 0; i < index.ExcludeFor.Length; i++)
                    dto.ExcludeFor.Add(index.ExcludeFor[i]);
            }

            IReadOnlyList<CompositeIndex.Field> fields = index.Fields;
            for (int i = 0; i < fields.Count; i++)
            {
                CompositeIndex.Field field = fields[i];
                dto.Fields.Add(new CatalogCompositeIndexFieldDto
                {
                    Name = field.Name,
                    Function = field.Function?.ToString(),
                    Direction = field.Direction.ToString(),
                    JsonPath = field.JsonPath,
                    JsonType = field.JsonPath == null ? null : field.JsonType.ToString(),
                });
            }
            return dto;
        }
    }
}
