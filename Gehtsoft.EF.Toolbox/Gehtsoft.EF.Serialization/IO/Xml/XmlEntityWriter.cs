using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Serialization.IO.Xml
{
    public class XmlEntityWriter : IEntityWriter, IDisposable
    {
        private XmlWriter mWriter;
        private bool mIsNewWriter;
        
        private bool mTypeStarted;
        private bool mEntityStarted;

        private EntityDescriptor mDescriptor;

        public string RootElementName { get; set; } = "es";
        public string TypeElementName { get; set; } = "t";
        public string EntityElementName { get; set; } = "e";
        public string PropertyElementName { get; set; } = "p";
        public string NameAttributeName { get; set; } = "n";
        public string IDAttributeName { get; set; } = "i";
        public string TypeAttributeName { get; set; } = "t";
        public string EncodedAttributeName { get; set; } = "u";
        public IBlobAccessor BlobAccessor { get; set; } = new Base64BlobAccessor();

        public XmlEntityWriter(StringWriter writer, XmlWriterSettings settings = null) : this(settings == null ? XmlWriter.Create(writer) : XmlWriter.Create(writer, settings), true)
        {

        }

        public XmlEntityWriter(StringBuilder builder, XmlWriterSettings settings = null) : this(settings == null ? XmlWriter.Create(builder) : XmlWriter.Create(builder, settings), true)
        {

        }

        public XmlEntityWriter(XmlWriter writer, bool isNewWriter = false)
        {
            mWriter = writer;
            mIsNewWriter = isNewWriter;
            if (mIsNewWriter)
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(RootElementName);
            }
        }

        public void Dispose()
        {
            if (mWriter != null)
            {
                if (mEntityStarted)
                {
                    mWriter.WriteEndElement();
                    mEntityStarted = false;
                }

                if (mTypeStarted)
                {
                    mWriter.WriteEndElement();
                    mTypeStarted = false;
                }

                if (mIsNewWriter)
                {
                    mWriter.WriteEndElement(); //end root
                    mWriter?.Dispose();
                }
            }

            mWriter = null;
        }

        public void Start(Type type)
        {
            if (mWriter == null)
                throw new InvalidOperationException("Object is already disposed");

            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (mEntityStarted)
            {
                mWriter.WriteEndElement();
                mEntityStarted = false;
            }

            if (mTypeStarted)
            {
                mWriter.WriteEndElement();
                mTypeStarted = false;
            }

            mDescriptor = AllEntities.Inst[type];
            mWriter.WriteStartElement(TypeElementName);
            string v = type.GetTypeInfo().AssemblyQualifiedName;
            if (v.IndexOfAny(XMLCHARS) >= 0)
            {
                mWriter.WriteAttributeString(EncodedAttributeName, "t");
                v = Convert.ToBase64String(Encoding.UTF8.GetBytes(v));
            }
            mWriter.WriteAttributeString(TypeAttributeName, v);
            int id = 0;
            foreach (TableDescriptor.ColumnInfo column in mDescriptor.TableDescriptor)
            {
                mWriter.WriteStartElement(PropertyElementName);
                mWriter.WriteAttributeString(IDAttributeName, id.ToString());
                mWriter.WriteAttributeString(NameAttributeName, column.ID);
                mWriter.WriteEndElement();
                id++;
            }           
            mTypeStarted = true;
        }

        private static readonly char[] XMLCHARS = new char[] {'<', '>', '&', '/', '\'', '\"', '\r', '\n'};

        public void Write(object entity)
        {
            if (mWriter == null)
                throw new InvalidOperationException("Object is already disposed");

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!mTypeStarted)
                throw new InvalidOperationException("Type isn't started");

            if (mDescriptor.EntityType != entity.GetType())
                throw new ArgumentException("The entity is not an entity of the type that is currently started", nameof(entity));

            if (mEntityStarted)
            {
                mWriter.WriteEndElement();
                mEntityStarted = false;
            }

            mWriter.WriteStartElement(EntityElementName);
            mEntityStarted = true;

            int id = 0;
            foreach (TableDescriptor.ColumnInfo column in mDescriptor.TableDescriptor)
            {
                object value = column.PropertyAccessor.GetValue(entity);
                if (column.ForeignKey && value != null)
                    value = column.ForeignTable.PrimaryKey.PropertyAccessor.GetValue(value);
                string t, v;

                if (value is byte[] bytes)
                {
                    t = "l";
                    v = BlobAccessor.Save(bytes);
                }
                else
                    TextFormatter.Format(value, out v, out t);

                mWriter.WriteStartElement(PropertyElementName);
                mWriter.WriteAttributeString(IDAttributeName, id.ToString());
                mWriter.WriteAttributeString(TypeAttributeName, t);
                if (v.IndexOfAny(XMLCHARS) >= 0)
                {
                    mWriter.WriteAttributeString(EncodedAttributeName, "t");
                    v = Convert.ToBase64String(Encoding.UTF8.GetBytes(v));
                }
                if (v != null)
                    mWriter.WriteString(v);
                mWriter.WriteEndElement();
                id++;
            }
        }
    }
}
