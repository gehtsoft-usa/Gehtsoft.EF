using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Legacy.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class ODataProcessorTests : IClassFixture<ODataProcessorTests.Fixture>
    {
        public class Fixture : IDisposable
        {
            public SqlDbConnection Connection { get; }
            public ISqlDbConnectionFactory ConnectionFactory { get; }
            public EdmModelBuilder ModelBuilder { get; }
            public ODataProcessor Processor { get; }

            public Fixture()
            {
                Connection = SqliteDbConnectionFactory.CreateMemory();

                // Create tables in FK order
                Create(Connection, typeof(Employee));
                Create(Connection, typeof(Category));
                Create(Connection, typeof(Good));
                Create(Connection, typeof(Sale));

                // Populate test data (replicating TestEntity1.TestEntities data setup)
                PopulateData(Connection);

                // Build the OData model from entities with scope "entities"
                var entities = EntityFinder.FindEntities(new Assembly[] { typeof(Employee).Assembly }, "entities", false);
                ModelBuilder = new EdmModelBuilder();
                ModelBuilder.Build(entities, "entities");

                ConnectionFactory = new ExistingConnectionFactory(Connection);
                Processor = new ODataProcessor(ConnectionFactory, ModelBuilder, "");
            }

            private static void PopulateData(SqlDbConnection connection)
            {
                // Insert employees
                Employee boss = new Employee("Boss") { EmpoyeeType1 = EmpoyeeType.Manager };
                Employee mgr1 = new Employee("Manager1", boss);
                Employee mgr2 = new Employee("Manager2", boss);
                Employee sm1 = new Employee("Salesman1", mgr1);
                Employee sm2 = new Employee("Salesman5", mgr2);
                Employee sm3 = new Employee("Salesman3", mgr2);
                Employee sm4 = new Employee("Salesman4", mgr2);
                Employee sm5 = new Employee("Salesman5", mgr2);

                using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Employee)))
                {
                    query.Execute(boss);
                    query.Execute(mgr1);
                    query.Execute(mgr2);
                    query.Execute(sm1);
                    query.Execute(sm2);
                    query.Execute(sm3);
                    query.Execute(sm4);
                    query.Execute(sm5);
                }

                // Update sm2 and delete sm5 (same as TestEntity1.TestEntities)
                using (ModifyEntityQuery query = connection.GetUpdateEntityQuery(typeof(Employee)))
                {
                    sm2.Name = "Salesman2";
                    sm2.Manager = mgr1;
                    sm2.LastCheck = new DateTime(2015, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
                    query.Execute(sm2);
                }

                using (ModifyEntityQuery query = connection.GetDeleteEntityQuery(typeof(Employee)))
                {
                    query.Execute(sm5);
                }

                // Insert categories
                Category cat1 = new Category(100, "Food");
                Category cat2 = new Category(200, "Clothes");

                using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Category)))
                {
                    query.Execute(cat1);
                    query.Execute(cat2);
                }

                // Insert goods
                Good good1 = new Good(cat1, "Bread");
                Good good2 = new Good(cat1, "Milk");
                Good good3 = new Good(cat2, "Socks");
                Good good4 = new Good(cat2, "Pants");
                Good good5 = new Good(cat2, "Trousers");

                Good[] goods = new Good[] { good1, good2, good3, good4, good5 };

                using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Good)))
                {
                    query.Execute(good1);
                    query.Execute(good2);
                    query.Execute(good3);
                    query.Execute(good4);
                    query.Execute(good5);
                }

                // Insert 100 sales with deterministic random data
                Employee[] sms = new Employee[] { sm1, sm2, sm3, sm4 };
                Random r = new Random(42); // fixed seed for reproducibility
                DateTime dt = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

                using (SqlDbTransaction transaction = connection.BeginTransaction())
                {
                    using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Sale)))
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            Sale sale = new Sale();
                            if (i == 10)
                                sale.SalesPerson = sm4;
                            else
                                sale.SalesPerson = sms[r.Next(sms.Length)];
                            sale.Good = goods[r.Next(goods.Length)];
                            sale.SalesDate = dt.AddDays(i);
                            sale.Total = 50 + r.Next(50);
                            if (r.Next(5) == 1)
                            {
                                do
                                {
                                    sale.ReferencePerson = sms[r.Next(sms.Length)];
                                } while (sale.SalesPerson == sale.ReferencePerson);
                            }
                            query.Execute(sale);
                        }
                    }
                    transaction.Commit();
                }
            }

            private static void Create(SqlDbConnection connection, Type type)
            {
                EntityQuery query;
                using (query = connection.GetCreateEntityQuery(type))
                    query.Execute();
            }

            public void Dispose()
            {
                Drop(Connection, typeof(Sale));
                Drop(Connection, typeof(Good));
                Drop(Connection, typeof(Category));
                Drop(Connection, typeof(Employee));
                Connection?.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        private readonly Fixture mFixture;

        public ODataProcessorTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        private static void Drop(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetDropEntityQuery(type))
                query.Execute();
        }

        [Fact]
        public void TestSelectData()
        {
            var processor = mFixture.Processor;
            var modelBuilder = mFixture.ModelBuilder;

            object result = processor.SelectData(new Uri("/Employee/$count", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(Int64));

            long count = (long)result;

            result = processor.SelectData(new Uri("/Employee", UriKind.Relative));
            IEnumerable<object> array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().Be(count);

            result = processor.SelectData(new Uri("/Employee(1)", UriKind.Relative));
            Dictionary<string, object> data = result as Dictionary<string, object>;
            data["ID"].Should().Be(1);

            result = processor.SelectData(new Uri("/Sale?$skip=1&$top=2", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().Be(2);

            result = processor.SelectData(new Uri("/Sale(19)/Good", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data.Should().NotBeNull();

            string goodName = (string)data["Name"];
            Dictionary<string, object> dataItem;

            result = processor.SelectData(new Uri("/Sale(19)/Good/Sale?$expand=Good", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object entity in array)
            {
                data = entity as Dictionary<string, object>;
                dataItem = data["Good"] as Dictionary<string, object>;
                dataItem["Name"].Should().Be(goodName);
            }

            result = processor.SelectData(new Uri("/Sale(19)/Good/Sale(19)?$expand=Good", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data["ID"].Should().Be(19);
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem["Name"].Should().Be(goodName);

            result = processor.SelectData(new Uri("/Sale(1)/SalesDate", UriKind.Relative));
            result.Should().BeOfType(typeof(DateTime));

            result = processor.SelectData(new Uri("/Sale?$select=ID,SalesDate&$expand=Good", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            data = array.FirstOrDefault() as Dictionary<string, object>;
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem.Should().NotBeNull();
            dataItem["Name"].Should().NotBeNull();

            result = processor.SelectData(new Uri("/Sale?$expand=Good($select=Name;$expand=Category($select=Name))", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            data = array.FirstOrDefault() as Dictionary<string, object>;
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem["Name"].Should().NotBeNull();
            dataItem = dataItem["Category"] as Dictionary<string, object>;
            dataItem.Should().NotBeNull();
            dataItem["Name"].Should().NotBeNull();

            result = processor.SelectData(new Uri("/Sale?$expand=Good($expand=Category),SalesPerson", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            data = array.FirstOrDefault() as Dictionary<string, object>;
            dataItem = data["Good"] as Dictionary<string, object>;
            dataItem["Name"].Should().NotBeNull();
            dataItem = data["SalesPerson"] as Dictionary<string, object>;
            dataItem.Should().NotBeNull();
            dataItem["Name"].Should().NotBeNull();

            result = processor.SelectData(new Uri("/Category?$top=1&$expand=Good($filter=Name eq 'Bread')", UriKind.Relative));
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

            var item1 = array.First();
            data = item1 as Dictionary<string, object>;
            var categoryId = (int)data["ID"];

            result = processor.SelectData(new Uri($"/Category({categoryId})?$expand=Good", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data.Should().NotBeNull();
            dataList = data["Good"] as IEnumerable<object>;
            dataList.Should().NotBeNull();

            result = processor.SelectData(new Uri("/Good?$expand=Sale($filter=SalesDate gt 2010-01-31;$orderby=SalesDate desc)&$filter=Name eq 'Bread' or ID ne 5", UriKind.Relative));
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
                    DateTime.Compare(saleDate.Value, new DateTime(2010, 1, 31, 0, 0, 0, DateTimeKind.Unspecified)).Should().BeGreaterThan(0);
                }
            }

            result = processor.SelectData(new Uri("/Good?$filter=not contains(tolower(Name),'e')", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                (!((string)data["Name"]).ToLower().Contains('e')).Should().BeTrue();
            }

            result = processor.SelectData(new Uri("/Good/$count", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(Int64));

            result = processor.SelectData(new Uri("/Good?$filter=startswith(tolower(Name), 'br')", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().BeGreaterThan(0);
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                ((string)data["Name"]).StartsWith("br", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            }

            result = processor.SelectData(new Uri("/Good?$filter=endswith(tolower(Name), 'ad') eq true", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            ((long)array.Count()).Should().BeGreaterThan(0);
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                ((string)data["Name"]).EndsWith("ad", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            }

            result = processor.SelectData(new Uri("/Good?$filter=ID in (2,3)", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                int id = (int)data["ID"];
                (id == 2 || id == 3).Should().BeTrue();
            }

            result = processor.SelectData(new Uri("/Good?$filter=Name in ('Bread', 'Milk')", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            foreach (object item in array)
            {
                data = item as Dictionary<string, object>;
                string name = (string)data["Name"];
                (name == "Bread" || name == "Milk").Should().BeTrue();
            }

            result = processor.SelectData(new Uri("/Sale?$select=SalesDate,Total&&$inlinecount=allpages&$filter=SalesDate ge 2010-04-04", UriKind.Relative));
            array = (result as Dictionary<string, object>)["value"] as IEnumerable<object>;
            array.Should().NotBeNull();
            array.Count().Should().BeGreaterThan(0);

            result = processor.SelectData(new Uri("/Sale/$count?$expand=Good($expand=Category),SalesPerson", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(Int64));

            count = (long)result;

            int salePagingLimit = modelBuilder.EntityPagingLimitByName("Sale_Type");
            result = processor.SelectData(new Uri("/Sale?$select=SalesDate,Total&$expand=Good($expand=Category),SalesPerson&$orderby=SalesDate desc,Total,Good/Name&$inlinecount=allpages", UriKind.Relative));
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
                result = processor.SelectData(new Uri(processor.GetRelativeUrl(nextLink), UriKind.Relative));
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
                        array.Count().Should().BeLessThanOrEqualTo(salePagingLimit);
                    }
                }
            }

            result = processor.SelectData(new Uri("/Sale(9999)", UriKind.Relative));
            (result as Dictionary<string, object>).ContainsKey("odata.error").Should().BeTrue();

            string str = processor.GetFormattedData(new Uri("/Employee/$count?$format=json", UriKind.Relative));
            int.TryParse(str, out int _).Should().BeTrue();

            str = processor.GetFormattedData(new Uri("/Sale(1)/SalesDate", UriKind.Relative));
            DateTime.TryParse(str, CultureInfo.InvariantCulture, out DateTime _).Should().BeTrue();

            str = processor.GetFormattedData(new Uri("/Sale?$select=ID,SalesDate&$expand=Good($select=Name)", UriKind.Relative));
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
                    string dateStr = value[0]["SalesDate"].Value<string>();
                    dateStr.Should().NotBeNull();

                    (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out DateTime dt)).Should().BeTrue();

                    JValue next = (JValue)obj["odata.nextLink"];
                    ((IEnumerable<JToken>)next).Should().NotBeNull();
                    string nextStr = next.Value.ToString();

                    Uri testUri = new Uri(nextStr, UriKind.Relative);
                    testUri.Should().NotBeNull();
                }
            }

            str = processor.GetFormattedData(new Uri("/Sale?$format=atom", UriKind.Relative));
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
                    code.Value.ToString().Should().Be(nameof(EfODataExceptionCode.UnsupportedFormat));
                }
            }

            str = processor.GetFormattedData(new Uri("/Sale?$expand=Good($select=Name)&$inlinecount=allpages&$format=xml", UriKind.Relative));
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
                string dateStr = dateAttr.Value;
                dateStr.Should().NotBeNull();
                DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out DateTime _).Should().BeTrue();

                XmlNode saleTotal = node.SelectSingleNode("item[@key='Total']");
                saleTotal.Should().NotBeNull();
                XmlAttribute totalAttr = saleTotal.Attributes["value"];
                totalAttr.Should().NotBeNull();
                string totalStr = totalAttr.Value;
                totalStr.Should().NotBeNull();
                double.TryParse(totalStr, out double ttl).Should().BeTrue();
                ttl.Should().BeGreaterThan(0);

                XmlNode saleGood = node.SelectSingleNode("item[@key='Good']");
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
            result = processor.SelectData(new Uri("/Good/$count", UriKind.Relative));
            long goodCount = (long)result;

            // Add a new Good
            string crudResult = processor.AddNewRecord("Good", "{Category:100,Name:'MyGood'}", out bool wasError);
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
                    (Int32.TryParse(categoryValue.Value.ToString(), out int category)).Should().BeTrue();
                    (category == 100).Should().BeTrue();

                    JValue nameValue = (JValue)obj["Name"];
                    ((IEnumerable<JToken>)nameValue).Should().NotBeNull();
                    (nameValue.Value.ToString() == "MyGood").Should().BeTrue();
                }
            }

            // Try to add the same new Good
            crudResult = processor.AddNewRecord("Good", "{Category:100,Name:'MyGood'}", out wasError);
            crudResult.Should().NotBeNull();
            wasError.Should().BeTrue();

            // get number of goods after
            result = processor.SelectData(new Uri("/Good/$count", UriKind.Relative));
            (goodCount + 1 == (long)result).Should().BeTrue();

            // Edit the new added Good
            crudResult = processor.UpdateRecord("Good", "{Category:100,Name:'ChangedGood'}", newGoodId, out wasError);
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
                    (Int32.TryParse(idValue.Value.ToString(), out int id)).Should().BeTrue();
                    (newGoodId == id).Should().BeTrue();

                    JValue categoryValue = (JValue)obj["Category"];
                    ((IEnumerable<JToken>)categoryValue).Should().NotBeNull();
                    (Int32.TryParse(categoryValue.Value.ToString(), out int category)).Should().BeTrue();
                    (category == 100).Should().BeTrue();

                    JValue nameValue = (JValue)obj["Name"];
                    ((IEnumerable<JToken>)nameValue).Should().NotBeNull();
                    (nameValue.Value.ToString() == "ChangedGood").Should().BeTrue();
                }
            }

            // Read the new Good and compare values
            result = processor.SelectData(new Uri($"/Good({newGoodId})", UriKind.Relative));
            data = result as Dictionary<string, object>;
            data.Should().NotBeNull();
            ((string)data["Name"] == "ChangedGood").Should().BeTrue();
            ((int)data["ID"] == newGoodId).Should().BeTrue();
            ((long)data["category"] == 100).Should().BeTrue();

            // Delete the added Good
            crudResult = processor.RemoveRecord("Good", newGoodId);
            // check that there was not an error
            crudResult.Should().BeNull();

            // get number of goods after
            result = processor.SelectData(new Uri("/Good/$count", UriKind.Relative));
            (goodCount == (long)result).Should().BeTrue();
        }
    }
}
