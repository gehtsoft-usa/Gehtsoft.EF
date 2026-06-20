using System;
using System.IO;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Text.Json;

namespace Gehtsoft.EF.Serialization.IO.Json
{
    /// <summary>
    /// Serializes entities to JSON. Each type is written as an object carrying its EF scope
    /// and table name plus an array of entities; each entity is an object mapping column name
    /// to a { "t": typecode, "v": value } pair (scalars encoded via <see cref="TextFormatter"/>,
    /// blobs via <see cref="BlobAccessor"/>).
    /// </summary>
    public sealed class JsonEntityWriter : IEntityWriter, IDisposable
    {
        public const string TypesProperty = "es";
        public const string ScopeProperty = "scope";
        public const string NameProperty = "name";
        public const string EntitiesProperty = "entities";
        public const string TypeCodeProperty = "t";
        public const string ValueProperty = "v";

        public IBlobAccessor BlobAccessor { get; set; } = new Base64BlobAccessor();

        private Utf8JsonWriter mWriter;
        private readonly bool mOwnsWriter;
        private EntityDescriptor mDescriptor;
        private bool mTypeOpen;

        public JsonEntityWriter(Stream stream, JsonWriterOptions options = default)
            : this(new Utf8JsonWriter(stream ?? throw new ArgumentNullException(nameof(stream)), options), true)
        {
        }

        public JsonEntityWriter(Utf8JsonWriter writer, bool ownsWriter = false)
        {
            mWriter = writer ?? throw new ArgumentNullException(nameof(writer));
            mOwnsWriter = ownsWriter;
            if (mOwnsWriter)
                mWriter.WriteStartObject();
            mWriter.WriteStartArray(TypesProperty);
        }

        public void Start(Type type)
        {
            if (mWriter == null)
                throw new InvalidOperationException("Object is already disposed");
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            CloseType();

            mDescriptor = AllEntities.Inst[type];
            mWriter.WriteStartObject();
            mWriter.WriteString(ScopeProperty, mDescriptor.TableDescriptor.Scope ?? string.Empty);
            mWriter.WriteString(NameProperty, mDescriptor.TableDescriptor.Name);
            mWriter.WriteStartArray(EntitiesProperty);
            mTypeOpen = true;
        }

        public void Write(object entity)
        {
            if (mWriter == null)
                throw new InvalidOperationException("Object is already disposed");
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (!mTypeOpen)
                throw new InvalidOperationException("Type isn't started");
            if (mDescriptor.EntityType != entity.GetType())
                throw new ArgumentException("The entity is not an entity of the type that is currently started", nameof(entity));

            mWriter.WriteStartObject();
            foreach (TableDescriptor.ColumnInfo column in mDescriptor.TableDescriptor)
            {
                object value = column.PropertyAccessor.GetValue(entity);
                if (column.ForeignKey && value != null)
                    value = column.ForeignTable.PrimaryKey.PropertyAccessor.GetValue(value);

                mWriter.WriteStartObject(column.ID);
                if (value == null)
                {
                    mWriter.WriteString(TypeCodeProperty, "n");
                }
                else if (value is byte[] bytes)
                {
                    mWriter.WriteString(TypeCodeProperty, "l");
                    mWriter.WriteString(ValueProperty, BlobAccessor.Save(bytes));
                }
                else
                {
                    TextFormatter.Format(value, out string v, out string t);
                    mWriter.WriteString(TypeCodeProperty, t);
                    if (v != null)
                        mWriter.WriteString(ValueProperty, v);
                }
                mWriter.WriteEndObject();
            }
            mWriter.WriteEndObject();
        }

        private void CloseType()
        {
            if (mTypeOpen)
            {
                mWriter.WriteEndArray();  // entities
                mWriter.WriteEndObject(); // type
                mTypeOpen = false;
            }
        }

        public void Dispose()
        {
            if (mWriter != null)
            {
                CloseType();
                mWriter.WriteEndArray(); // es
                if (mOwnsWriter)
                    mWriter.WriteEndObject(); // root
                mWriter.Flush();
                if (mOwnsWriter)
                    mWriter.Dispose();
                mWriter = null;
            }
        }
    }
}
