using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Bson
{
    public class BsonEntityDescription
    {
        public Type EntityType { get; internal set; }
        public string Table { get; internal set; }
        public BsonEntityField[] Fields { get; internal set; }
        public Dictionary<string, BsonEntityField> FieldsIndex { get; } = new Dictionary<string, BsonEntityField>();
        public BsonEntityField PrimaryKey { get; internal set; }
    }
}

