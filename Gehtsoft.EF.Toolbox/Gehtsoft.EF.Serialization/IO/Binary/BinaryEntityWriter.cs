using System;
using System.IO;
using System.Reflection;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Serialization.IO.Binary
{
    /// <summary>
    /// Serializes entities into a compact, self-describing binary stream. Each type is
    /// emitted as a header (assembly-qualified type name + ordered column names) followed
    /// by its entities; blobs are stored inline (no <see cref="IBlobAccessor"/> needed).
    /// </summary>
    public sealed class BinaryEntityWriter : IEntityWriter, IDisposable
    {
        internal const byte TypeMarker = 1;
        internal const byte EntityMarker = 2;
        internal const byte EndMarker = 0;

        private BinaryWriter mWriter;
        private readonly bool mOwnsWriter;
        private EntityDescriptor mDescriptor;

        public BinaryEntityWriter(Stream stream)
            : this(new BinaryWriter(stream ?? throw new ArgumentNullException(nameof(stream)), Encoding.UTF8, true), true)
        {
        }

        public BinaryEntityWriter(BinaryWriter writer, bool ownsWriter = false)
        {
            mWriter = writer ?? throw new ArgumentNullException(nameof(writer));
            mOwnsWriter = ownsWriter;
        }

        public void Start(Type type)
        {
            if (mWriter == null)
                throw new InvalidOperationException("Object is already disposed");
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            mDescriptor = AllEntities.Inst[type];
            mWriter.Write(TypeMarker);
            mWriter.Write(mDescriptor.TableDescriptor.Scope ?? string.Empty);
            mWriter.Write(mDescriptor.TableDescriptor.Name);
            mWriter.Write(mDescriptor.TableDescriptor.Count);
            foreach (TableDescriptor.ColumnInfo column in mDescriptor.TableDescriptor)
                mWriter.Write(column.ID);
        }

        public void Write(object entity)
        {
            if (mWriter == null)
                throw new InvalidOperationException("Object is already disposed");
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (mDescriptor == null)
                throw new InvalidOperationException("Type isn't started");
            if (mDescriptor.EntityType != entity.GetType())
                throw new ArgumentException("The entity is not an entity of the type that is currently started", nameof(entity));

            mWriter.Write(EntityMarker);
            foreach (TableDescriptor.ColumnInfo column in mDescriptor.TableDescriptor)
            {
                object value = column.PropertyAccessor.GetValue(entity);
                if (column.ForeignKey && value != null)
                    value = column.ForeignTable.PrimaryKey.PropertyAccessor.GetValue(value);
                BinaryFormatter.Write(mWriter, value);
            }
        }

        public void Dispose()
        {
            if (mWriter != null)
            {
                mWriter.Write(EndMarker);
                mWriter.Flush();
                if (mOwnsWriter)
                    mWriter.Dispose();
                mWriter = null;
            }
        }
    }
}
