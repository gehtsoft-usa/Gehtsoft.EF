using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;
using Gehtsoft.EF.Utils;
using System.Collections;
using System.Linq;
using System.ComponentModel;

namespace Gehtsoft.EF.Bson
{
    /// <summary>
    /// The extension to convert the entities to BSON and back
    /// </summary>
    public static class EntityToBsonController
    {
        /// <summary>
        /// The flag forcing to interpret unspecified time zone as a local timezone.
        /// </summary>
        public static bool UnspecifiedTypeIsLocalByDefault { get; set; } = true;

        /// <summary>
        /// The flag indicating whether DateTime must be returned in local time zone.
        /// </summary>
        public static bool ReturnDateTimeAsLocalByDefault { get; set; } = true;

        /// <summary>
        /// The extension method that converts an entity into a BSON.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static BsonDocument ConvertToBson(this object entity)
        {
            BsonEntityDescription description = AllEntities.Inst.FindBsonEntity(entity.GetType());
            BsonDocument document = new BsonDocument();

            foreach (BsonEntityField field in description.Fields)
            {
                object value = field.PropertyAccessor.GetValue(entity);
                document.Set(field.FieldName, SerializeValue(value, field));
            }

            return document;
        }

        /// <summary>
        /// The extension method that converts a BSON document to an entity.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object ToEntity(this BsonDocument document, Type type)
        {
            BsonEntityDescription description = AllEntities.Inst.FindBsonEntity(type);
            object value = Activator.CreateInstance(type);
            foreach (BsonEntityField field in description.Fields)
            {
                if (document.Contains(field.FieldName))
                {
                    object propValue = DeserializeValue(document[field.FieldName], field);
                    if (propValue != null)
                    {
                        if (field.IsReference && field.PropertyAccessor.PropertyType != propValue.GetType())
                        {
                            BsonEntityDescription refEntity = AllEntities.Inst.FindBsonEntity(field.PropertyElementType);
                            if (refEntity == null)
                                throw new BsonException(BsonExceptionCode.TypeIsNotEntity);
                            BsonEntityField pk = refEntity.PrimaryKey;
                            if (pk == null)
                                throw new BsonException(BsonExceptionCode.NoPk);
                            if (pk.PropertyAccessor.PropertyType != propValue.GetType())
                                propValue = Convert.ChangeType(propValue, pk.PropertyAccessor.PropertyType);
                            object v = Activator.CreateInstance(field.PropertyAccessor.PropertyType);
                            pk.PropertyAccessor.SetValue(v, propValue);
                            propValue = v;
                        }
                        else
                        {
                            if (!field.IsArray && propValue.GetType() != field.PropertyElementType)
                                propValue = Convert.ChangeType(propValue, field.PropertyAccessor.PropertyType);
                        }
                    }

                    field.PropertyAccessor.SetValue(value, propValue);
                }
            }
            return value;
        }

        [DocgenIgnore]
        public static object DeserializeValue(BsonValue value, BsonEntityField fieldInfo)
        {
            if (value == null || value.IsBsonNull)
                return null;
            if (value.IsString)
            {
                if (fieldInfo != null && fieldInfo.PropertyElementType == typeof(Guid))
                {
                    string s = value.AsString;
                    if (Guid.TryParse(s, out Guid g))
                        return g;
                    return null;
                }
                return value.AsString;
            }
            if (value.IsObjectId)
                return value.AsObjectId;
            if (value.IsBoolean)
                return value.AsBoolean;
            if (value.IsInt32)
            {
                if (fieldInfo != null && fieldInfo.PropertyElementType.GetTypeInfo().IsEnum)
                    return Enum.ToObject(fieldInfo.PropertyElementType, value.AsInt32);
                return value.AsInt32;
            }
            if (value.IsInt64)
            {
                if (fieldInfo != null && fieldInfo.PropertyElementType.GetTypeInfo().IsEnum)
                    return Enum.ToObject(fieldInfo.PropertyElementType, value.AsInt64);
                return value.AsInt64;
            }
            if (value.IsDouble)
                return value.AsDouble;
            if (value.IsDecimal128)
                return value.AsDecimal;
            if (value.IsBsonDateTime)
            {
                BsonDateTime dateTime = value.AsBsonDateTime;
                DateTime utcTime = new DateTime(dateTime.ToUniversalTime().Ticks, DateTimeKind.Utc);
                if (ReturnDateTimeAsLocalByDefault)
                    return utcTime.ToLocalTime();
                return utcTime;
            }

            if (value.IsBsonBinaryData)
                return value.AsBsonBinaryData.Bytes;
            if (value.IsBsonArray && fieldInfo != null && fieldInfo.IsArray)
            {
                BsonArray arrSrc = value.AsBsonArray;
                int length = arrSrc.Count;
                object arrDst = Activator.CreateInstance(fieldInfo.PropertyAccessor.PropertyType, new object[] { length });
                Array arrDest1 = (Array)arrDst;
                for (int i = 0; i < length; i++)
                {
                    object v = DeserializeValue(arrSrc[i], fieldInfo);
                    arrDest1.SetValue(v, i);
                }

                return arrDst;
            }
            if (value.IsBsonDocument && fieldInfo != null)
                return ToEntity(value.AsBsonDocument, fieldInfo.PropertyElementType);
            throw new BsonException(BsonExceptionCode.TypeIsNotSupported);
        }

        [DocgenIgnore]
        public static BsonValue SerializeValue(object value, BsonEntityField fieldInfo)
        {
            if (value == null)
                return BsonNull.Value;
            else if (value is int iv)
                return new BsonInt32(iv);
            else if (value is long lv)
                return new BsonInt64(lv);
            else if (value is double dbl)
                return new BsonDouble(dbl);
            else if (value is decimal dcml)
                return new BsonDecimal128(dcml);
            else if (value is DateTime dt)
            {
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    if (UnspecifiedTypeIsLocalByDefault)
                        dt = new DateTime(dt.Ticks, DateTimeKind.Local);
                    else
                        dt = new DateTime(dt.Ticks, DateTimeKind.Utc);
                }
                if (dt.Kind == DateTimeKind.Utc)
                    return new BsonDateTime(dt);
                else if (dt.Kind == DateTimeKind.Local)
                    return new BsonDateTime(dt.ToUniversalTime());

                return new BsonDateTime(new DateTime(dt.Ticks, DateTimeKind.Utc));
            }
            else if (value is bool b)
                return new BsonBoolean(b);
            else if (value is byte[] bt)
                return new BsonBinaryData(bt);
            else if (value is string s)
                return new BsonString(s);
            else if (value.GetType().GetTypeInfo().IsEnum)
                return new BsonInt32((int?)Convert.ChangeType(value, typeof(int)) ?? 0);
            else if (value is ObjectId oid)
                return new BsonObjectId(oid);
            else if (value is BsonObjectId oid1)
                return oid1;
            else if (value is Guid g)
            {
                return new BsonString(g.ToString());
            }
            else if (value.GetType().IsArray)
            {
                BsonArray rarray = new BsonArray();
                Array sarray = (Array)value;
                for (int i = 0; i < sarray.Length; i++)
                    rarray.Add(SerializeValue(sarray.GetValue(i), fieldInfo));
                return rarray;
            }
            else if (value is IEnumerable enumerable)
            {
                List<object> slist = new List<object>();
                foreach (var obj in enumerable)
                    slist.Add(obj);
                BsonArray rarray = new BsonArray();
                for (int i = 0; i < slist.Count; i++)
                    rarray.Add(SerializeValue(slist[i], fieldInfo));
                return rarray;
            }
            else
            {
                if (fieldInfo != null)
                {
                    BsonEntityDescription refDescription = AllEntities.Inst.FindBsonEntity(fieldInfo.PropertyElementType);
                    if (fieldInfo.IsReference)
                    {
                        BsonEntityField pk = refDescription.PrimaryKey;
                        if (pk == null)
                            throw new BsonException(BsonExceptionCode.NoPk);
                        object pkvalue = pk.PropertyAccessor.GetValue(value);
                        return SerializeValue(pkvalue, pk);
                    }
                }
                var isEntity = AllEntities.Get(value.GetType(), false) != null;
                if (isEntity)
                    return ConvertToBson(value);
                throw new ArgumentException($"The type {value.GetType()} is neither an entity nor any of supported simple types");
            }
        }

        /// <summary>
        /// The extension method that converts a BSON document to an entity (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="document"></param>
        /// <returns></returns>
        public static T ToEntity<T>(this BsonDocument document) where T : class => EntityToBsonController.ToEntity(document, typeof(T)) as T;

        public static EntityCollection<T> ToEntities<T>(this IEnumerable<BsonDocument> documents) where T : class
            => ToEntities<EntityCollection<T>, T>(documents);

        /// <summary>
        /// The extension method that converts the enumeration of the BSON documents into a collection of entities
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents"></param>
        /// <returns></returns>
        public static TC ToEntities<TC, T>(this IEnumerable<BsonDocument> documents)
            where TC : ICollection<T>, new()
            where T : class
        {
            TC collection = new TC();
            foreach (BsonDocument document in documents)
                collection.Add(document.ToEntity<T>());
            return collection;
        }
    }
}
