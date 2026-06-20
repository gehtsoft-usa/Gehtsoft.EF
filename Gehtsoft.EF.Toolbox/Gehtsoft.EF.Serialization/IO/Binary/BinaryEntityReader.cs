using System;
using System.IO;
using System.Text;
using System.Threading;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Serialization.IO.Binary
{
    /// <summary>
    /// Reads entities from the stream produced by <see cref="BinaryEntityWriter"/> and
    /// raises <see cref="OnTypeStarted"/> / <see cref="OnEntity"/> as they are decoded.
    /// Records identify their type by EF scope and table name, matched against the
    /// supplied <paramref name="types"/> (as with <see cref="Db.DbEntityReader"/>).
    /// </summary>
    public sealed class BinaryEntityReader : IEntityReader, IDisposable
    {
        public event TypeStartedDelegate OnTypeStarted;
        public event EntityDelegate OnEntity;

        private BinaryReader mReader;
        private readonly bool mOwnsReader;
        private readonly CancellationToken? mCancellationToken;
        private readonly EntityTypeResolver mResolver;

        public BinaryEntityReader(EntityFinder.EntityTypeInfo[] types, Stream stream, CancellationToken? token = null)
            : this(types, new BinaryReader(stream ?? throw new ArgumentNullException(nameof(stream)), Encoding.UTF8, true), true, token)
        {
        }

        public BinaryEntityReader(EntityFinder.EntityTypeInfo[] types, BinaryReader reader, bool ownsReader = false, CancellationToken? token = null)
        {
            mResolver = new EntityTypeResolver(types);
            mReader = reader ?? throw new ArgumentNullException(nameof(reader));
            mOwnsReader = ownsReader;
            mCancellationToken = token;
        }

        public void Scan()
        {
            if (mReader == null)
                throw new InvalidOperationException("Object is already disposed");

            EntityDescriptor descriptor = null;
            TableDescriptor.ColumnInfo[] columns = null;
            Stream stream = mReader.BaseStream;

            while (stream.Position < stream.Length)
            {
                if (mCancellationToken != null && ((CancellationToken)mCancellationToken).IsCancellationRequested)
                    return;

                byte marker = mReader.ReadByte();
                if (marker == BinaryEntityWriter.EndMarker)
                    return;

                if (marker == BinaryEntityWriter.TypeMarker)
                {
                    string scope = mReader.ReadString();
                    string name = mReader.ReadString();
                    descriptor = mResolver.Resolve(scope, name);
                    int count = mReader.ReadInt32();
                    columns = new TableDescriptor.ColumnInfo[count];
                    for (int i = 0; i < count; i++)
                        columns[i] = descriptor[mReader.ReadString()];
                    OnTypeStarted?.Invoke(descriptor.EntityType);
                }
                else if (marker == BinaryEntityWriter.EntityMarker)
                {
                    if (descriptor == null)
                        throw new InvalidOperationException("Entity record encountered before any type record");

                    object entity = EntityMaterializer.CreateInstance(descriptor);
                    for (int i = 0; i < columns.Length; i++)
                    {
                        object raw = BinaryFormatter.Read(mReader);
                        EntityMaterializer.Assign(entity, columns[i], raw);
                    }
                    OnEntity?.Invoke(entity);
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected marker {marker} in the binary stream");
                }
            }
        }

        public void Dispose()
        {
            if (mOwnsReader)
                mReader?.Dispose();
            mReader = null;
        }
    }
}
