using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Gehtsoft.EF.Db.SqlDb.OData
{
    [XmlRoot("set")]
    public class XmlSerializableDictionary : Dictionary<string, object>, IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
#pragma warning disable RCS1079 // Throwing of new NotImplementedException.
            throw new NotImplementedException();
#pragma warning restore RCS1079 // Throwing of new NotImplementedException.
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            XmlSerializer valueSerializer;
            foreach (string key in this.Keys)
            {
                writer.WriteStartElement("item");
                writer.WriteStartAttribute("key");
                writer.WriteRaw(key);
                writer.WriteEndAttribute();
                object value = this[key];
                if (value != null)
                {
                    if (value is IEnumerable<object>)
                    {
                        valueSerializer = new XmlSerializer(typeof(XmlSerializableDictionary));
                        IEnumerable<object> list = value as IEnumerable<object>;
                        foreach (object value1 in list)
                            valueSerializer.Serialize(writer, value1);
                    }
                    else if (value is XmlSerializableDictionary)
                    {
                        valueSerializer = new XmlSerializer(typeof(XmlSerializableDictionary));
                        valueSerializer.Serialize(writer, value);
                    }
                    else
                    {
                        writer.WriteStartAttribute("value");
                        string result;
                        if (value is DateTime date)
                        {
                            result = date.ToString("s");
                        }
                        else
                        {
                            result = value.ToString().EscapeXml();
                        }
                        writer.WriteRaw(result);
                        writer.WriteEndAttribute();
                    }
                }
                writer.WriteEndElement();
            }
        }
    }
}