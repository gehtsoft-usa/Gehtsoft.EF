using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Serialization.IO.Json
{
    /// <summary>
    /// Reads entities from the JSON produced by <see cref="JsonEntityWriter"/>. The input is
    /// buffered and parsed forward-only with <see cref="Utf8JsonReader"/>; types are resolved
    /// by EF scope and table name against the supplied <paramref name="types"/>.
    /// </summary>
    public sealed class JsonEntityReader : IEntityReader, IDisposable
    {
        public event TypeStartedDelegate OnTypeStarted;
        public event EntityDelegate OnEntity;

        public IBlobAccessor BlobAccessor { get; set; } = new Base64BlobAccessor();

        private byte[] mBytes;
        private readonly EntityTypeResolver mResolver;
        private readonly CancellationToken? mCancellationToken;

        public JsonEntityReader(EntityFinder.EntityTypeInfo[] types, Stream stream, CancellationToken? token = null)
            : this(types, ReadAll(stream ?? throw new ArgumentNullException(nameof(stream))), token)
        {
        }

        public JsonEntityReader(EntityFinder.EntityTypeInfo[] types, string json, CancellationToken? token = null)
            : this(types, Encoding.UTF8.GetBytes(json ?? throw new ArgumentNullException(nameof(json))), token)
        {
        }

        public JsonEntityReader(EntityFinder.EntityTypeInfo[] types, byte[] utf8Json, CancellationToken? token = null)
        {
            mResolver = new EntityTypeResolver(types);
            mBytes = utf8Json ?? throw new ArgumentNullException(nameof(utf8Json));
            mCancellationToken = token;
        }

        private static byte[] ReadAll(Stream stream)
        {
            if (stream is MemoryStream ms)
                return ms.ToArray();
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }

        public void Scan()
        {
            if (mBytes == null)
                throw new InvalidOperationException("Object is already disposed");

            var reader = new Utf8JsonReader(mBytes);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                throw new InvalidOperationException("Malformed JSON: root object expected");

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.GetString() == JsonEntityWriter.TypesProperty)
                {
                    ReadTypesArray(ref reader);
                    return;
                }
                reader.Read();
                reader.Skip();
            }
        }

        private void ReadTypesArray(ref Utf8JsonReader reader)
        {
            reader.Read(); // StartArray
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new InvalidOperationException("Malformed JSON: type array expected");

            while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                if (!ReadType(ref reader))
                    return;
            }
        }

        private bool ReadType(ref Utf8JsonReader reader)
        {
            string scope = null;
            EntityDescriptor descriptor = null;

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                string property = reader.GetString();
                if (property == JsonEntityWriter.ScopeProperty)
                {
                    reader.Read();
                    scope = reader.GetString();
                }
                else if (property == JsonEntityWriter.NameProperty)
                {
                    reader.Read();
                    descriptor = mResolver.Resolve(scope, reader.GetString());
                    OnTypeStarted?.Invoke(descriptor.EntityType);
                }
                else if (property == JsonEntityWriter.EntitiesProperty)
                {
                    if (!ReadEntities(ref reader, descriptor))
                        return false;
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }
            return true;
        }

        private bool ReadEntities(ref Utf8JsonReader reader, EntityDescriptor descriptor)
        {
            reader.Read(); // StartArray
            while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                object entity = EntityMaterializer.CreateInstance(descriptor);

                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    TableDescriptor.ColumnInfo column = descriptor[reader.GetString()];

                    reader.Read(); // StartObject of the { t, v } pair
                    string typeCode = null;
                    string value = null;
                    while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string key = reader.GetString();
                        reader.Read();
                        if (key == JsonEntityWriter.TypeCodeProperty)
                            typeCode = reader.GetString();
                        else if (key == JsonEntityWriter.ValueProperty)
                            value = reader.GetString();
                    }

                    object raw;
                    if (typeCode == "n")
                        raw = null;
                    else if (typeCode == "l")
                        raw = BlobAccessor.Load(value);
                    else
                        raw = TextFormatter.Parse(typeCode, value);

                    EntityMaterializer.Assign(entity, column, raw);
                }

                OnEntity?.Invoke(entity);

                if (mCancellationToken != null && ((CancellationToken)mCancellationToken).IsCancellationRequested)
                    return false;
            }
            return true;
        }

        public void Dispose()
        {
            mBytes = null;
        }
    }
}
