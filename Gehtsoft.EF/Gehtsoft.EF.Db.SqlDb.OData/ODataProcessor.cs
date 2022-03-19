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
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
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
        private IEdmModel Model => mModelBuilder.Model;
        private readonly EdmModelBuilder mModelBuilder;
        private FormatType mFormat = FormatType.Json;

        public string Root { get; set; }

        public string CanDeleteName { get; set; } = "_candelete_";

        public string ODataCountName { get; set; } = "odata.count";

        public string ODataMetadataName { get; set; } = "odata.metadata";

        public ODataProcessor(ISqlDbConnectionFactory connectionFactory, EdmModelBuilder edmModelBuilder, string root = "")
        {
            mConnectionFactory = connectionFactory;
            mModelBuilder = edmModelBuilder;
            Root = root;
            while (Root.Length > 0 && Root.EndsWith("/")) Root = Root.Substring(0, Root.Length - 1);
        }

        public String GetRelativeUrl(string url) => url.Substring(Root.Length);

        public Task<object> SelectDataAsync(Uri uri, CancellationToken? token) => SelectDataCore(true, uri, token);

        public object SelectData(Uri uri) => SelectDataCore(false, uri, null).ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task<object> SelectDataCore(bool asyncCall, Uri uri, CancellationToken? token)
        {
            SqlDbConnection connection = mConnectionFactory.GetConnection();
            try
            {
                bool inlinecount = false;
                bool candelete = false;
                string queryStr = Uri.UnescapeDataString(uri.OriginalString);
                if (queryStr.Contains("/$metadata#"))
                    return SelectMetadata(queryStr);

                string[] queryParts = queryStr.Split(new char[] { '&', '?' });
                if (queryParts.Length > 1)
                {
                    for (int i = 1; i < queryParts.Length; i++)
                    {
                        string queryParameter = queryParts[i];
                        if (queryParameter.Equals("$inlinecount=allpages") || queryParameter.Equals("$count=true"))
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

                ODataUriParser parser = new ODataUriParser(Model, uri);
                ODataUri uriParser = parser.ParseUri();
                ODataToQuery oDataToQuery = new ODataToQuery(mModelBuilder, uriParser, connection);
                
                var queryBuilder = oDataToQuery.BuildQuery();

                int pagingLimit = mModelBuilder.EntityPagingLimitByName(oDataToQuery.MainEntityDescriptor.EntityType.Name + "_Type");
                bool hasPagingLimit = pagingLimit > 0;
                bool queryHasSkip = oDataToQuery.Skip != null;
                bool queryHasTop = oDataToQuery.Top != null;
                bool forcedSkip = queryHasTop || queryHasSkip;

                if (queryHasSkip)
                    queryBuilder.Skip = oDataToQuery.Skip.Value;
                if (queryHasTop)
                    queryBuilder.Limit = oDataToQuery.Top.Value;
                else if (hasPagingLimit)
                    queryBuilder.Limit = pagingLimit;

                using (SqlDbQuery query = connection.GetQuery(queryBuilder))
                {
                    if (inlinecount && oDataToQuery.ResultMode != ODataToQuery.ResultType.Array)
                        throw new EfODataException(EfODataExceptionCode.QueryOptionsFault);

                    BindParams(query, oDataToQuery);

                    string primaryKeyName = oDataToQuery.MainEntityDescriptor.TableDescriptor.PrimaryKey?.ID;
                    Type primaryKeyType = null;

                    if (primaryKeyName != null)
                        primaryKeyType = mModelBuilder.TypeByName(oDataToQuery.MainEntityDescriptor.EntityType, primaryKeyName);

                    if (asyncCall)
                        await query.ExecuteReaderAsync(token);
                    else
                        query.ExecuteReader();

                    List<object> result = new List<object>();
                    while (asyncCall ? await query.ReadNextAsync(token) : query.ReadNext())
                    {
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
                                if (result.Find(t => ((int)(t as XmlSerializableDictionary)[primaryKeyName]) == currId) is XmlSerializableDictionary prev)
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

                        if (candelete)
                        {
                            object orgValue = (result[result.Count - 1] as Dictionary<string, object>)[primaryKeyName];
                            int key = (int)query.LanguageSpecifics.TranslateValue(orgValue, primaryKeyType);
                            bool recordCanBeDeleted = this.CanDelete(oDataToQuery.MainEntityDescriptor.EntityType, key);
                            (result[result.Count - 1] as Dictionary<string, object>).Add(CanDeleteName, recordCanBeDeleted);
                        }
                    }

                    string segment = oDataToQuery.MainEntityDescriptor.EntityType.Name;
                    string metadata = $"{Root}/$metadata#{segment}";
                    if (oDataToQuery.ResultMode != ODataToQuery.ResultType.Array)
                    {
                        metadata = $"{metadata}/@Element";
                    }
                    for (int i = 1; i < queryParts.Length; i++)
                    {
                        string queryParameter = queryParts[i];
                        if (queryParameter.StartsWith("$select="))
                        {
                            metadata += queryParameter;
                            break;
                        }
                    }

                    if (oDataToQuery.ResultMode != ODataToQuery.ResultType.Array)
                    {
                        if (result.Count == 0)
                            throw new EfODataException(EfODataExceptionCode.ResourceNotFound, segment);
                        if (oDataToQuery.ResultMode == ODataToQuery.ResultType.Plain)
                            return result[0];
                        XmlSerializableDictionary res = result[0] as XmlSerializableDictionary;
                        res.Add(ODataMetadataName, metadata);
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
                                BindParams(queryForCount, oDataToQueryForCount);

                                if (asyncCall)
                                    await queryForCount.ExecuteReaderAsync(token);
                                else
                                    queryForCount.ExecuteReader();
                                if (asyncCall ? await queryForCount.ReadNextAsync(token) : queryForCount.ReadNext())
                                {
                                    totalCount = (long)queryForCount.GetValue(0);
                                }
                            }
                        }
                    }

                    XmlSerializableDictionary resDict = new XmlSerializableDictionary();

                    if (totalCount > -1)
                    {
                        resDict.Add(ODataCountName, totalCount);
                    }

                    resDict.Add("value", result);
                    if (metadata != null)
                        resDict.Add(ODataMetadataName, metadata);

                    if ((queryHasTop || hasPagingLimit) && !oDataToQuery.HasSkipToken)
                    {
                        StringBuilder nextUrl = new StringBuilder(Root + Encode(queryParts[0]));
                        int paramCount = 0;
                        for (int i = 1; i < queryParts.Length; i++)
                        {
                            string queryParameter = queryParts[i];
                            if (queryParameter.StartsWith("$skip") || queryParameter.StartsWith("$top"))
                                continue;

                            nextUrl.Append(paramCount == 0 ? '?' : '&');
                            nextUrl.Append(Encode(queryParameter));
                            paramCount++;
                        }

                        var skipCount = (oDataToQuery.Skip ?? 0) + (oDataToQuery.Top ?? pagingLimit);
                        if (totalCount == -1 || skipCount < totalCount)
                        {
                            nextUrl.Append(paramCount == 0 ? '?' : '&');
                            nextUrl.Append("$top=").Append(oDataToQuery.Top ?? pagingLimit);
                            nextUrl.Append('&');
                            nextUrl.Append("$skip=").Append(skipCount);
                            resDict.Add("odata.nextLink", nextUrl.ToString());
                        }
                    }
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

        private void SelectMetadata_Properties(DocumentBuilder document, string typeName, string entityName, IEdmEntityType type, string now)
        {
            document.AtomElement("a", "feed", typeName + ".#Properties", now, "Properties");
            foreach (var property in type.Properties())
            {
                document.AtomElement("a", "entry", typeName + "." + property.Name, now, entityName + "." + property.Name);
                document.AppendElement("category", "a")
                    .AppendAttribute("term", "System.Data.Services.Providers.ResourceProperty")
                    .AppendAttribute("scheme", "http://schemas.microsoft.com/ado/2007/08/dataservices/scheme")
                    .Done();

                document.AppendElement("content", "a")
                    .AppendAttribute("type", "application/xml");
                document.AppendElement("properties", "m");

                document.AppendElement("FullName", "d")
                    .AppendText($"{mModelBuilder.Namespace}.{entityName}.{property.Name}")
                    .Done();

                document.AppendElement("Name", "d")
                    .AppendText(property.Name)
                    .Done();

                document.AppendElement("IsKey", "d")
                    .AppendAttribute("type", "m", "Edm.Boolean")
                    .AppendText(property.IsKey() ? "True" : "False")
                    .Done();

                document.AppendElement("IsPrimitive", "d")
                    .AppendAttribute("type", "m", "Edm.Boolean")
                    .AppendText(property.Type.IsPrimitive() ? "True" : "False")
                    .Done();

                document.AppendElement("IsComplexType", "d")
                    .AppendAttribute("type", "m", "Edm.Boolean")
                    .AppendText(property.Type.IsComplex() ? "True" : "False")
                    .Done();

                document.AppendElement("IsReference", "d")
                    .AppendAttribute("type", "m", "Edm.Boolean")
                    .AppendText(property.Type.IsEntityReference() ? "True" : "False")
                    .Done();

                document.AppendElement("IsSetReference", "d")
                    .AppendAttribute("type", "m", "Edm.Boolean")
                    .AppendText(property.Type.IsCollection() ? "True" : "False")
                    .Done();

                document.AppendElement("ResourceTypeName", "d")
                    .AppendText(property.Type.FullName())
                    .Done();

                document.Done() //m:properties
                    .Done()     //content
                    .Done();    //property/entry
            }
            document.Done();    //feed
        }

        private object SelectMetadata(string queryStr)
        {
            var index = queryStr.IndexOf("/$metadata#");
            var entityName = queryStr.Substring(index + 11);

            bool properties = false;
            if (entityName.EndsWith("/Properties"))
            {
                entityName = entityName.Substring(0, entityName.Length - 11);
                properties = true;
            }


            var typeName = $"{mModelBuilder.Namespace}.{entityName}_Type";
            var type = Model.FindType(typeName) as IEdmEntityType;
            if (type == null)
                throw new ArgumentException($"Requested type {entityName} aka {typeName} is not found", nameof(queryStr));

            var document = new DocumentBuilder();
            document.AddNamespace("a", "http://www.w3.org/2005/Atom");
            document.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices");
            document.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

            var now = DateTime.UtcNow.ToString("yyyy-mm-ddTHH:mm:ssZ");

            if (properties)
                SelectMetadata_Properties(document, typeName, entityName, type, now);
            else
            {
                document
                    .AtomElement("a", "entry", typeName, now, entityName)
                        .AppendElement("link", "a")
                            .AppendAttribute("rel", $"http://schemas.microsoft.com/ado/2007/08/dataservices/related/Properties")
                            .AppendAttribute("type", "application/atom+xml;type=feed")
                            .AppendAttribute("title", "Properties")
                            .AppendAttribute("href", $"/$metadata#{entityName}/Properties")
                            .AppendElement("inline", "m");

                SelectMetadata_Properties(document, typeName, entityName, type, now);

                document.Done();    //inline
                document.Done();    //link
            }
            return document.Document;
        }

        private void BindParams(SqlDbQuery query, ODataToQuery oDataToQuery)
        {
            foreach (KeyValuePair<string, object> pair in oDataToQuery.BindParams)
            {
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

        private async Task<string> GetFormattedDataCore(bool asyncCall, Uri uri, CancellationToken? token)
        {
            object obj;
            string result = string.Empty;

            if (asyncCall)
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
                        serializer.Serialize(writer, obj);
                    result = writer.ToString();
                }
            }
            return result;
        }

        public Task<string> RemoveRecordAsync(string tableName, int id, CancellationToken? token) => RemoveRecordCore(true, tableName, id, token);

        public string RemoveRecord(string tableName, int id) => RemoveRecordCore(false, tableName, id, null).ConfigureAwait(false).GetAwaiter().GetResult();

        private async Task<string> RemoveRecordCore(bool asyncCall, string tableName, int id, CancellationToken? token)
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
                DeleteQueryBuilder builder = connection.GetDeleteQueryBuilder(entityDescriptor.TableDescriptor);
                string pKey = builder.Where.PropertyName(null, entityDescriptor.TableDescriptor.PrimaryKey);
                string expression = builder.Where.InfoProvider.Specifics.GetOp(CmpOp.Eq, pKey, id.ToString());
                builder.Where.Add(LogOp.And, expression);
                builder.PrepareQuery();

                using (SqlDbQuery query = connection.GetQuery(builder))
                {
                    if (asyncCall)
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

        private async Task<Tuple<string, bool>> AddUpdateRecordCore(bool asyncCall, string tableName, string serializedBody, CancellationToken? token, int sourceId = 0)
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
                    FillEntityWithDefaultValues(entity, entityDescriptor.TableDescriptor);
                }
                else
                {
                    using (SelectEntitiesQuery getQuery = connection.GetSelectEntitiesQuery(entityType))
                    {
                        string key = getQuery.Where.PropertyName(pKey);
                        string expression = getQuery.LanguageSpecifics.GetOp(CmpOp.Eq, key, sourceId.ToString());
                        getQuery.Where.Add(LogOp.And, expression);
                        entity = asyncCall ? await getQuery.ReadOneAsync(token) : getQuery.ReadOne();
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
                            if (fieldName == CanDeleteName) pCanDeleteJValue = (JValue)item.Value;
                            object value = ((JValue)item.Value).Value;
                            ChangeFieldValueInEntity(entity, fieldName, value);
                        }
                    }
                }
                ModifyEntityQuery query;
                if (sourceId <= 0)
                    query = connection.GetInsertEntityQuery(entityType);
                else
                    query = connection.GetUpdateEntityQuery(entityType);

                if (asyncCall)
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
                    bool recordCanBeDeleted = (sourceId <= 0) || this.CanDelete(entityType, (int)newKey);
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

        private void FillEntityWithDefaultValues(object entity, TableDescriptor tableDescriptor)
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

        private object CreateEntity(Type type, int id)
        {
            object entity = Activator.CreateInstance(type);
            EntityDescriptor entityDescriptor = AllEntities.Inst[entity.GetType()];
            string pKey = entityDescriptor.TableDescriptor.PrimaryKey.ID;
            PropertyInfo propertyInfo = entity.GetType().GetProperty(pKey);
            propertyInfo.SetValue(entity, id);

            return entity;
        }

        private void ChangeFieldValueInEntity(object entity, string fieldName, object value)
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
                        object innerObj = CreateEntity(propertyType, Int32.Parse(value.ToString()));
                        if (innerObj != null)
                        {
                            propertyInfo.SetValue(entity, innerObj);
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

        private string Encode(string source)
        {
            return source.Replace(" ", "%20");
        }

        public bool CanDelete(Type type, int id)
        {
            SqlDbConnection connection = mConnectionFactory.GetConnection();
            bool result = false;
            try
            {
                result = connection.CanDelete(CreateEntity(type, id));
            }
            finally
            {
                if (mConnectionFactory.NeedDispose)
                    connection.Dispose();
            }
            return result;
        }
    }
}