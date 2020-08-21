using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Entities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OData.Edm;
using FluentAssertions;
using Microsoft.OData.UriParser;
using System.Security.Policy;
using Microsoft.OData;
using System.Xml;
using System.IO;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.Edm.Csdl;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using static TestApp.TestEntity1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestApp
{
    [TestFixture]
    public class ODataProcessorTest
    {
        private EdmModelBuilder mModelBuilder;
        private ODataProcessor mPocessor;
        private ISqlDbConnectionFactory mConnectionFactory;

        public ISqlDbConnectionFactory MConnectionFactory { get => mConnectionFactory; set => mConnectionFactory = value; }

        [OneTimeSetUp]
        public void Setup()
        {
            MConnectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=d:\test.db");
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { this.GetType().Assembly }, "entities", false);
            EdmModelBuilder builder = new EdmModelBuilder();
            builder.Build(entities, "entities");
            mModelBuilder = builder;

            mPocessor = new ODataProcessor(MConnectionFactory, builder, "https://services.odata.org/V3/OData/OData.svc");
            using (SqlDbConnection connection = MConnectionFactory.GetConnection())
            {
                TestEntity1.TestEntities(connection);
            }
        }

        [Test]
        public void TestSelectData()
        {
            object result;

            result = mPocessor.SelectData(new Uri($"/Employee/$count", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(Int64));

            long count = (long)result;

            result = mPocessor.SelectData(new Uri($"/Employee", UriKind.Relative));
            IEnumerable<object> array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().Be(count);

            result = mPocessor.SelectData(new Uri($"/Employee(1)", UriKind.Relative));
            Dictionary<string, object> data = result as Dictionary<string, object>;
            data["ID"].Should().Be(1);

            result = mPocessor.SelectData(new Uri($"/Sale?$skip=1&$top=2", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().Be(2);

            result = mPocessor.SelectData(new Uri($"/Sale(19)/Good", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data.Should().NotBeNull();

            string goodName = (string)data["Name"];
            Dictionary<string, object> dataItem;

            result = mPocessor.SelectData(new Uri($"/Sale(19)/Good/Sale?$expand=Good", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object entity in array)
            {
                data = entity as Dictionary<string, object>;
                dataItem = data["Good"] as Dictionary<string, object>;
                dataItem["Name"].Should().Be(goodName);
            }

            result = mPocessor.SelectData(new Uri($"/Sale(19)/Good/Sale(19)?$expand=Good", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data["ID"].Should().Be(19);
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem["Name"].Should().Be(goodName);

            result = mPocessor.SelectData(new Uri($"/Sale(1)/SalesDate", UriKind.Relative));
            result.Should().BeOfType(typeof(DateTime));

            result = mPocessor.SelectData(new Uri($"/Sale?$select=ID,SalesDate&$expand=Good", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            data = array.FirstOrDefault() as Dictionary<string, object>;
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem.Should().NotBeNull();
            dataItem["Name"].Should().NotBeNull();

            result = mPocessor.SelectData(new Uri($"/Sale?$expand=Good($select=Name;$expand=Category($select=Name))", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            data = array.FirstOrDefault() as Dictionary<string, object>;
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem["Name"].Should().NotBeNull();
            dataItem = dataItem["Category"] as Dictionary<string, object>;
            dataItem.Should().NotBeNull();
            dataItem["Name"].Should().NotBeNull();

            result = mPocessor.SelectData(new Uri($"/Sale?$expand=Good($expand=Category),SalesPerson", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            data = array.FirstOrDefault() as Dictionary<string, object>;
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem["Name"].Should().NotBeNull();
            dataItem = data["SalesPerson"] as Dictionary<string, object>;
            dataItem.Should().NotBeNull();
            dataItem["Name"].Should().NotBeNull();

            result = mPocessor.SelectData(new Uri($"/Category?$top=1&$expand=Good($filter=Name eq 'Bread')", UriKind.Relative));
            //using (StringWriter writer = new StringWriter())
            //{
            //    using (JsonTextWriter jswriter = new JsonTextWriter(writer))
            //    {
            //        JsonSerializer sr = JsonSerializer.Create();
            //        sr.Formatting = Newtonsoft.Json.Formatting.None;
            //        sr.NullValueHandling = NullValueHandling.Ignore;
            //        sr.StringEscapeHandling = StringEscapeHandling.Default;
            //        sr.Serialize(jswriter, result);
            //    }
            //    string qqq = writer.ToString();
            //}
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            data = array.FirstOrDefault() as Dictionary<string, object>;
            IEnumerable<object> dataList = data["Good"] as IEnumerable<object>;
            dataList.Should().NotBeNull();
            foreach (object obj in dataList)
            {
                Dictionary<string, object> sub = obj as Dictionary<string, object>;
                sub.Should().NotBeNull();
                (sub["Name"] as string).Should().Be("Bread");
            }

            int categoryId = 0;
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                categoryId = (int)data["ID"];
                break;
            }

            result = mPocessor.SelectData(new Uri($"/Category({categoryId})?$expand=Good", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data.Should().NotBeNull();
            dataList = data["Good"] as IEnumerable<object>;
            dataList.Should().NotBeNull();

            result = mPocessor.SelectData(new Uri($"/Good?$expand=Sale($filter=SalesDate gt 2010-01-31;$orderby=SalesDate desc)&$filter=Name eq 'Bread' or ID ne 5", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                ((int)data["ID"] != 5 || (string)data["Name"] == "Bread").Should().BeTrue();
                dataList = data["Sale"] as IEnumerable<object>;
                dataList.Should().NotBeNull();
                foreach (object saleObject in dataList)
                {
                    Dictionary<string, object> sale = saleObject as Dictionary<string, object>;
                    DateTime? saleDate = (DateTime)sale["SalesDate"];
                    saleDate.Should().NotBeNull();
                    DateTime.Compare(saleDate.Value, new DateTime(2010, 1, 31)).Should().BeGreaterThan(0);
                }
            }

            result = mPocessor.SelectData(new Uri($"/Good?$filter=not contains(tolower(Name),'e')", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                (!((string)data["Name"]).ToLower().Contains('e')).Should().BeTrue();
            }

            result = mPocessor.SelectData(new Uri($"/Good/$count", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(Int64));

            count = (long)result;

            result = mPocessor.SelectData(new Uri($"/Good?$filter=trimleft(concat(' ', Name)) eq Name", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().Be(count);

            result = mPocessor.SelectData(new Uri($"/Good?$filter=startswith(tolower(Name), 'br')", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().BeGreaterThan(0);
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                ((string)data["Name"]).ToLower().StartsWith("br").Should().BeTrue();
            }

            result = mPocessor.SelectData(new Uri($"/Good?$filter=endswith(tolower(Name), 'ad') eq true", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().BeGreaterThan(0);
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                ((string)data["Name"]).ToLower().EndsWith("ad").Should().BeTrue();
            }

            result = mPocessor.SelectData(new Uri($"/Good?$filter=ID in (2,3)", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                int id = (int)data["ID"];
                (id == 2 || id == 3).Should().BeTrue();
            }

            result = mPocessor.SelectData(new Uri($"/Good?$filter=Name in ('Bread', 'Milk')", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                string name = (string)data["Name"];
                (name == "Bread" || name == "Milk").Should().BeTrue();
            }

            result = mPocessor.SelectData(new Uri($"/Sale?$select=SalesDate,Total&&$inlinecount=allpages&$filter=SalesDate ge 2010-04-04", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            array.Count().Should().BeGreaterThan(0);

            result = mPocessor.SelectData(new Uri($"/Sale/$count?$expand=Good($expand=Category),SalesPerson", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(Int64));

            count = (long)result;

            int salePagingLimit = mModelBuilder.EntityPagingLimitByName("Sale_Type");
            result = mPocessor.SelectData(new Uri($"/Sale?$select=SalesDate,Total&$expand=Good($expand=Category),SalesPerson&$orderby=SalesDate desc,Total,Good/Name&$inlinecount=allpages", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            if (salePagingLimit > 0)
            {
                array.Count().Should().Be(salePagingLimit);
            }
            else
            {
                array.Count().Should().BeGreaterThan(0);
            }
            array.Should().NotBeNull();
            long total = (long)(result as Dictionary<string, object>)["odata.count"];
            total.Should().Be(count);

            string nextLink = null;
            if ((result as Dictionary<string, object>).ContainsKey("odata.nextLink"))
                nextLink = (string)(result as Dictionary<string, object>)["odata.nextLink"];
            nextLink.Should().NotBeNull();
            if (salePagingLimit > 0)
            {
                array.Count().Should().Be(salePagingLimit);
            }
            else
            {
                array.Count().Should().BeGreaterThan(0);
            }
            while (nextLink != null)
            {
                result = mPocessor.SelectData(new Uri(mPocessor.GetRelativeUrl(nextLink), UriKind.Relative));
                array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
                nextLink = null;
                if ((result as Dictionary<string, object>).ContainsKey("odata.nextLink"))
                {
                    if (salePagingLimit > 0)
                    {
                        array.Count().Should().Be(salePagingLimit);
                    }
                    else
                    {
                        array.Count().Should().BeGreaterThan(0);
                    }
                    nextLink = (string)(result as Dictionary<string, object>)["odata.nextLink"];
                }
                else
                {
                    if (salePagingLimit > 0)
                    {
                        array.Count().Should().BeLessOrEqualTo(salePagingLimit);
                    }
                }
            }

            result = mPocessor.SelectData(new Uri($"/Sale(9999)", UriKind.Relative));
            (result as Dictionary<string, object>).ContainsKey("odata.error").Should().BeTrue();

            string str = mPocessor.GetFormattedData(new Uri($"/Employee/$count?$format=json", UriKind.Relative));
            int testi = 0;
            int.TryParse(str, out testi).Should().BeTrue();

            str = mPocessor.GetFormattedData(new Uri($"/Sale(1)/SalesDate", UriKind.Relative));
            DateTime testd;
            DateTime.TryParse(str, out testd).Should().BeTrue();

            str = mPocessor.GetFormattedData(new Uri($"/Sale?$select=ID,SalesDate&$expand=Good($select=Name)", UriKind.Relative));
            using (StringReader reader = new StringReader(str))
            {
                using (JsonTextReader jsreader = new JsonTextReader(reader))
                {
                    JsonSerializer sr = JsonSerializer.Create();
                    sr.Formatting = Newtonsoft.Json.Formatting.None;
                    sr.NullValueHandling = NullValueHandling.Ignore;
                    sr.StringEscapeHandling = StringEscapeHandling.Default;
                    JObject obj = (JObject)sr.Deserialize(jsreader);

                    JArray value = (JArray)obj["value"];
                    value.Should().NotBeNull();
                    if (salePagingLimit > 0)
                    {
                        value.Count.Should().Be(salePagingLimit);
                    }
                    else
                    {
                        value.Count().Should().BeGreaterThan(0);
                    }
                    DateTime dt = DateTime.MinValue;
                    string dateStr = value[0]["SalesDate"].Value<string>();
                    dateStr.Should().NotBeNull();

                    (DateTime.TryParse(dateStr, out dt)).Should().BeTrue();

                    JValue next = (JValue)obj["odata.nextLink"];
                    ((IEnumerable<JToken>)next).Should().NotBeNull();
                    string nextStr = next.Value.ToString();

                    Uri testUri = new Uri(nextStr);
                    testUri.Should().NotBeNull();
                }
            }

            str = mPocessor.GetFormattedData(new Uri($"/Sale?$format=atom", UriKind.Relative));
            using (StringReader reader = new StringReader(str))
            {
                using (JsonTextReader jsreader = new JsonTextReader(reader))
                {
                    JsonSerializer sr = JsonSerializer.Create();
                    sr.Formatting = Newtonsoft.Json.Formatting.None;
                    sr.NullValueHandling = NullValueHandling.Ignore;
                    sr.StringEscapeHandling = StringEscapeHandling.Default;
                    JObject obj = (JObject)sr.Deserialize(jsreader);

                    JObject error = (JObject)obj["odata.error"];
                    error.Should().NotBeNull();
                    JValue code = (JValue)error["code"];
                    code.Value.ToString().Should().Be(EfODataExceptionCode.UnsupportedFormat.ToString());
                }
            }

            str = mPocessor.GetFormattedData(new Uri($"/Sale?$expand=Good($select=Name)&$inlinecount=allpages&$format=xml", UriKind.Relative));
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(str);
            XmlNode rootNode = doc.DocumentElement;
            XmlNode valueNode = rootNode.SelectSingleNode("item[@key='value']");
            valueNode.Should().NotBeNull();
            XmlNodeList sets = valueNode.SelectNodes("set");
            if (salePagingLimit > 0)
            {
                sets.Count.Should().Be(salePagingLimit);
            }
            else
            {
                sets.Count.Should().BeGreaterThan(0);
            }
            foreach (XmlNode node in sets)
            {
                XmlNode saleDate = node.SelectSingleNode("item[@key='SalesDate']");
                saleDate.Should().NotBeNull();
                XmlAttribute dateAttr = saleDate.Attributes["value"];
                dateAttr.Should().NotBeNull();
                DateTime dt = DateTime.MinValue;
                string dateStr = dateAttr.Value;
                dateStr.Should().NotBeNull();
                DateTime.TryParse(dateStr, out dt).Should().BeTrue();

                XmlNode saleTotal = node.SelectSingleNode("item[@key='Total']");
                saleTotal.Should().NotBeNull();
                XmlAttribute totalAttr = saleTotal.Attributes["value"];
                totalAttr.Should().NotBeNull();
                double ttl = 0;
                string totalStr = totalAttr.Value;
                totalStr.Should().NotBeNull();
                double.TryParse(totalStr, out ttl).Should().BeTrue();
                ttl.Should().BeGreaterThan(0);

                XmlNode saleGood= node.SelectSingleNode("item[@key='Good']");
                saleGood.Should().NotBeNull();
                saleGood.Attributes["value"].Should().BeNull();
                XmlNodeList setsGood = saleGood.SelectNodes("set");
                setsGood.Count.Should().Be(1);
                XmlNode saleGoodNameNode = setsGood[0].SelectSingleNode("item[@key='Name']");
                saleGoodNameNode.Should().NotBeNull();
                XmlAttribute saleGoodNameAttr = saleGoodNameNode.Attributes["value"];
                saleGoodNameAttr.Should().NotBeNull();
                saleGoodNameAttr.Value.Length.Should().BeGreaterThan(0);
            }

            //
            // Testing CRUD operations
            //

            // get number of goods before
            result = mPocessor.SelectData(new Uri($"/Good/$count", UriKind.Relative));
            long goodCount = (long)result;

            // Add a new Good
            bool wasError;
            string crudResult = mPocessor.AddNewRecord("Good", "{Category:100,Name:'MyGood'}", out wasError);
            wasError.Should().BeFalse();

            int newGoodId = 0;
            // Check that 'ID' was added
            using (StringReader reader = new StringReader(crudResult))
            {
                using (JsonTextReader jsreader = new JsonTextReader(reader))
                {
                    JsonSerializer sr = JsonSerializer.Create();
                    sr.Formatting = Newtonsoft.Json.Formatting.None;
                    sr.NullValueHandling = NullValueHandling.Ignore;
                    sr.StringEscapeHandling = StringEscapeHandling.Default;
                    JObject obj = (JObject)sr.Deserialize(jsreader);

                    JValue idValue = (JValue)obj["ID"];
                    ((IEnumerable<JToken>)idValue).Should().NotBeNull();
                    (Int32.TryParse(idValue.Value.ToString(), out newGoodId)).Should().BeTrue();
                    (newGoodId > 0).Should().BeTrue();

                    JValue categoryValue = (JValue)obj["Category"];
                    ((IEnumerable<JToken>)categoryValue).Should().NotBeNull();
                    int category;
                    (Int32.TryParse(categoryValue.Value.ToString(), out category)).Should().BeTrue();
                    (category == 100).Should().BeTrue();

                    JValue nameValue = (JValue)obj["Name"];
                    ((IEnumerable<JToken>)nameValue).Should().NotBeNull();
                    (nameValue.Value.ToString() == "MyGood").Should().BeTrue();
                }
            }

            // Try to add the same new Good
            crudResult = mPocessor.AddNewRecord("Good", "{Category:100,Name:'MyGood'}", out wasError);
            wasError.Should().BeTrue();

            // get number of goods after
            result = mPocessor.SelectData(new Uri($"/Good/$count", UriKind.Relative));
            (goodCount + 1 == (long)result).Should().BeTrue();

            // Edit the new added Good
            crudResult = mPocessor.UpdateRecord("Good", "{Category:100,Name:'ChangedGood'}", newGoodId, out wasError);
            wasError.Should().BeFalse();
            // Check the changes
            using (StringReader reader = new StringReader(crudResult))
            {
                using (JsonTextReader jsreader = new JsonTextReader(reader))
                {
                    JsonSerializer sr = JsonSerializer.Create();
                    sr.Formatting = Newtonsoft.Json.Formatting.None;
                    sr.NullValueHandling = NullValueHandling.Ignore;
                    sr.StringEscapeHandling = StringEscapeHandling.Default;
                    JObject obj = (JObject)sr.Deserialize(jsreader);

                    JValue idValue = (JValue)obj["ID"];
                    ((IEnumerable<JToken>)idValue).Should().NotBeNull();
                    int id;
                    (Int32.TryParse(idValue.Value.ToString(), out id)).Should().BeTrue();
                    (newGoodId == id).Should().BeTrue();

                    JValue categoryValue = (JValue)obj["Category"];
                    ((IEnumerable<JToken>)categoryValue).Should().NotBeNull();
                    int category;
                    (Int32.TryParse(categoryValue.Value.ToString(), out category)).Should().BeTrue();
                    (category == 100).Should().BeTrue();

                    JValue nameValue = (JValue)obj["Name"];
                    ((IEnumerable<JToken>)nameValue).Should().NotBeNull();
                    (nameValue.Value.ToString() == "ChangedGood").Should().BeTrue();
                }
            }

            // Read the new Good and compare values
            result = mPocessor.SelectData(new Uri($"/Good({newGoodId})", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data.Should().NotBeNull();
            ((string)data["Name"] == "ChangedGood").Should().BeTrue();
            ((int)data["ID"] == newGoodId).Should().BeTrue();
            ((long)data["category"] == 100).Should().BeTrue();

            // Delete the added Good
            crudResult = mPocessor.RemoveRecord("Good", newGoodId);
            // check that there was not an error
            crudResult.Should().BeNull();

            // get number of goods after
            result = mPocessor.SelectData(new Uri($"/Good/$count", UriKind.Relative));
            (goodCount == (long)result).Should().BeTrue();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            using (SqlDbConnection connection = MConnectionFactory.GetConnection())
            {
                Drop(connection, typeof(Sale));
                Drop(connection, typeof(Good));
                Drop(connection, typeof(Category));
                Drop(connection, typeof(Employee));
                Drop(connection, typeof(SerializationCallback));
                Drop(connection, typeof(TestDefaults));
            }
        }

        private static void Drop(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetDropEntityQuery(type))
                query.Execute();
        }

    }
}
