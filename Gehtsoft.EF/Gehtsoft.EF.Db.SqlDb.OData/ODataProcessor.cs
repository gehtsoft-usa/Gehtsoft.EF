using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Gehtsoft.EF.Db.SqlDb.OData
{
    public class ODataProcessor
    {
        internal enum FormatType
        {
            Json,
            Xml
        }

        private readonly ISqlDbConnectionFactory mConnectionFactory;
        private IEdmModel mModel => mModelBuilder.Model;
        private readonly EdmModelBuilder mModelBuilder;
        private string mRoot = string.Empty;
        private FormatType mFormat = FormatType.Json;

        private string mODataCountName = "odata.count";
        private string mODataMetadataName = "odata.metadata";
        private string mCanDeleteName = "_candelete_";

        public string Root
        {
            get { return mRoot; }
            set { mRoot = value; }
        }

        public string CanDeleteName
        {
            get { return mCanDeleteName; }
            set { mCanDeleteName = value; }
        }

        public string ODataCountName
        {
            get { return mODataCountName; }
            set { mODataCountName = value; }
        }

        public string ODataMetadataName
        {
            get { return mODataMetadataName; }
            set { mODataMetadataName = value; }
        }

        public ODataProcessor(ISqlDbConnectionFactory connectionFactory, EntityFinder.EntityTypeInfo[] entities, string nsname = "NS", string root = "")
        {
            mConnectionFactory = connectionFactory;
            mModelBuilder = new EdmModelBuilder();
            mModelBuilder.Build(entities, nsname);
            mRoot = root;
            while (mRoot.Length > 0 && mRoot.EndsWith("/")) mRoot = mRoot.Substring(0, mRoot.Length - 1);
        }

        public ODataProcessor(ISqlDbConnectionFactory connectionFactory, EdmModelBuilder edmModelBuilder, string root = "")
        {
            mConnectionFactory = connectionFactory;
            mModelBuilder = edmModelBuilder;
            mRoot = root;
            while (mRoot.Length > 0 && mRoot.EndsWith("/")) mRoot = mRoot.Substring(0, mRoot.Length - 1);
        }

        public String GetRelativeUrl(string url) => url.Substring(mRoot.Length);

        public Task<object> SelectDataAsync(Uri uri, CancellationToken? token) => SelectDataCore(true, uri, token);

        public object SelectData(Uri uri) => SelectDataCore(false, uri, null).ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task<object> SelectDataCore(bool async, Uri uri, CancellationToken? token)
        {
            SqlDbConnection connection = mConnectionFactory.GetConnection();
            try
            {
                bool inlinecount = false;
                bool candelete = false;
                string queryStr = Uri.UnescapeDataString(uri.OriginalString);
                string[] queryParts = queryStr.Split(new char[] { '&', '?' });
                if (queryParts.Length > 1)
                {
                    for (int i = 1; i < queryParts.Length; i++)
                    {
                        string queryParameter = queryParts[i];
                        if (queryParameter.Equals("$inlinecount=allpages"))
                        {
                            inlinecount = true;
                            continue;
                        }
                        if (queryParameter.Equals("$count=true"))
                        {
                            inlinecount = true;
                            continue;
                        }
                        if (queryParameter.Equals("$candelete=true"))
                        {
                            candelete = true;
                            continue;
                        }
                        if (queryParameter.StartsWith("$format="))
                        {
                            string[] parts = queryParameter.Split(new char[] { '=' });
                            if (parts.Length < 2 || parts[1].Length == 0)
                            {
                                throw new EfODataException(EfODataExceptionCode.UnsupportedFormat);
                            }
                            string format = parts[1].ToLower();
                            if (format == "json")
                            {
                                mFormat = FormatType.Json;
                                continue;
                            }
                            else if (format == "xml")
                            {
                                mFormat = FormatType.Xml;
                                continue;
                            }
                            throw new EfODataException(EfODataExceptionCode.UnsupportedFormat);
                        }
                    }
                    if (!queryParts[0].StartsWith("/")) queryParts[0] = "/" + queryParts[0];
                }

                ODataUriParser parser = new ODataUriParser(mModel, uri);
                ODataUri uriParser = parser.ParseUri();
                ODataToQuery oDataToQuery = new ODataToQuery(mModelBuilder, uriParser, connection);
                AQueryBuilder queryBuilder = oDataToQuery.BuildQuery();
                using (SqlDbQuery query = connection.GetQuery(queryBuilder))
                {
                    if (inlinecount && oDataToQuery.ResultMode != ODataToQuery.ResultType.Array)
                    {
                        throw new EfODataException(EfODataExceptionCode.QueryOptionsFault);
                    }

                    int pagingLimit = mModelBuilder.EntityPagingLimitByName(oDataToQuery.MainEntityDescriptor.EntityType.Name + "_Type");

                    bindParams(query, oDataToQuery);

                    string primaryKeyName = null;
                    if (oDataToQuery.OneToMany || pagingLimit > 0 || candelete)
                    {
                        foreach (var property in oDataToQuery.MainEntityDescriptor.TableDescriptor)
                        {
                            if (property.PrimaryKey)
                            {
                                primaryKeyName = property.ID;
                                break;
                            }
                        }
                    }
                    Type primaryKeyType = null;
                    if (primaryKeyName != null)
                    {
                        primaryKeyType = mModelBuilder.TypeByName(oDataToQuery.MainEntityDescriptor.EntityType, primaryKeyName);
                    }

                    if (async)
                        await query.ExecuteReaderAsync(token);
                    else
                        query.ExecuteReader();

                    List<object> result = new List<object>();
                    int dal = oDataToQuery.Skip ?? 0;
                    string skiptoken = null;

                    while (async ? await query.ReadNextAsync(token) : query.ReadNext())
                    {
                        if (pagingLimit > 0 && oDataToQuery.ResultMode == ODataToQuery.ResultType.Array && !oDataToQuery.OneToMany)
                        {
                            if (result.Count >= pagingLimit)
                            {
                                if (primaryKeyName != null)
                                {
                                    object orgValue = (result[result.Count - 1] as Dictionary<string, object>)[primaryKeyName];
                                    object value = query.LanguageSpecifics.TranslateValue(orgValue, primaryKeyType);
                                    if (value is int || value is long || value is double)
                                        skiptoken = value.ToString();
                                    else
                                        skiptoken = $"'{value.ToString()}'";
                                }
                                break;
                            }
                        }

                        object o = oDataToQuery.Bind(query);
                        if (oDataToQuery.OneToMany && o is XmlSerializableDictionary dict)
                        {
                            List<string> keysToDelete = new List<string>();
                            XmlSerializableDictionary toAdd = new XmlSerializableDictionary();
                            foreach (KeyValuePair<string, object> pair in dict)
                            {
                                if (pair.Key.StartsWith(ODataToQuery.ARRMARKER))
                                {
                                    string newKey = pair.Key.Substring(ODataToQuery.ARRMARKER.Length);
                                    keysToDelete.Add(pair.Key);
                                    toAdd.Add(newKey, pair.Value);
                                }
                            }
                            foreach (string key in keysToDelete) dict.Remove(key);
                            foreach (KeyValuePair<string, object> pair in toAdd)
                            {
                                dict.Add(pair.Key, new List<object> { pair.Value });
                            }

                            if (result.Count == 0)
                                result.Add(o);
                            else
                            {
                                XmlSerializableDictionary curr = o as XmlSerializableDictionary;
                                int currId = (int)curr[primaryKeyName];
                                XmlSerializableDictionary prev = result.Where(t => ((int)(t as XmlSerializableDictionary)[primaryKeyName]) == currId).FirstOrDefault() as XmlSerializableDictionary;
                                if (prev != null)
                                {
                                    foreach (KeyValuePair<string, object> pair in toAdd)
                                    {
                                        List<object> list = prev[pair.Key] as List<object>;
                                        list.Add(pair.Value);
                                    }
                                }
                                else
                                    result.Add(o);
                            }
                        }
                        else
                            result.Add(o);

                        if(candelete)
                        {
                            object orgValue = (result[result.Count - 1] as Dictionary<string, object>)[primaryKeyName];
                            int key = (int)query.LanguageSpecifics.TranslateValue(orgValue, primaryKeyType);
                            bool recordCanBeDeleted = this.CanDelete(oDataToQuery.MainEntityDescriptor.EntityType, key);
                            (result[result.Count - 1] as Dictionary<string, object>).Add(CanDeleteName, recordCanBeDeleted);
                        }
                    }

                    string segment = oDataToQuery.MainEntityDescriptor.EntityType.Name;
                    string metadata = $"{mRoot}/$metadata#{segment}";
                    if (oDataToQuery.ResultMode != ODataToQuery.ResultType.Array)
                    {
                        metadata = $"{metadata}/@Element";
                    }
                    for (int i = 1; i < queryParts.Length; i++)
                    {
                        string queryParameter = queryParts[i];
                        if (queryParameter.StartsWith("$select="))
                        {
                            metadata = $"{metadata}{queryParameter}";
                            break;
                        }
                    }

                    if (oDataToQuery.ResultMode != ODataToQuery.ResultType.Array)
                    {
                        if (result.Count == 0)
                        {
                            throw new EfODataException(EfODataExceptionCode.ResourceNotFound, segment);
                        }
                        if (oDataToQuery.ResultMode == ODataToQuery.ResultType.Plain)
                        {
                            return result[0];
                        }
                        XmlSerializableDictionary res = result[0] as XmlSerializableDictionary;
                        res.Add(mODataMetadataName, metadata);
                        return res;
                    }

                    long totalCount = -1;
                    int limit = ((SelectQueryBuilder)queryBuilder).Limit;
                    int skip = ((SelectQueryBuilder)queryBuilder).Skip;
                    if (inlinecount)
                    {
                        if (oDataToQuery.OneToMany)
                        {
                            totalCount = result.Count;
                        }
                        else
                        {
                            ODataUri uriParserForCount = parser.ParseUri();
                            uriParserForCount.SkipToken = null;
                            uriParserForCount.Skip = null;
                            uriParserForCount.Top = null;
                            ODataToQuery oDataToQueryForCount = new ODataToQuery(mModelBuilder, uriParserForCount, connection);
                            AQueryBuilder queryBuilderForCount = oDataToQueryForCount.BuildQuery(true);
                            SelectQueryBuilder builderForCount = (SelectQueryBuilder)queryBuilderForCount;
                            builderForCount.ResetResultset();
                            builderForCount.AddToResultset(AggFn.Count);

                            using (SqlDbQuery queryForCount = connection.GetQuery(builderForCount))
                            {
                                bindParams(queryForCount, oDataToQueryForCount);

                                if (async)
                                    await queryForCount.ExecuteReaderAsync(token);
                                else
                                    queryForCount.ExecuteReader();
                                if (async ? await queryForCount.ReadNextAsync(token) : queryForCount.ReadNext())
                                {
                                    totalCount = (long)queryForCount.GetValue(0);
                                }
                            }
                        }
                    }

                    if (oDataToQuery.OneToMany)
                    {
                        int quanto = oDataToQuery.Top ?? int.MaxValue;
                        if (quanto > result.Count - dal)
                        {
                            quanto = result.Count - dal;
                        }
                        if (pagingLimit > 0 && quanto >= pagingLimit)
                        {
                            quanto = pagingLimit;
                            if (primaryKeyName != null)
                            {
                                object orgValue = (result[dal + quanto - 1] as Dictionary<string, object>)[primaryKeyName];
                                object value = query.LanguageSpecifics.TranslateValue(orgValue, primaryKeyType);
                                if (value is int || value is long || value is double)
                                    skiptoken = value.ToString();
                                else
                                    skiptoken = $"'{value.ToString()}'";
                            }
                        }
                        result = result.GetRange(dal, quanto);
                    }
                    XmlSerializableDictionary resDict = new XmlSerializableDictionary();

                    if (totalCount > -1)
                    {
                        resDict.Add(mODataCountName, totalCount);
                    }
                    resDict.Add("value", result);
                    if (skiptoken != null)
                    {
                        StringBuilder nextUrl = new StringBuilder(mRoot + encode(queryParts[0]));
                        bool wasTop = false;
                        int paramCounts = 0;
                        for (int i = 1; i < queryParts.Length; i++)
                        {
                            string queryParameter = queryParts[i];
                            if (queryParameter.StartsWith("$top="))
                            {
                                wasTop = true;
                            }
                            else if (queryParameter.StartsWith("$skip"))
                            {
                                continue;
                            }
                            else
                            {
                                nextUrl.Append(paramCounts == 0 ? "?" : "&");
                                nextUrl.Append(encode(queryParameter));
                                paramCounts++;
                            }
                        }
                        if (wasTop)
                        {
                            if (limit > pagingLimit)
                            {
                                nextUrl.Append(paramCounts == 0 ? "?" : "&");
                                nextUrl.Append($"$top={limit - pagingLimit}");
                            }
                        }
                        nextUrl.Append(paramCounts == 0 ? "?" : "&");
                        if (uriParser.OrderBy == null)
                            nextUrl.Append($"$skiptoken={skiptoken}");
                        else
                            nextUrl.Append($"$skip={skip + pagingLimit}");

                        resDict.Add("odata.nextLink", nextUrl.ToString());
                    }

                    resDict.Add(mODataMetadataName, metadata);
                    return resDict;
                }
            }
            catch (Exception ex)
            {
                XmlSerializableDictionary res = new XmlSerializableDictionary();
                XmlSerializableDictionary error = new XmlSerializableDictionary();
                XmlSerializableDictionary message = new XmlSerializableDictionary();
                string code = string.Empty;
                if (ex is EfODataException efex)
                {
                    code = efex.ErrorCode.ToString();
                }

                message.Add("lang", "en-US");
                message.Add("value", ex.Message);
                error.Add("code", code);
                error.Add("message", message);
                res.Add("odata.error", error);

                return res;
            }
            finally
            {
                if (mConnectionFactory.NeedDispose)
                    connection.Dispose();
            }
        }

        private void bindParams(SqlDbQuery query, ODataToQuery oDataToQuery)
        {
            foreach (KeyValuePair<string, object> pair in oDataToQuery.BindParams)
            {
                Type tttt = pair.Value.GetType();
                if (pair.Value is int intValue)
                    query.BindParam(pair.Key, intValue);
                else if (pair.Value is double doubleValue)
                    query.BindParam(pair.Key, doubleValue);
                else if (pair.Value is bool boolValue)
                    query.BindParam(pair.Key, boolValue);
                else if (pair.Value is DateTime dateTimeValue)
                    query.BindParam(pair.Key, dateTimeValue);
                else if (pair.Value is DateTimeOffset dateTimeOffsetValue)
                    query.BindParam(pair.Key, dateTimeOffsetValue.LocalDateTime);
                else if (pair.Value is Microsoft.OData.Edm.Date dateValue)
                    query.BindParam(pair.Key, new DateTime(dateValue.Year, dateValue.Month, dateValue.Day));
                else
                    query.BindParam(pair.Key, pair.Value.ToString());
            }
        }

        public Task<string> GetFormattedDataAsync(Uri uri, CancellationToken? token) => GetFormattedDataCore(true, uri, token);

        public string GetFormattedData(Uri uri) => GetFormattedDataCore(false, uri, null).ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task<string> GetFormattedDataCore(bool async, Uri uri, CancellationToken? token)
        {
            object obj;
            string result = string.Empty;

            if (async)
                obj = await SelectDataAsync(uri, token);
            else
                obj = SelectData(uri);

            if (!(obj is Dictionary<string, object>))
            {
                if (obj is DateTime date)
                {
                    result = date.ToString("s");
                }
                else
                {
                    result = obj.ToString();
                }
            }
            else if (mFormat == FormatType.Json)
            {
                using (StringWriter writer = new StringWriter())
                {
                    using (JsonTextWriter jswriter = new JsonTextWriter(writer))
                    {
                        JsonSerializer sr = JsonSerializer.Create();
                        sr.Formatting = Newtonsoft.Json.Formatting.None;
                        sr.NullValueHandling = NullValueHandling.Ignore;
                        sr.StringEscapeHandling = StringEscapeHandling.Default;
                        sr.Serialize(jswriter, obj);
                    }
                    result = writer.ToString();
                }
            }
            else if (mFormat == FormatType.Xml)
            {
                XmlSerializer serializer = new XmlSerializer(obj.GetType());
                using (StringWriter writer = new StringWriter())
                {
                    using (XmlWriter jswriter = XmlWriter.Create(writer))
                    {
                        serializer.Serialize(writer, obj);
                        result = writer.ToString();
                    }
                    result = writer.ToString();
                }
            }
            return result;
        }

        public Task<string> RemoveRecordAsync(string tableName, int id, CancellationToken? token) => RemoveRecordCore(true, tableName, id, token);

        public string RemoveRecord(string tableName, int id) => RemoveRecordCore(false, tableName, id, null).ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task<string> RemoveRecordCore(bool async, string tableName, int id, CancellationToken? token)
        {
            string result = null;
            SqlDbConnection connection = mConnectionFactory.GetConnection();
            try
            {
                string enityTypeName = tableName + "_Type";
                Type entityType = mModelBuilder.EntityTypeByName(enityTypeName);
                if (entityType == null)
                    throw new EfODataException(EfODataExceptionCode.NoEntityInBuildQuery);
                EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
                DeleteQueryBuilder builder = new DeleteQueryBuilder(connection.GetLanguageSpecifics(), entityDescriptor.TableDescriptor);
                string pKey = builder.Where.PropertyName(null, entityDescriptor.TableDescriptor.PrimaryKey);
                string expression = builder.Where.InfoProvider.Specifics.GetOp(CmpOp.Eq, pKey, id.ToString());
                builder.Where.Add(LogOp.And, expression);
                builder.PrepareQuery();

                using (SqlDbQuery query = connection.GetQuery(builder))
                {
                    if (async)
                        await query.ExecuteNoDataAsync(token);
                    else
                        query.ExecuteNoData();
                }
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            finally
            {
                if (mConnectionFactory.NeedDispose)
                    connection.Dispose();
            }

            return result;
        }


        public Task<Tuple<string, bool>> AddNewRecordAsync(string tableName, string serializedBody, CancellationToken? token) => AddUpdateRecordCore(true, tableName, serializedBody, token);

        public string AddNewRecord(string tableName, string serializedBody, out bool wasError)
        {
            Tuple<string, bool> result = AddUpdateRecordCore(false, tableName, serializedBody, null).ConfigureAwait(false).GetAwaiter().GetResult();
            wasError = result.Item2;
            return result.Item1;
        }

        public Task<Tuple<string, bool>> UpdateRecordAsync(string tableName, string serializedBody, int id, CancellationToken? token) => AddUpdateRecordCore(true, tableName, serializedBody, token, id);

        public string UpdateRecord(string tableName, string serializedBody, int id, out bool wasError)
        {
            Tuple<string, bool> result = AddUpdateRecordCore(false, tableName, serializedBody, null, id).ConfigureAwait(false).GetAwaiter().GetResult();
            wasError = result.Item2;
            return result.Item1;
        }

        private async Task<Tuple<string, bool>> AddUpdateRecordCore(bool async, string tableName, string serializedBody, CancellationToken? token, int sourceId = 0)
        {
            bool wasError = false;
            string result = null;
            SqlDbConnection connection = mConnectionFactory.GetConnection();
            try
            {
                string enityTypeName = tableName + "_Type";
                Type entityType = mModelBuilder.EntityTypeByName(enityTypeName);
                if (entityType == null)
                    throw new EfODataException(EfODataExceptionCode.NoEntityInBuildQuery);

                EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
                string pKey = entityDescriptor.TableDescriptor.PrimaryKey.ID;
                JValue pKeyJValue = null;
                JValue pCanDeleteJValue = null;
                JObject body = null;
                object entity;
                if (sourceId <= 0)
                {
                    entity = Activator.CreateInstance(entityType);
                    fillEntityWithDefaultValues(entity, entityDescriptor.TableDescriptor);
                }
                else
                {
                    using (SelectEntitiesQuery getQuery = connection.GetSelectEntitiesQuery(entityType))
                    {
                        string key = getQuery.Where.PropertyName(pKey);
                        string expression = getQuery.LanguageSpecifics.GetOp(CmpOp.Eq, key, sourceId.ToString());
                        getQuery.Where.Add(LogOp.And, expression);
                        entity = async ? await getQuery.ReadOneAsync(token) : getQuery.ReadOne();
                    }
                }

                using (StringReader reader = new StringReader(serializedBody))
                {
                    using (JsonTextReader jsreader = new JsonTextReader(reader))
                    {
                        JsonSerializer sr = JsonSerializer.Create();
                        sr.Formatting = Newtonsoft.Json.Formatting.None;
                        sr.NullValueHandling = NullValueHandling.Ignore;
                        sr.StringEscapeHandling = StringEscapeHandling.Default;
                        body = (JObject)sr.Deserialize(jsreader);

                        foreach (KeyValuePair<string, JToken> item in body)
                        {
                            string fieldName = item.Key;
                            if (fieldName == pKey) pKeyJValue = (JValue)item.Value;
                            if(fieldName == CanDeleteName) pCanDeleteJValue = (JValue)item.Value;
                            object value = ((JValue)item.Value).Value;
                            changeFieldValueInEntity(entity, fieldName, value);
                        }
                    }
                }
                ModifyEntityQuery query;
                if (sourceId <= 0)
                    query = connection.GetInsertEntityQuery(entityType);
                else
                    query = connection.GetUpdateEntityQuery(entityType);

                if (async)
                    await query.ExecuteAsync(entity, token);
                else
                    query.Execute(entity);

                PropertyInfo propertyInfo = entity.GetType().GetProperty(pKey);
                object newKey = propertyInfo.GetValue(entity);
                if (pKeyJValue != null)
                {
                    pKeyJValue.Value = newKey;
                }
                else
                {
                    body.Add(pKey, new JValue(newKey));
                }
                if (sourceId <= 0 || pCanDeleteJValue != null)
                {
                    bool recordCanBeDeleted = (sourceId <= 0) ? true : this.CanDelete(entityType, (int)newKey);
                    if (pCanDeleteJValue != null)
                    {
                        pCanDeleteJValue.Value = recordCanBeDeleted;
                    }
                    else
                    {
                        body.Add(CanDeleteName, new JValue(recordCanBeDeleted));
                    }
                }

                using (StringWriter writer = new StringWriter())
                {
                    using (JsonTextWriter jswriter = new JsonTextWriter(writer))
                    {
                        JsonSerializer sr = JsonSerializer.Create();
                        sr.Formatting = Newtonsoft.Json.Formatting.None;
                        sr.NullValueHandling = NullValueHandling.Ignore;
                        sr.StringEscapeHandling = StringEscapeHandling.Default;
                        sr.Serialize(jswriter, body);
                    }
                    result = writer.ToString();
                }
            }
            catch (Exception ex)
            {
                result = ex.Message;
                wasError = true;
            }
            finally
            {
                if (mConnectionFactory.NeedDispose)
                    connection.Dispose();
            }

            return new Tuple<string, bool>(result, wasError);
        }

        private void fillEntityWithDefaultValues(object entity, TableDescriptor tableDescriptor)
        {
            foreach (TableDescriptor.ColumnInfo columnInfo in tableDescriptor)
            {
                if (columnInfo.DefaultValue != null)
                {
                    PropertyInfo propertyInfo = entity.GetType().GetProperty(columnInfo.ID);
                    Type propertyType = propertyInfo.PropertyType;
                    propertyInfo.SetValue(entity, Convert.ChangeType(columnInfo.DefaultValue, propertyType));
                }
            }
        }

        private object createEntity(Type type, int id)
        {
            object entity = Activator.CreateInstance(type);
            EntityDescriptor entityDescriptor = AllEntities.Inst[entity.GetType()];
            string pKey = entityDescriptor.TableDescriptor.PrimaryKey.ID;
            PropertyInfo propertyInfo = entity.GetType().GetProperty(pKey);
            propertyInfo.SetValue(entity, id);

            return entity;
        }

        private void changeFieldValueInEntity(object entity, string fieldName, object value)
        {
            PropertyInfo propertyInfo = entity.GetType().GetProperty(fieldName);
            if (propertyInfo == null)
            {
                string name = mModelBuilder.NameByField(entity.GetType(), fieldName);
                if (name != null)
                {
                    propertyInfo = entity.GetType().GetProperty(name);
                }
            }
            if (propertyInfo != null)
            {
                Type propertyType = propertyInfo.PropertyType;
                if (propertyType.IsClass && !propertyType.FullName.StartsWith("System."))
                {
                    EntityDescriptor entityDescriptor = AllEntities.Inst[propertyType, false];
                    if (entityDescriptor != null)
                    {
                        try
                        {
                            object innerObj = createEntity(propertyType, Int32.Parse(value.ToString()));
                            if (innerObj != null)
                            {
                                propertyInfo.SetValue(entity, innerObj);
                            }
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                        return;
                    }
                }
                try
                {
                    propertyInfo.SetValue(entity, Convert.ChangeType(value, propertyType));
                }
                catch { }
            }
        }

        private string encode(string source)
        {
            return source.Replace(" ", "%20");
        }

        public bool CanDelete(Type type, int id)
        {
            SqlDbConnection connection = mConnectionFactory.GetConnection();
            bool result = false;
            try
            {
                result = connection.CanDelete(createEntity(type, id));
            }
            finally
            {
                if (mConnectionFactory.NeedDispose)
                    connection.Dispose();
            }
            return result;
        }
    }

    [XmlRoot("set")]
    public class XmlSerializableDictionary : Dictionary<string, object>, IXmlSerializable
    {
        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            throw new NotImplementedException();
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            //XmlSerializer keySerializer = new XmlSerializer(typeof(string));
            XmlSerializer valueSerializer;
            foreach (string key in this.Keys)
            {
                writer.WriteStartElement("item");
                //writer.WriteStartElement("key");
                writer.WriteStartAttribute("key");
                writer.WriteRaw(key.ToString());
                //keySerializer.Serialize(writer, key);
                writer.WriteEndAttribute();
                //writer.WriteEndElement();
                //writer.WriteStartElement("value");
                object value = this[key];
                if (value != null)
                {
                    if (typeof(IEnumerable<object>).IsAssignableFrom(value.GetType()))
                    {
                        valueSerializer = new XmlSerializer(typeof(XmlSerializableDictionary));
                        IEnumerable<object> list = value as IEnumerable<object>;
                        foreach (object value1 in list)
                        {
                            //writer.WriteStartElement("entry");
                            valueSerializer.Serialize(writer, value1);
                            //writer.WriteEndElement();
                        }
                    }
                    else if (typeof(XmlSerializableDictionary).IsAssignableFrom(value.GetType()))
                    {
                        valueSerializer = new XmlSerializer(typeof(XmlSerializableDictionary));
                        valueSerializer.Serialize(writer, value);
                    }
                    else
                    {
                        valueSerializer = new XmlSerializer(value.GetType());
                        writer.WriteStartAttribute("value");

                        //valueSerializer.Serialize(writer, value);

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
                //writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }
    }
}