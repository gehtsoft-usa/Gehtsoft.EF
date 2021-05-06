using System;
using System.Collections.Generic;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;

namespace Gehtsoft.EF.Bson
{
    public static class AllEntitiesExtension
    {
        public static BsonEntityDescription FindType(this AllEntities entities, Type type)
        {
            EntityDescriptor descriptor = entities[type];
            BsonEntityDescription description = descriptor.GetTag<BsonEntityDescription>();
            if (description == null)
            {
                description = CreateBsonEntityDescription(descriptor);
                descriptor.SetTag(description);
            }
            return description;
        }

        private static BsonEntityDescription CreateBsonEntityDescription(EntityDescriptor descriptor)
        {
            BsonEntityDescription description = new BsonEntityDescription
            {
                EntityType = descriptor.EntityType,
                Table = descriptor.TableDescriptor.Name
            };

            List<BsonEntityField> fields = new List<BsonEntityField>(descriptor.TableDescriptor.Count);

            for (int i = 0; i < descriptor.TableDescriptor.Count; i++)
            {
                TableDescriptor.ColumnInfo columnInfo = descriptor.TableDescriptor[i];
                BsonEntityField field = new BsonEntityField()
                {
                    FieldName = columnInfo.Name,
                    IsAutoId = columnInfo.Autoincrement && columnInfo.PrimaryKey,
                    IsPrimaryKey = columnInfo.PrimaryKey,
                    PropertyAccessor = columnInfo.PropertyAccessor,
                    IsSorted = columnInfo.Sorted,
                    IsNullable = columnInfo.Nullable,
                };

                Type propertyType = field.PropertyAccessor.PropertyType;
                Type elementType = propertyType;
                bool isArray = false;
                BsonType bsonPropertyType = BsonType.Null, bsonElementType = BsonType.Null;

                if (propertyType.IsArray)
                {
                    if (propertyType == typeof(byte[]))
                    {
                        bsonElementType = BsonType.Binary;
                    }
                    else
                    {
                        isArray = true;
                        elementType = propertyType.GetElementType();
                        bsonPropertyType = BsonType.Array;
                    }
                }

                elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

                if ((elementType == typeof(object) && field.IsAutoId) || elementType == typeof(ObjectId))
                {
                    elementType = typeof(ObjectId);
                    bsonElementType = BsonType.ObjectId;
                    field.FieldName = "_id";
                }
                else if (elementType.GetTypeInfo().IsValueType)
                {
                    if (elementType == typeof(int))
                        bsonElementType = BsonType.Int32;
                    else if (elementType == typeof(long))
                        bsonElementType = BsonType.Int64;
                    else if (elementType == typeof(double))
                        bsonElementType = BsonType.Double;
                    else if (elementType == typeof(Decimal))
                        bsonElementType = BsonType.Decimal128;
                    else if (elementType == typeof(bool))
                        bsonElementType = BsonType.Boolean;
                    else if (elementType == typeof(DateTime))
                        bsonElementType = BsonType.DateTime;
                    else if (elementType == typeof(string))
                        bsonElementType = BsonType.String;
                    else if (elementType == typeof(Guid))
                        bsonElementType = BsonType.String;
                    else if (elementType.GetTypeInfo().IsEnum)
                        bsonElementType = BsonType.Int32;
                    else if (elementType == typeof(ObjectId))
                        bsonElementType = BsonType.ObjectId;
                }
                else
                {
                    if (elementType == typeof(Decimal128))
                        bsonElementType = BsonType.Decimal128;
                    else if (elementType == typeof(string))
                        bsonElementType = BsonType.String;
                    else if (elementType == typeof(byte[]))
                        bsonElementType = BsonType.Binary;
                    else if (elementType == typeof(ObjectId))
                        bsonElementType = BsonType.ObjectId;
                    else
                    {
                        bsonElementType = BsonType.Document;
                        field.IsReference = columnInfo.ForeignKey;
                        field.ReferencedEntity = AllEntities.Inst.FindType(elementType);
                    }
                }

                if (bsonElementType == BsonType.Null)
                    throw new BsonException(BsonExceptionCode.TypeIsNotSupported);

                if (bsonPropertyType != BsonType.Array)
                    bsonPropertyType = bsonElementType;

                field.IsArray = isArray;
                field.PropertyBsonElementType = bsonElementType;
                field.PropertyBsonType = bsonPropertyType;
                field.PropertyElementType = elementType;

                fields.Add(field);
                if (field.IsPrimaryKey)
                    description.PrimaryKey = field;

                description.FieldsIndex[field.FieldName] = field;
                if (field.FieldName != field.PropertyAccessor.Name)
                    description.FieldsIndex[field.PropertyAccessor.Name] = field;
            }

            description.Fields = fields.ToArray();

            return description;
        }
    }
}