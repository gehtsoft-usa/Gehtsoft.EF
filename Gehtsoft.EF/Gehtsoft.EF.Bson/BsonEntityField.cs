using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Utils;
using MongoDB.Bson;

namespace Gehtsoft.EF.Bson
{
    [DocgenIgnore]
    [ExcludeFromCodeCoverage]
    public class BsonEntityField
    {
        public string PropertyName { get; internal set; }
        public string FieldName { get; internal set; }
        public IPropertyAccessor PropertyAccessor { get; internal set; }
        public Type PropertyType { get; internal set; }
        public Type PropertyElementType { get; internal set; }
        public BsonType PropertyBsonElementType { get; internal set; }
        public BsonType PropertyBsonType { get; internal set; }
        public bool IsArray { get; internal set; }
        public bool IsNullable { get; internal set; }
        public bool IsSorted { get; internal set; }
        public bool IsAutoId { get; internal set; }
        public bool IsPrimaryKey { get; internal set; }
        public bool IsReference { get; set; }
        public BsonEntityDescription ReferencedEntity { get; set; }
        public string Column => FieldName ?? PropertyAccessor.Name;
    }
}