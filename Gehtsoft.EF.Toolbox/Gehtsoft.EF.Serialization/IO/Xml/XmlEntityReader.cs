using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Serialization.IO.Xml
{
    public class XmlEntityReader : IDisposable, IEntityReader
    {
        public string RootElementName { get; set; } = "es";
        public string TypeElementName { get; set; } = "t";
        public string EntityElementName { get; set; } = "e";
        public string PropertyElementName { get; set; } = "p";
        public string NameAttributeName { get; set; } = "n";
        public string IDAttributeName { get; set; } = "i";
        public string TypeAttributeName { get; set; } = "t";
        public string EncodedAttributeName { get; set; } = "u";
        public IBlobAccessor BlobAccessor { get; set; } = new Base64BlobAccessor();
        public int MaximumPropertiesPerEntity 
        { 
            get { return mCurrentProperties.Length; }
            set
            {
                mCurrentProperties = new string[value];
                mCurrentValues = new object[value];
                mColumns = new TableDescriptor.ColumnInfo[value];
            }
        }

        public event TypeStartedDelegate OnTypeStarted;
        public event EntityDelegate OnEntity;

        private CancellationToken? mCancellationToken;


        private XmlReader mReader;
        private readonly bool mIsNewReader;

        public XmlEntityReader(Stream reader, XmlReaderSettings settings = null, CancellationToken? token = null) : this(settings == null ? XmlReader.Create(reader) : XmlReader.Create(reader, settings), true, token)
        {

        }

        public XmlEntityReader(string buffer, CancellationToken? token = null) : this(XmlReader.Create(new StringReader(buffer)), true, token)
        {

        }

        public XmlEntityReader(XmlReader reader, bool isNewReader = false, CancellationToken? token = null)
        {
            mCancellationToken = token;
            mReader = reader;
            mIsNewReader = isNewReader;
            if (isNewReader)
                reader.MoveToContent();
        }

        public void Dispose()
        {
            if (mIsNewReader)
                mReader?.Dispose();
            mReader = null;
        }

        private EntityDescriptor mCurrentType = null;
        private string[] mCurrentProperties = new string[32];
        private TableDescriptor.ColumnInfo[] mColumns = new TableDescriptor.ColumnInfo[32];
        private object[] mCurrentValues = new object[32];
        private bool mInEntity = false;

        public class Element
        {
            public string Name { get; set; }
            public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
            public StringBuilder Text { get; } = new StringBuilder();
            public bool Notified { get; set; } = false;
            public bool IsEmpty { get; set; }
        }

        private object mReturnEntity = null;
        
        private void ElementStarted(Element element)
        {
            if (element.Name == TypeElementName)
            {
                string typeName = element.Attributes[TypeAttributeName];
                if (element.Attributes.ContainsKey(EncodedAttributeName))
                    typeName = Encoding.UTF8.GetString(Convert.FromBase64String(typeName));
                Type entityType = Type.GetType(typeName);
                mCurrentType = AllEntities.Inst[entityType];
                mInEntity = false;
                for (int i = 0; i < mCurrentProperties.Length; i++)
                {
                    mCurrentProperties[i] = null;
                    mColumns[i] = null;
                }
                OnTypeStarted?.Invoke(entityType);
            }
            else if (element.Name == EntityElementName)
            {
                for (int i = 0; i < mCurrentProperties.Length; i++)
                    mCurrentValues[i] = null;
                mInEntity = true;
            }
        }

        private void ElementEnded(Element element)
        {
            if (element.Name == TypeElementName)
            {
                mCurrentType = null;
            }
            else if (element.Name == PropertyElementName)
            {
                if (mInEntity)
                {
                    string sid = element.Attributes[IDAttributeName];
                    int id = Int32.Parse(sid);
                    if (id >= mCurrentProperties.Length)
                        throw new IndexOutOfRangeException($"Enitity contains too many properties {id}. Increase MaximumPropertiesPerEntity value.");
                    string type = element.Attributes[TypeElementName];
                    bool encoded = element.Attributes.ContainsKey(EncodedAttributeName);
                    string text = element.Text.ToString();
                    if (encoded)
                        text = Encoding.UTF8.GetString(Convert.FromBase64String(text));

                    if (type == "l")
                        mCurrentValues[id] = BlobAccessor.Load(text);
                    else if (mColumns[id].ForeignKey)
                    {
                        if (type == "n")
                            mCurrentValues[id] = null;
                        else
                            mCurrentValues[id] = TextFormatter.ParseAndConvert(type, text, mColumns[id].ForeignTable.PrimaryKey.PropertyAccessor.PropertyType);
                    }
                    else
                        mCurrentValues[id] = TextFormatter.ParseAndConvert(type, text, mColumns[id].PropertyAccessor.PropertyType);
                }
                else
                {
                    string sid = element.Attributes[IDAttributeName];
                    int id = Int32.Parse(sid);
                    if (id >= mCurrentProperties.Length)
                        throw new IndexOutOfRangeException($"Enitity contains too many properties {id}. Increase MaximumPropertiesPerEntity value.");
                    string name = element.Attributes[NameAttributeName];
                    mCurrentProperties[id] = name;
                    mColumns[id] = mCurrentType[name];
                }
            }
            else if (element.Name == EntityElementName)
            {
                mReturnEntity = Activator.CreateInstance(mCurrentType.EntityType);
                for (int i = 0; i < mCurrentType.TableDescriptor.Count; i++)
                {
                    if (mCurrentType.TableDescriptor[i].DefaultValue != null)
                        mCurrentType.TableDescriptor[i].PropertyAccessor.SetValue(mReturnEntity, mCurrentType.TableDescriptor[i].DefaultValue);
                }
                for (int i = 0; i < mCurrentValues.Length; i++)
                {
                    if (mCurrentProperties[i] != null)
                    {
                        TableDescriptor.ColumnInfo column = mColumns[i] ?? mCurrentType[mCurrentProperties[i]];
                        if (column.ForeignKey)
                        {
                            if (mCurrentValues[i] == null)
                                column.PropertyAccessor.SetValue(mReturnEntity, null);
                            else
                            {
                                EntityDescriptor foreignDescriptor = AllEntities.Inst[column.PropertyAccessor.PropertyType];
                                object refValue = Activator.CreateInstance(foreignDescriptor.EntityType);
                                foreignDescriptor.PrimaryKey.PropertyAccessor.SetValue(refValue, mCurrentValues[i]);
                                column.PropertyAccessor.SetValue(mReturnEntity, refValue);
                            }
                        }
                        else
                        {
                            column.PropertyAccessor.SetValue(mReturnEntity, mCurrentValues[i]);
                        }
                    }
                }
                mInEntity = false;
            }
        }

        Stack<Element> mStack = new Stack<Element>();

        public void Scan()
        {
            while (mReader.Read())
            {
                if (mCancellationToken != null && ((CancellationToken) mCancellationToken).IsCancellationRequested)
                    return;

                switch (mReader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (mStack.Count > 0 && !mStack.Peek().Notified)
                        {
                            ElementStarted(mStack.Peek());
                            mStack.Peek().Notified = true;
                            if (mStack.Peek().IsEmpty)
                                ElementEnded(mStack.Pop());
                        }
                        Element element = new Element();
                        element.Name = mReader.Name;
                        element.Notified = false;
                        for (int i = 0; i < mReader.AttributeCount; i++)
                        {
                            mReader.MoveToAttribute(i);
                            element.Attributes[mReader.Name] = mReader.Value;
                        }
                        mReader.MoveToElement();
                        element.IsEmpty = mReader.IsEmptyElement;
                        mStack.Push(element);
                        break;
                    case XmlNodeType.EndElement:
                        if (mStack.Count == 0)
                            return ;
                        ElementEnded(mStack.Pop());
                        if (mReturnEntity != null)
                        {
                            OnEntity?.Invoke(mReturnEntity);
                            mReturnEntity = null;
                        }
                        break;
                    case XmlNodeType.CDATA:
                        mStack.Peek().Text.Append(mReader.Value);
                        break;
                    case XmlNodeType.Text:
                        mStack.Peek().Text.Append(mReader.Value);
                        break;
                    case XmlNodeType.Whitespace:
                        mStack.Peek().Text.Append(mReader.Value);
                        break;
                    case XmlNodeType.EntityReference:
                        mStack.Peek().Text.Append(mReader.Value);
                        break;
                }
            }
            return ;
        }
    }
}
