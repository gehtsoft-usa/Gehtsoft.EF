using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.Tools.TypeUtils;
using Microsoft.Win32;
using MySqlConnector.Logging;
using NUnit.Framework;
using NUnit.Framework.Internal.Commands;
using NUnit.Framework.Legacy;
using FluentAssertions;

namespace TestApp
{
    public static class TestEntity1
    {
        #region Static entity

        public enum EmpoyeeType
        {
            Manager,
            Salesman,
        }

        [Entity(Table = "tdefault")]
        public class TestDefaults
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "dv", DbType = DbType.String, Size = 128, Sorted = true)]
            public string DVal { get; set; }

            [EntityProperty(DbType = DbType.String, Size = 128, Sorted = true, DefaultValue = "default")]
            public string StringVal { get; set; }

            [EntityProperty(DefaultValue = true)]
            public bool BoolVal { get; set; }

            [EntityProperty(DefaultValue = 123)]
            public int IntVal { get; set; }
        }

        [Entity(Table = "tcallback")]
        public class SerializationCallback : IEntitySerializationCallback
        {
            private readonly static string[] names = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };

            private static string ToName(int? value)
            {
                if (value == null || value < 0 || value >= names.Length)
                    return null;
                return names[(int)value];
            }

            private static int? ToValue(string name)
            {
                if (name == null)
                    return null;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == name)
                        return i;
                }
                return null;
            }

            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(DbType = DbType.Int32, Nullable = true)]
            private int? IntVal { get; set; }

            public string StringValue { get; set; } = "zero";

            public void BeforeSerialization(SqlDbConnection connection)
            {
                IntVal = ToValue(StringValue);
            }

            public void AfterDeserealization(SelectEntitiesQueryBase query)
            {
                StringValue = ToName(IntVal);
            }
        }

        [Entity(Scope = "entities", Table = "temployee")]
        public class Employee
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            [EntityProperty(ForeignKey = true, Nullable = true)]
            public Employee Manager { get; set; }

            [EntityProperty(Field = "type", DbType = DbType.Int32, Sorted = true, Nullable = true)]
            public EmpoyeeType? EmpoyeeType1 { get; set; } = EmpoyeeType.Salesman;

            [EntityProperty(Sorted = true)]
            public DateTime? LastCheck { get; set; }

            public Employee()
            {
            }

            public Employee(string name)
            {
                Name = name;
            }

            public Employee(string name, Employee manager)
            {
                Name = name;
                Manager = manager;
            }

            internal bool Found { get; set; }
        }

        [Entity(Scope = "entities", Table = "tcategory")]
        public class Category
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Name { get; set; }

            public int Count { get; set; }

            public double Total { get; set; }

            public int Count1 { get; set; }

            public double Total1 { get; set; }

            public Category()
            {
            }

            public Category(int id, string name)
            {
                ID = id;
                Name = name;
            }
        }

        [Entity(Scope = "entities", Table = "tgood")]
        public class Good
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "category", ForeignKey = true)]
            public Category Category { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true, Unique = true)]
            public string Name { get; set; }

            public Good()
            {
            }

            public Good(Category cat, string name)
            {
                Category = cat;
                Name = name;
            }
        }

        public class GoodUpdateRecord : GenericEntityAccessorUpdateRecord
        {
            [UpdateRecordProperty(PropertyName = nameof(Good.Category))]
            public Category Category { get; set; }

            public GoodUpdateRecord() : base(typeof(Good))
            {
            }
        }

        public class GoodFilter : GenericEntityAccessorFilter
        {
            [FilterProperty(PropertyName = nameof(Good.Name), Operation = CmpOp.Eq)]
            public string NameIs { get; set; }

            [FilterProperty(PropertyName = nameof(Good.Name), Operation = CmpOp.Like)]
            public string NameStartsWith { get; set; }

            [FilterProperty(PropertyName = nameof(Good.Category), Operation = CmpOp.Eq)]
            public Category Category { get; set; }

            public GoodFilter() : base(typeof(Good))
            {
            }
        }

        private static readonly GenericEntitySortOrder[] goodOrder = new GenericEntitySortOrder[] { new GenericEntitySortOrder(nameof(Good.Name)) };
        private static readonly GenericEntitySortOrder[] goodOrderRev = new GenericEntitySortOrder[] { new GenericEntitySortOrder(nameof(Good.Name), SortDir.Desc) };

        [Entity(Scope = "entities", Table = "tsale", Metadata = typeof(SalesMetadata))]
        [PagingLimit(10)]
        public class Sale
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "saledate", DbType = DbType.DateTime, Sorted = true)]
            public DateTime SalesDate { get; set; }

            [EntityProperty(Field = "salesperson", ForeignKey = true)]
            public Employee SalesPerson { get; set; }

            [EntityProperty(Field = "referenceperson", ForeignKey = true, Nullable = true)]
            public Employee ReferencePerson { get; set; }

            [EntityProperty(Field = "good", ForeignKey = true)]
            public Good Good { get; set; }

            [EntityProperty(Field = "total", DbType = DbType.Double, Size = 8, Precision = 2, DefaultValue = 10)]
            public double Total { get; set; }

            [EntityProperty(Field = "note", Size = 64, Nullable = true)]
            public string Note { get; set; }

            public Sale()
            {
            }

            public Sale(Employee salesPerson, DateTime dateTime, Good good, double total)
            {
                SalesDate = dateTime;
                SalesPerson = salesPerson;
                Good = good;
                Total = total;
            }

            public Sale(Employee salesPerson, Employee reference, DateTime dateTime, Good good, double total)
            {
                SalesDate = dateTime;
                ReferencePerson = reference;
                SalesPerson = salesPerson;
                Good = good;
                Total = total;
            }

            public override string ToString()
            {
                return $"[{ID}]{SalesDate} - {Good.Name} @ {Total} by {SalesPerson.Name}";
            }
        }

        public class SalesMetadata : ICompositeIndexMetadata
        {
            public IEnumerable<CompositeIndex> Indexes
            {
                get
                {
                    CompositeIndex index = new CompositeIndex(typeof(Sale), "Index1")
                    {
                        nameof(Sale.SalesDate),
                        { "salesperson", SortDir.Desc }
                    };
                    yield return index;

                    index = new CompositeIndex(typeof(Sale), "Index2")
                    {
                        FailIfUnsupported = false
                    };
                    index.Add(SqlFunctionId.Upper, nameof(Sale.Note));
                    yield return index;
                }
            }
        }

        public class CustomSaleTargetClass
        {
            public int ID { get; set; }

            public DateTime SalesDate { get; set; }

            public int SaleDay { get; set; }

            public int SaleMonth { get; set; }

            public int SaleYear { get; set; }

            public DayOfWeek SaleDayOfWeek { get; set; }

            public string SalesPersonName { get; set; }

            public string GoodName { get; set; }

            public double Total { get; set; }

            public double AdjustedTotal { get; set; }
        }

        private static readonly GenericEntitySortOrder[] saleOrder = new GenericEntitySortOrder[] { new GenericEntitySortOrder($"{nameof(Sale.SalesPerson)}.{nameof(Employee.Name)}"),
                                                                                   new GenericEntitySortOrder(nameof(Sale.SalesDate), SortDir.Desc),
                                                                                   new GenericEntitySortOrder(nameof(Sale.ID))};

        public class SalesFilter : GenericEntityAccessorFilter
        {
            [FilterProperty(PropertyName = nameof(Sale.ReferencePerson), Operation = CmpOp.IsNull)]
            public bool? HasReferencePerson { get; set; }

            [FilterProperty(PropertyName = nameof(Sale.Good) + "." + nameof(Good.Name), Operation = CmpOp.Eq)]
            public string GoodName { get; set; }

            public SalesFilter() : base(typeof(Sale))
            {
            }
        }

        public class SumOfSales
        {
            public int ID { get; set; }
            public double Total { get; set; }
            public bool Checked { get; set; }
        }

        public class SumOfSalesCollection : IEnumerable<SumOfSales>
        {
            private readonly Dictionary<int, SumOfSales> mDictionary = new Dictionary<int, SumOfSales>();

            public SumOfSales this[int id]
            {
                get
                {
                    if (mDictionary.TryGetValue(id, out SumOfSales s))
                        return s;
                    s = new SumOfSales() { ID = id, Checked = false, Total = 0 };
                    mDictionary[id] = s;
                    return s;
                }
            }

            public IEnumerator<SumOfSales> GetEnumerator()
            {
                return mDictionary.Values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private static void Create(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetCreateEntityQuery(type))
                query.Execute();
        }

        private static void Drop(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetDropEntityQuery(type))
                query.Execute();
        }

        public static void TestEntities(SqlDbConnection connection, bool testHierarchical = true)
        {
            Drop(connection, typeof(Sale));
            Drop(connection, typeof(Good));
            Drop(connection, typeof(Category));
            Drop(connection, typeof(Employee));
            Drop(connection, typeof(SerializationCallback));
            Drop(connection, typeof(TestDefaults));

            Create(connection, typeof(TestDefaults));
            Create(connection, typeof(SerializationCallback));
            Create(connection, typeof(Employee));
            Create(connection, typeof(Category));
            Create(connection, typeof(Good));
            Create(connection, typeof(Sale));

            #region test defaults

            using (SqlDbQuery query = connection.GetQuery(connection.ConnectionType == "oracle" ? "insert into tdefault(id, dv) values (1, :p1)" : "insert into tdefault(dv) values (@p1)"))
            {
                query.BindParam("p1", "1234");
                query.ExecuteNoData();
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<TestDefaults>())
            {
                query.Execute();
                TestDefaults t = query.ReadOne<TestDefaults>();
                ClassicAssert.AreEqual("1234", t.DVal);
                ClassicAssert.AreEqual("default", t.StringVal);
                ClassicAssert.AreEqual(true, t.BoolVal);
                ClassicAssert.AreEqual(123, t.IntVal);
            }

            #endregion test defaults

            #region test callback

            SerializationCallback[] serializationCallbacks = new SerializationCallback[]
            {
                new SerializationCallback() {StringValue = "eleven"},
                new SerializationCallback() {StringValue = "four"},
                new SerializationCallback() {StringValue = "two"},
                new SerializationCallback() {StringValue = "sixteen"},
                new SerializationCallback() {StringValue = "twelve"},
                new SerializationCallback() {StringValue = "one"},
            };

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<SerializationCallback>())
            {
                for (int i = 0; i < serializationCallbacks.Length; i++)
                    query.Execute(serializationCallbacks[i]);
            }

            EntityCollection<SerializationCallback> callbacks1;

            serializationCallbacks[0].StringValue = "five";
            serializationCallbacks[4].StringValue = "ten";

            using (ModifyEntityQuery query = connection.GetUpdateEntityQuery<SerializationCallback>())
            {
                for (int i = 0; i < serializationCallbacks.Length; i++)
                    query.Execute(serializationCallbacks[i]);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<SerializationCallback>())
            {
                query.AddOrderBy<SerializationCallback>(callback => callback.ID);
                callbacks1 = query.ReadAll<SerializationCallback>();
            }

            ClassicAssert.AreEqual(serializationCallbacks.Length, callbacks1.Count);
            for (int i = 0; i < serializationCallbacks.Length; i++)
            {
                ClassicAssert.AreEqual(serializationCallbacks[i].StringValue, callbacks1[i].StringValue);
            }

            #endregion test callback

            #region create tables and data and read using regular queries

            TableDescriptor td = AllEntities.Inst[typeof(Employee)].TableDescriptor;
            ClassicAssert.IsNotNull(td["ID"]);
            ClassicAssert.AreEqual(DbType.Int32, td["ID"].DbType);
            ClassicAssert.AreEqual("id", td["ID"].Name);
            ClassicAssert.IsTrue(td["ID"].PrimaryKey);
            ClassicAssert.IsTrue(td["ID"].Autoincrement);
            ClassicAssert.IsFalse(td["ID"].Nullable);

            ClassicAssert.IsNotNull(td["LastCheck"]);
            ClassicAssert.AreEqual(DbType.DateTime, td["LastCheck"].DbType);
            ClassicAssert.AreEqual("lastcheck", td["LastCheck"].Name);
            ClassicAssert.IsFalse(td["LastCheck"].PrimaryKey);
            ClassicAssert.IsFalse(td["LastCheck"].Autoincrement);
            ClassicAssert.IsTrue(td["LastCheck"].Nullable);

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
                ClassicAssert.IsTrue(boss.ID > 0);
                query.Execute(mgr1);
                query.Execute(mgr2);
                query.Execute(sm1);
                query.Execute(sm2);
                query.Execute(sm3);
                query.Execute(sm4);
                query.Execute(sm5);
            }

            ClassicAssert.IsFalse(connection.CanDelete<Employee>(mgr2));
            ClassicAssert.IsTrue(connection.CanDelete<Employee>(sm5));

            Employee[] sms = new Employee[] { sm1, sm2, sm3, sm4 };

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

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(typeof(Employee)))
            {
                ClassicAssert.AreEqual(7, query.RowCount);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Employee)))
            {
                query.AddOrderBy(nameof(Employee.ID));
                query.Execute();
                EntityCollection<Employee> coll = query.ReadAll<EntityCollection<Employee>, Employee>();
                ClassicAssert.AreEqual(7, coll.Count);

                ClassicAssert.AreEqual(boss.Name, coll[0].Name);
                ClassicAssert.AreEqual(EmpoyeeType.Manager, coll[0].EmpoyeeType1);
                ClassicAssert.IsNull(coll[0].LastCheck);

                ClassicAssert.IsNull(coll[0].Manager);
                ClassicAssert.AreEqual(mgr1.Name, coll[1].Name);
                ClassicAssert.IsNotNull(coll[1].Manager);

                ClassicAssert.AreEqual(boss.ID, coll[1].Manager.ID);
                ClassicAssert.AreEqual(EmpoyeeType.Salesman, coll[1].EmpoyeeType1);

                ClassicAssert.AreEqual("Salesman2", coll[4].Name);
                ClassicAssert.IsNotNull(coll[4].LastCheck);
                ClassicAssert.AreEqual(new DateTime(2015, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), coll[4].LastCheck);
            }

            EntityCollection<Employee> allEmployees;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
            {
                query.AddOrderBy(nameof(Employee.ID));
                allEmployees = query.ReadAll<Employee>();
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
            {
                query.Where.Property(nameof(Employee.Name)).ToUpper().Eq().Value("SALESMAN1");
                var rs = query.ReadAll<Employee>();
                ClassicAssert.AreEqual(1, rs.Count);
                ClassicAssert.AreEqual(allEmployees[3].ID, rs[0].ID);
            }

            if (testHierarchical)
            {
                allEmployees.ForEach(e => e.Found = false);

                using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
                {
                    using (SelectEntitiesTreeQuery query1 = connection.GetSelectEntitiesTreeQuery<Employee>(false))
                    {
                        query1.AddToResultset(nameof(Employee.ID));
                        query.Where.Property(nameof(Employee.ID)).In(query1);
                        query.AddOrderBy($"{nameof(Employee.Manager)}.{nameof(Employee.ID)}");
                    }

                    EntityCollection<Employee> emps = query.ReadAll<Employee>();
                    ClassicAssert.AreEqual(allEmployees.Count, emps.Count);
                    foreach (Employee e1 in emps)
                    {
                        int idx = allEmployees.Find(e1);
                        ClassicAssert.AreNotEqual(-1, idx);
                        allEmployees[idx].Found = true;
                    }
                }

                ClassicAssert.IsTrue(allEmployees.All(e => e.Found));

                allEmployees.ForEach(e => e.Found = false);

                using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
                {
                    using (SelectEntitiesTreeQuery query1 = connection.GetSelectEntitiesTreeQuery<Employee>())
                    {
                        query1.Root = mgr1;
                        query1.AddToResultset(nameof(Employee.ID));
                        query.Where.Property(nameof(Employee.ID)).In(query1);
                        query.AddOrderBy(nameof(Employee.ID));
                    }

                    EntityCollection<Employee> emps = query.ReadAll<Employee>();
                    ClassicAssert.AreEqual(allEmployees.Count(e => e.Manager?.ID == mgr1.ID || e.ID == mgr1.ID), emps.Count);
                    foreach (Employee e1 in emps)
                    {
                        int idx = allEmployees.Find(e1);
                        ClassicAssert.AreNotEqual(-1, idx);
                        allEmployees[idx].Found = true;
                    }
                }

                ClassicAssert.IsTrue(allEmployees.Where(e => e.Manager?.ID == mgr1.ID || e.ID == mgr1.ID).All(e => e.Found));
            }

            Category cat1 = new Category(100, "Food");
            Category cat2 = new Category(200, "Clothes");

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Category)))
            {
                query.Execute(cat1);
                ClassicAssert.AreEqual(100, cat1.ID);
                query.Execute(cat2);
            }

            Good good1 = new Good(cat1, "Bread");
            Good good2 = new Good(cat1, "Milk");
            Good good3 = new Good(cat2, "Socks");
            Good good4 = new Good(cat2, "Pants");
            Good good5 = new Good(cat1, "Troussers");

            Good[] goods = new Good[] { good1, good2, good3, good4, good5 };

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Good)))
            {
                query.Execute(good1);
                query.Execute(good2);
                query.Execute(good3);
                query.Execute(good4);
                query.Execute(good5);
            }

            using (ModifyEntityQuery query = connection.GetUpdateEntityQuery(typeof(Good)))
            {
                good5.Category = cat2;
                good5.Name = "Trousers";
                query.Execute(good5);
            }

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(typeof(Good)))
            {
                ClassicAssert.AreEqual(5, query.RowCount);
            }

            if (connection.GetLanguageSpecifics().CaseSensitiveStringComparison)
            {
                using (var query = connection.GetSelectEntitiesCountQuery<Good>())
                {
                    query.Where.Property(nameof(Good.Name)).ToUpper().Eq().Value("Trousers");
                    query.Execute();
                    query.RowCount.Should().Be(0);
                }
            }

            using (var query = connection.GetSelectEntitiesCountQuery<Good>())
            {
                query.Where.Add(LogOp.Not).Property(nameof(Good.Name)).IsNull();
                query.Where.Property(nameof(Good.Name)).ToUpper().Eq().Value("Trousers").ToUpper();
                query.Execute();
                query.RowCount.Should().Be(1);
            }

            using (var query = connection.GetGenericSelectEntityQuery(typeof(Good)))
            {
                query.AddToResultset(AggFn.Count, null);
                query.Execute();
                query.ReadNext();
                ClassicAssert.AreEqual(5, query.GetValue<int>(0));
            }

            using (var query = connection.GetGenericSelectEntityQuery(typeof(Good)))
            {
                query.AddToResultset(AggFn.Count, nameof(Good.Category));
                query.Execute();
                query.ReadNext();
                ClassicAssert.AreEqual(2, query.GetValue<int>(0));
            }

            List<Sale> sales = new List<Sale>();
            Random r = new Random((int)(DateTime.Now.Ticks & 0x7fffffff));

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
                        sales.Add(sale);
                        sale.Good.Category.Count++;
                        sale.Good.Category.Total += sale.Total;
                    }
                }
                transaction.Commit();
            }
            ClassicAssert.AreNotEqual(good1.Category.Count, 0);
            ClassicAssert.IsFalse(connection.CanDelete<Good>(good1));
            ClassicAssert.IsFalse(connection.CanDelete<Employee>(sm4));

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Category)))
            {
                query.AddEntity(typeof(Good), nameof(Category.ID), true);
                query.AddEntity(typeof(Sale), $"{nameof(Good)}.{nameof(Good.ID)}", true);
                query.AddToResultset(AggFn.Count, null, "salesCount");
                query.AddToResultset(AggFn.Sum, typeof(Sale), nameof(Sale.Total), "salesTotal");
                query.AddOrderBy(nameof(Category.Name));
                query.AddGroupBy(nameof(Category.ID));
                query.Execute();
                EntityCollection<Category> coll = query.ReadAll<EntityCollection<Category>, Category>(
                        (row, entitiesQuery) =>
                        {
                            row.Count = (row.ID == cat1.ID ? cat1.Count : cat2.Count);
                            row.Total = (row.ID == cat1.ID ? cat1.Total : cat2.Total);
                            row.Count1 = entitiesQuery.GetValue<int>("salesCount");
                            row.Total1 = entitiesQuery.GetValue<double>("salesTotal");
                        }
                    );

                ClassicAssert.AreEqual(2, coll.Count);
                foreach (Category cat in coll)
                {
                    ClassicAssert.AreEqual(cat.Count, cat.Count1);
                    ClassicAssert.AreEqual(cat.Total, cat.Total1);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Sale)))
            {
                query.AddOrderBy(nameof(Sale.ID));
                query.Skip = 10;
                query.Limit = 15;
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                ClassicAssert.AreEqual(15, coll.Count);
                ClassicAssert.AreEqual(sales[10].ID, coll[0].ID);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Sale)))
            {
                query.Where.Property($"{nameof(Sale.Good)}.{nameof(Good.ID)}").Eq(good2);
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                ClassicAssert.AreNotEqual(0, coll.Count);
                foreach (Sale sale in coll)
                    ClassicAssert.AreEqual(good2.ID, sale.Good.ID);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Sale)))
            {
                string param = query.Where.Property($"{nameof(Sale.Good)}.{nameof(Good.ID)}").Neq(good2.ID).ParameterName;
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                ClassicAssert.AreNotEqual(0, coll.Count);
                foreach (Sale sale in coll)
                    ClassicAssert.AreNotEqual(good2.ID, sale.Good.ID);

                query.BindParam(param, good3);
                query.Execute();
                coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                ClassicAssert.AreNotEqual(0, coll.Count);
                foreach (Sale sale in coll)
                    ClassicAssert.AreNotEqual(good3.ID, sale.Good.ID);
            }

            SumOfSalesCollection ss = new SumOfSalesCollection();
            foreach (Sale s in sales)
            {
                ss[s.Good.ID].Total += s.Total;
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery(typeof(Good)))
            {
                query.AddEntity(typeof(Sale), nameof(Good.ID));
                query.AddToResultset(typeof(Good), nameof(Good.ID), "id");
                query.AddToResultset(typeof(Good), nameof(Good.Name), "name");
                query.AddToResultset(AggFn.Sum, typeof(Sale), nameof(Sale.Total), "sale");
                query.AddOrderBy(typeof(Good), nameof(Good.Name));
                query.AddGroupBy(typeof(Good), nameof(Good.ID));
                query.Execute();
                while (query.ReadNext())
                {
                    SumOfSales s = ss[query.GetValue<int>("id")];
                    ClassicAssert.IsFalse(s.Checked);
                    ClassicAssert.AreEqual(s.Total, query.GetValue<double>("sale"));
                    s.Checked = true;
                }
            }

            foreach (SumOfSales s in ss)
            {
                ClassicAssert.IsTrue(s.Checked);
                s.Checked = false;
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery(typeof(Good)))
            {
                query.AddEntity(typeof(Sale), nameof(Good.ID));
                query.AddToResultset(typeof(Good), nameof(Good.ID), "Id");
                query.AddToResultset(typeof(Good), nameof(Good.Name), "Name");
                query.AddToResultset(AggFn.Sum, typeof(Sale), nameof(Sale.Total), "Sale");
                query.AddOrderBy(typeof(Good), nameof(Good.Name));
                query.AddGroupBy(typeof(Good), nameof(Good.ID));
                query.Execute();
                IEnumerable<dynamic> all = query.ReadAllDynamic();
                foreach (var one in all)
                {
                    SumOfSales s = ss[(int)one.Id];
                    ClassicAssert.AreEqual(s.ID, one.Id);
                    ClassicAssert.AreEqual(s.Total, one.Sale);
                    s.Checked = true;
                }
            }

            foreach (SumOfSales s in ss)
                ClassicAssert.IsTrue(s.Checked);

            TestLinqAccessor(connection);
            TestLinqQueryable(connection);
            TestLinqQueryable1(connection);

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Good>())
            {
                query.AddEntity(typeof(Sale), nameof(Good.ID));
                query.AddToResultset(AggFn.Sum, typeof(Sale), nameof(Sale.Total), "Total");
                query.AddGroupBy(nameof(Sale.ID));
                query.Execute();
                ICollection<dynamic> all = query.ReadAllDynamic();
                ClassicAssert.AreEqual(5, all.Count);
                foreach (var one in all)
                {
                    SumOfSales s = ss[one.ID];
                    ClassicAssert.IsNotNull(s);
                    ClassicAssert.AreEqual(s.Total, one.Total);
                }
            }

            SelectEntityQueryFilter[] filter = new SelectEntityQueryFilter[]
            {
                new SelectEntityQueryFilter() {Property = nameof(Sale.SalesPerson)},
                new SelectEntityQueryFilter() {Property = nameof(Sale.Total)},
                new SelectEntityQueryFilter() {EntityType = typeof(Good), Property = nameof(Good.Category)},
            };

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>(filter))
            {
                query.Execute();
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                foreach (Sale sale in coll)
                {
                    ClassicAssert.AreNotEqual(new DateTime(0, DateTimeKind.Unspecified), sale.SalesDate);
                    ClassicAssert.IsNull(sale.SalesPerson);
                    ClassicAssert.AreEqual(0, sale.Total);
                    ClassicAssert.IsNotNull(sale.Good);
                    ClassicAssert.IsNull(sale.Good.Category);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                object[] goodids = new object[2] { good1.ID, good3.ID };
                query.Where.PropertyOf<Good>(nameof(Good.ID)).In().Values(goodids);
                query.Execute();
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                ClassicAssert.IsTrue(coll.Count > 0);
                foreach (Sale sale in coll)
                    ClassicAssert.IsTrue(sale.Good.ID == good1.ID || sale.Good.ID == good3.ID);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Good>())
            {
                using (SelectEntitiesQueryBase query1 = connection.GetSelectEntitiesQueryBase<Sale>())
                {
                    query1.AddToResultset(nameof(Sale.ID));
                    query1.AddEntity(typeof(Good));
                    query1.Where.PropertyOf<Good>(nameof(Good.ID)).Eq().Reference(query.GetReference(nameof(Good.ID)));
                    query.Where.Exists(query1);
                    query.Execute();
                }
            }

            Good emptyGood = new Good() { Category = cat1, Name = "New Good" };

            using (var query = connection.GetInsertEntityQuery<Good>())
                query.Execute(emptyGood);

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Good>())
            {
                query.AddEntity(typeof(Sale), TableJoinType.Left, typeof(Good), nameof(Good.ID),
                                                        CmpOp.Eq, typeof(Sale), nameof(Sale.Good));

                query.AddToResultset(typeof(Good), nameof(Good.ID), "Id");
                query.AddToResultset(AggFn.Sum, typeof(Sale), nameof(Sale.Total), "Total");
                query.AddGroupBy(typeof(Good), nameof(Good.ID));
                query.Execute();
                var result = query.ReadAllDynamic();
                bool emptyGoodFound = false;
                foreach (var row in result)
                {
                    if (row.Id == emptyGood.ID)
                    {
                        emptyGoodFound = true;
                        ClassicAssert.AreEqual(0, row.Total);
                    }
                    else
                    {
                        SumOfSales one = ss[row.Id];
                        ClassicAssert.AreEqual(one.Total, row.Total);
                    }
                }
                ClassicAssert.IsTrue(emptyGoodFound);
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Good>())
            {
                query.AddEntity(typeof(Sale), TableJoinType.Left, typeof(Good), nameof(Good.ID), CmpOp.Eq, typeof(Sale), nameof(Sale.Good));
                query.AddToResultset(typeof(Good), nameof(Good.ID), "Id");
                query.AddToResultset(AggFn.Sum, typeof(Sale), nameof(Sale.Total), "Total");
                query.AddGroupBy(typeof(Good), nameof(Good.ID));
                query.Having.PropertyOf<Sale>(nameof(Sale.Total)).Sum().IsNull();
                query.Execute();
                var result = query.ReadAllDynamic();
                ClassicAssert.AreEqual(1, result.Count);
                bool emptyGoodFound = false;
                foreach (var row in result)
                {
                    if (row.Id == emptyGood.ID)
                    {
                        emptyGoodFound = true;
                        ClassicAssert.AreEqual(0, row.Total);
                    }
                }
                ClassicAssert.IsTrue(emptyGoodFound);
            }

            using (var query = connection.GetDeleteEntityQuery<Good>())
                query.Execute(emptyGood);

            double averageSale = 0;
            using (var query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddToResultset(AggFn.Avg, nameof(Sale.Total));
                query.Execute();
                if (query.ReadNext())
                    averageSale = query.GetValue<double>(0);
            }

            ClassicAssert.IsTrue(averageSale > 0);

            using (var query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Property(nameof(Sale.Total)).Ge(averageSale);
                var result = query.ReadAll<Sale>();
                ClassicAssert.IsNotNull(result);
                ClassicAssert.AreNotEqual(0, result.Count);
                foreach (var sale in result)
                    ClassicAssert.IsTrue(sale.Total >= averageSale);
            }

            using (var query = connection.GetSelectEntitiesQuery<Sale>())
            {
                using (var subquery = connection.GetGenericSelectEntityQuery<Sale>())
                {
                    subquery.AddToResultset(AggFn.Avg, nameof(Sale.Total));
                    query.Where.Property(nameof(Sale.Total)).Ge().Query(subquery);
                }
                var result = query.ReadAll<Sale>();
                ClassicAssert.IsNotNull(result);
                ClassicAssert.AreNotEqual(0, result.Count);
                foreach (var sale in result)
                    ClassicAssert.IsTrue(sale.Total >= averageSale);
            }

            using (var query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddToResultset(nameof(Sale.Good), "Id");
                query.AddToResultset(AggFn.Avg, nameof(Sale.Total), "Avg");
                query.AddGroupBy(nameof(Sale.Good));
                query.Having.Property(nameof(Sale.Total)).Avg().Ge().Value(averageSale);
                var result = query.ReadAllDynamic();
                ClassicAssert.IsNotNull(result);
                ClassicAssert.AreNotEqual(0, result.Count);
                foreach (var g in result)
                    ClassicAssert.IsTrue(g.Avg >= averageSale);
            }

            using (var query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddToResultset(nameof(Sale.Good), "Id");
                query.AddToResultset(AggFn.Avg, nameof(Sale.Total), "Avg");
                query.AddGroupBy(nameof(Sale.Good));
                using (var subquery = connection.GetGenericSelectEntityQuery<Sale>())
                {
                    subquery.AddToResultset(AggFn.Avg, nameof(Sale.Total));
                    query.Having.Add().PropertyOf<Sale>(nameof(Sale.Total)).Avg().Is(CmpOp.Ge).Query(subquery);
                }

                var result = query.ReadAllDynamic();
                ClassicAssert.IsNotNull(result);
                ClassicAssert.AreNotEqual(0, result.Count);
                foreach (var g in result)
                    ClassicAssert.IsTrue(g.Avg >= averageSale);
            }

            #endregion create tables and data and read using regular queries

            #region test dynamic query building

            EntityCollection<Good> catGoods;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Good>())
            {
                query.Where.Property(nameof(Good.Category)).Eq(cat2);
                catGoods = query.ReadAll<Good>();
            }

            ClassicAssert.AreNotEqual(0, catGoods.Count);
            ClassicAssert.IsTrue(catGoods.Count > 2);

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Category>())
            {
                query.Where.Property(nameof(Category.ID)).Eq(cat2);

                query.FindType(typeof(Category));
                var e1 = query.AddEntity(typeof(Good), TableJoinType.Left);
                e1.On.And().Reference(query.GetReference(typeof(Category), nameof(Category.ID))).Eq().Reference(query.GetReference(typeof(Good), 0, nameof(Good.Category)));
                e1.On.And().Reference(query.GetReference(typeof(Good), 0, nameof(Good.ID))).Eq().Value(catGoods[0].ID);

                var e2 = query.AddEntity(typeof(Good), TableJoinType.Left);
                e2.On.And().Reference(query.GetReference(typeof(Category), nameof(Category.ID))).Eq().Reference(query.GetReference(typeof(Good), 1, nameof(Good.Category)));
                e2.On.And().Reference(query.GetReference(typeof(Good), 1, nameof(Good.ID))).Eq().Value(catGoods[1].ID);

                query.AddToResultset(nameof(Category.ID), "ID");
                query.AddToResultset(nameof(Category.Name), "Name");
                query.AddToResultset(typeof(Good), 0, nameof(Category.Name), "Good1");
                query.AddToResultset(typeof(Good), 1, nameof(Category.Name), "Good2");

                query.AddOrderBy(typeof(Good), 1, nameof(Good.Name), SortDir.Asc);
                query.Execute();
                IList<dynamic> res = query.ReadAllDynamic();
                ClassicAssert.AreEqual(1, res.Count);
                dynamic r0 = res[0];
                ClassicAssert.AreEqual(cat2.ID, r0.ID);
                ClassicAssert.AreEqual(cat2.Name, r0.Name);
                ClassicAssert.AreEqual(catGoods[0].Name, r0.Good1);
                ClassicAssert.AreEqual(catGoods[1].Name, r0.Good2);
            }

            #endregion test dynamic query building

            #region Test generic entity reader

            GenericEntityAccessor<Good, int> goodEntityAccessor = new GenericEntityAccessor<Good, int>(connection);
            GoodFilter goodFilter = new GoodFilter();

            ClassicAssert.AreEqual(5, goodEntityAccessor.Count(null));
            goodFilter.Reset();
            goodFilter.NameIs = "Bread";
            ClassicAssert.AreEqual(1, goodEntityAccessor.Count(goodFilter));

            goodFilter.Reset();
            goodFilter.NameStartsWith = "B%";
            ClassicAssert.AreEqual(1, goodEntityAccessor.Count(goodFilter));

            goodFilter.Reset();
            goodFilter.Category = cat2;
            ClassicAssert.AreEqual(3, goodEntityAccessor.Count(goodFilter));

            TestGoodAccessorRead(goodEntityAccessor, null);
            TestGoodAccessorRead(goodEntityAccessor, goodFilter);

            Good newGood = new Good() { Category = cat1, Name = "newgood" };
            goodEntityAccessor.Save(newGood);
            ClassicAssert.AreEqual(6, goodEntityAccessor.Count(null));
            ClassicAssert.IsTrue(newGood.ID >= 1);
            Good good = goodEntityAccessor.Get(newGood.ID);
            ClassicAssert.IsNotNull(good);
            ClassicAssert.AreEqual(newGood.Name, good.Name);
            ClassicAssert.AreEqual(newGood.Category.ID, good.Category.ID);
            newGood.Name = "newgoodnewname";
            goodEntityAccessor.Save(newGood);
            ClassicAssert.AreEqual(6, goodEntityAccessor.Count(null));
            ClassicAssert.AreEqual(newGood.ID, good.ID);
            ClassicAssert.AreNotEqual(newGood.Name, good.Name);
            good = goodEntityAccessor.Get(newGood.ID);
            ClassicAssert.AreEqual(newGood.Name, good.Name);
            ClassicAssert.AreEqual("newgoodnewname", good.Name);
            ClassicAssert.AreEqual(newGood.Category.ID, good.Category.ID);
            goodEntityAccessor.Delete(good);
            ClassicAssert.AreEqual(5, goodEntityAccessor.Count(null));
            ClassicAssert.IsNull(goodEntityAccessor.Get(newGood.ID));

            SalesFilter salesFilter = new SalesFilter();
            GenericEntityAccessor<Sale, int> saleAccessor = new GenericEntityAccessor<Sale, int>(connection);
            EntityCollection<Sale> saleCollection1, saleCollection2;

            salesFilter.Reset();
            int totalSalesCount, referencedSalesCount, notReferencedSalesCount;

            totalSalesCount = saleAccessor.Count(null);
            ClassicAssert.AreEqual(totalSalesCount, saleAccessor.Count(salesFilter));

            salesFilter.HasReferencePerson = true;
            referencedSalesCount = saleAccessor.Count(salesFilter);

            salesFilter.HasReferencePerson = false;
            notReferencedSalesCount = saleAccessor.Count(salesFilter);

            ClassicAssert.AreNotEqual(0, referencedSalesCount);
            ClassicAssert.AreNotEqual(0, notReferencedSalesCount);
            ClassicAssert.AreEqual(totalSalesCount, notReferencedSalesCount + referencedSalesCount);
            ClassicAssert.IsTrue(totalSalesCount > 20);

            saleCollection1 = saleAccessor.Read<EntityCollection<Sale>>(null, saleOrder, null, null);
            saleCollection2 = saleAccessor.Read<EntityCollection<Sale>>(null, saleOrder, 1, 10);
            ClassicAssert.AreEqual(10, saleCollection2.Count);
            ClassicAssert.AreEqual(saleCollection1[1].ID, saleCollection2[0].ID);

            salesFilter.Reset();
            salesFilter.GoodName = good1.Name;

            saleCollection1 = saleAccessor.Read<EntityCollection<Sale>>(salesFilter, saleOrder, null, null);
            ClassicAssert.AreNotEqual(0, saleCollection1);
            Sale prevSale = null;
            foreach (Sale sale in saleCollection1)
            {
                ClassicAssert.AreEqual(salesFilter.GoodName, sale.Good.Name);
                if (prevSale != null)
                {
                    int r1 = string.Compare(prevSale.SalesPerson.Name, sale.SalesPerson.Name);
                    if (r1 == 0)
                    {
                        if (prevSale.SalesDate == sale.SalesDate)
                        {
                            ClassicAssert.IsTrue(prevSale.ID < sale.ID);
                        }
                        else if (prevSale.SalesDate < sale.SalesDate)
                        {
                            ClassicAssert.Fail();
                        }
                    }
                    else if (r1 > 0)
                    {
                        ClassicAssert.Fail();
                    }
                }
                prevSale = sale;
            }
            ClassicAssert.NotNull(prevSale);

            prevSale = null;
            int cc = 0;
            while (true)
            {
                prevSale = saleAccessor.NextEntity(prevSale, saleOrder, salesFilter);
                if (prevSale == null)
                    break;
                ClassicAssert.AreEqual(saleCollection1[cc++].ID, prevSale.ID);
            }
            ClassicAssert.AreEqual(cc, saleCollection1.Count);

            prevSale = null;
            cc = saleCollection1.Count;
            while (true)
            {
                prevSale = saleAccessor.NextEntity(prevSale, saleOrder, salesFilter, true);
                if (prevSale == null)
                    break;
                ClassicAssert.AreEqual(saleCollection1[--cc].ID, prevSale.ID);
            }
            ClassicAssert.AreEqual(0, cc);

            Category cat3 = new Category() { ID = 500, Name = "newcat" };
            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Category>())
                query.Execute(cat3);

            GoodUpdateRecord goodUpdate = new GoodUpdateRecord() { Category = cat3 };
            goodFilter.Reset();
            goodFilter.Category = cat2;

            cc = goodEntityAccessor.Count(goodFilter);
            ClassicAssert.AreEqual(cc, goodEntityAccessor.UpdateMultiple(goodFilter, goodUpdate));

            EntityCollection<Good> goodCollection = goodEntityAccessor.Read<EntityCollection<Good>>(null, null, null, null);
            int cc1 = 0;
            foreach (Good g in goodCollection)
            {
                ClassicAssert.AreNotEqual(g.Category.ID, cat2.ID);
                if (g.Category.ID == cat3.ID)
                    cc1++;
            }

            ClassicAssert.AreEqual(cc1, cc);

            #endregion Test generic entity reader

            #region Test autoincrement flags for insert

            int lastID = 0;
            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Employee>())
            {
                Employee emp = new Employee() { ID = 0, Name = "dummy1" };
                query.Execute(emp);
                ClassicAssert.AreNotEqual(0, emp.ID);

                emp = new Employee() { ID = 10000, Name = "dummy2" };
                query.Execute(emp);
                ClassicAssert.AreNotEqual(10000, emp.ID);
                lastID = emp.ID;
            }

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Employee>(true))
            {
                Employee emp = new Employee() { ID = 0, Name = "dummy3" };
                query.Execute(emp);
                ClassicAssert.AreEqual(0, emp.ID);

                emp = new Employee() { ID = 10000, Name = "dummy4" };
                query.Execute(emp);
                ClassicAssert.AreEqual(10000, emp.ID);

                emp = new Employee() { ID = lastID + (connection.ConnectionType == "mysql" ? 2 : 1), Name = "dummy5" };
                query.Execute(emp);
                ClassicAssert.AreEqual(lastID + (connection.ConnectionType == "mysql" ? 2 : 1), emp.ID);
            }

            if (connection.ConnectionType == "oracle")
                ((OracleDbConnection)connection).UpdateSequence(typeof(Employee));

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Employee>())
            {
                Employee emp = new Employee() { ID = 0, Name = "dummy6" };
                query.Execute(emp);
                ClassicAssert.AreNotEqual(0, emp.ID);
            }

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<Employee>())
            {
                query.Where.Expression<Employee>(o => SqlFunction.Like(o.Name, "dummy%"));
                ClassicAssert.AreEqual(6, query.RowCount);
            }

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<Employee>())
            {
                query.Where.Expression<Employee>(o => SqlFunction.Like(SqlFunction.Upper(o.Name), SqlFunction.Upper("dummy%")));
                ClassicAssert.AreEqual(6, query.RowCount);
            }

            #endregion Test autoincrement flags for insert

            #region Test Reader

            using (var query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddEntity(typeof(Employee), nameof(Sale.SalesPerson));
                query.AddEntity(typeof(Good));
                query.AddToResultset(nameof(Sale.ID), nameof(CustomSaleTargetClass.ID));
                query.AddToResultset(nameof(Sale.SalesDate), nameof(CustomSaleTargetClass.SalesDate));
                query.AddToResultset(typeof(Employee), nameof(Employee.Name), nameof(CustomSaleTargetClass.SalesPersonName));
                query.AddToResultset(typeof(Good), nameof(Good.Name), nameof(CustomSaleTargetClass.GoodName));
                query.AddToResultset(nameof(Sale.Total), nameof(CustomSaleTargetClass.Total));

                SelectEntityQueryReader<CustomSaleTargetClass> reader = new SelectEntityQueryReader<CustomSaleTargetClass>(query);
                reader.AddAction<CustomSaleTargetClass>((o, q) => q.GetValue<double>(nameof(CustomSaleTargetClass.Total)) * 0.3, nameof(CustomSaleTargetClass.AdjustedTotal));
                reader.AddAction<CustomSaleTargetClass>((m, q) =>
                {
                    DateTime saleDate = q.GetValue<DateTime>(nameof(CustomSaleTargetClass.SalesDate));
                    m.SaleDay = saleDate.Day;
                    m.SaleMonth = saleDate.Month;
                    m.SaleYear = saleDate.Year;
                    m.SaleDayOfWeek = saleDate.DayOfWeek;
                });

                query.Execute();
                List<CustomSaleTargetClass> list = reader.ReadAll<List<CustomSaleTargetClass>>();
                list.Should().NotBeEmpty();
            }

            #endregion Test Reader
        }

        private static void TestGoodAccessorRead(GenericEntityAccessor<Good, int> goodEntityAccessor, GoodFilter filter)
        {
            EntityCollection<Good> rs = goodEntityAccessor.Read<EntityCollection<Good>>(filter, goodOrder, null, null);
            ClassicAssert.IsNotNull(rs);
            ClassicAssert.AreNotEqual(0, rs.Count);
            for (int i = 1; i < rs.Count; i++)
                ClassicAssert.IsTrue(string.Compare(rs[i - 1].Name, rs[i].Name) < 0);
            int cc = rs.Count;

            rs = goodEntityAccessor.Read<EntityCollection<Good>>(filter, goodOrderRev, null, null);
            ClassicAssert.IsNotNull(rs);
            ClassicAssert.AreEqual(cc, rs.Count);
            for (int i = 1; i < rs.Count; i++)
                ClassicAssert.IsTrue(string.Compare(rs[i - 1].Name, rs[i].Name) > 0);

            rs = goodEntityAccessor.Read<EntityCollection<Good>>(filter, goodOrder, null, null);
            Good current = null;
            cc = 0;

            while (true)
            {
                int nextID = goodEntityAccessor.NextKey(current, goodOrder, filter, false);
                current = goodEntityAccessor.NextEntity(current, goodOrder, filter, false);
                if (current == null)
                {
                    ClassicAssert.AreEqual(0, nextID);
                    break;
                }
                ClassicAssert.AreEqual(nextID, current.ID);
                ClassicAssert.AreEqual(rs[cc++].ID, current.ID);
            }
            current = null;
            cc = rs.Count;
            while (true)
            {
                current = goodEntityAccessor.NextEntity(current, goodOrder, filter, true);
                if (current == null)
                    break;
                ClassicAssert.AreEqual(rs[--cc].ID, current.ID);
            }
        }

        private static void TestLinqAccessor(SqlDbConnection connection)
        {
            double v;
            EntityCollection<Sale> sales;

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.AddOrderBy<Sale>(sale => sale.SalesDate);
                query.AddOrderBy<Good>(good => good.Name);
                sales = query.ReadAll<Sale>();
            }

            Sale prevSale = null;
            foreach (Sale sale in sales)
            {
                if (prevSale != null)
                {
                    ClassicAssert.IsTrue(sale.SalesDate > prevSale.SalesDate || (sale.SalesDate == prevSale.SalesDate && string.Compare(sale.Good.Name, prevSale.Good.Name) > 0));
                }

                prevSale = sale;
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddWholeTree();
                query.AddToResultset<Sale, DateTime>(sale => sale.SalesDate, "saledate");
                query.AddToResultset<Sale, double>(sale => SqlFunction.Sum(sale.Total), "total");
                query.Where.Expression<Sale>(sale => !SqlFunction.Like(sale.Good.Name, "z%") || sale.Good.Category.ID == Math.Abs(50 + 50) || sale.ID == 10);
                query.AddGroupBy<Sale>(sale => sale.SalesDate);
                query.AddOrderBy<Sale>(sale => sale.SalesDate);
                dynamic res = query.ReadAllDynamic();
                foreach (var r in res)
                {
                    ClassicAssert.AreEqual(sales.Where(sale => sale.SalesDate == r.saledate).Sum(sale => sale.Total), r.total, 0.000001);
                }
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddToResultset<Sale, int>(_ => SqlFunction.Count(), "count");
                query.AddToResultset<Sale, double>(sale => SqlFunction.Sum(sale.Total), "sum");
                query.AddToResultset<Sale, double>(sale => SqlFunction.Min(sale.Total), "min");
                query.AddToResultset<Sale, double>(sale => SqlFunction.Max(sale.Total), "max");
                query.AddToResultset<Sale, double>(sale => SqlFunction.Abs((SqlFunction.Max(sale.Total) - SqlFunction.Min(sale.Total)) / SqlFunction.Avg(sale.Total)), "fn");
                query.AddToResultset<Sale, double>(sale => SqlFunction.Avg(sale.Total), "avg");

                dynamic r = query.ReadOneDynamic();

                ClassicAssert.AreEqual(sales.Count, (double)r.count);
                double min, max, sum, avg, fn;
                min = sales.Min(o => o.Total);
                max = sales.Max(o => o.Total);
                sum = sales.Sum(o => o.Total);
                avg = sales.Average(o => o.Total);

                min.Should().Be(r.min);
                max.Should().Be(r.max);
                sum.Should().Be(r.sum);
                avg.Should().Be(r.avg);
                fn = (max - min) / avg;

                fn.Should().BeApproximately((double)r.fn, 1e-5);

                v = sales.Average(o => o.Total);
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddWholeTree();
                query.AddToResultset<Sale, int>(sale => sale.ID, "id");
                query.AddToResultset<Sale, double>(sale => sale.Total, "total");
                query.AddToResultset<Sale, DateTime>(sale => sale.SalesDate, "saledate");
                query.AddToResultset<Sale, string>(sale => sale.Good.Name, "good");
                if (connection.ConnectionType != "oracle" && connection.ConnectionType != "mssql")
                    query.AddToResultset<Sale, bool>(sale => sale.Good.Name == sale.Good.Category.Name, "booleanFlag");
                query.AddOrderBy<Good>(good => good.Name);
                dynamic result = query.ReadAllDynamic();
                (result as object)?.Should().NotBeNull();
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.Total > v && sale.Good.Category.ID == 100);
                dynamic res = query.ReadAllDynamic();
                foreach (var sale in res)
                {
                    ClassicAssert.IsTrue(sale.Total > v && sale.Good.Category.ID == 100);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                using (SelectEntitiesQueryBase query1 = connection.GetSelectEntitiesQueryBase<Good>())
                {
                    query1.AddWholeTree();
                    query1.AddToResultset<Good, int>(good => good.ID);
                    const int category = 100;
                    query1.Where.Expression<Good>(good => good.Category.ID == category);
                    query.Where.Expression<Sale>(sale => SqlFunction.In(sale.Good, query1));
                    query.AddOrderBy<Sale>(sale => sale.SalesDate);
                    query.Execute();
                    dynamic res = query.ReadAllDynamic();
                    bool atLeastOne = false;
                    foreach (var sale in res)
                    {
                        atLeastOne = true;
                        ClassicAssert.AreEqual(100, sale.Good.Category.ID);
                    }

                    ClassicAssert.IsTrue(atLeastOne);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                using (SelectEntitiesQueryBase query1 = connection.GetSelectEntitiesQueryBase<Good>())
                {
                    query1.AddWholeTree();
                    query1.AddToResultset<Good, int>(good => good.ID);
                    const int category = 100;
                    query1.Where.Expression<Good>(good => good.Category.ID == category);
                    query.Where.Expression<Sale>(sale => SqlFunction.NotIn(sale.Good, query1));
                    query.AddOrderBy<Sale>(sale => sale.SalesDate);
                    query.Execute();
                    dynamic res = query.ReadAllDynamic();
                    bool atLeastOne = false;
                    foreach (var sale in res)
                    {
                        atLeastOne = true;
                        ClassicAssert.AreNotEqual(100, sale.Good.Category.ID);
                    }

                    ClassicAssert.IsTrue(atLeastOne);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                using (SelectEntitiesQueryBase query1 = connection.GetSelectEntitiesQueryBase<Good>())
                {
                    query1.AddWholeTree();
                    query1.AddToResultset<Good, int>(good => good.ID);
                    query1.Where.Expression<Good>(good => good.ID == SqlFunction.Value<int>(query.GetReference(nameof(Sale.Good))));
                    query.Where.Expression<Sale>(_ => SqlFunction.Exists(query1));
                    query.AddOrderBy<Sale>(sale => sale.SalesDate);
                    query.Execute();
                    dynamic res = query.ReadAllDynamic();
                    var atLeastOne = (res as IEnumerable)?.GetEnumerator().MoveNext();
                    ClassicAssert.IsTrue(atLeastOne);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                using (SelectEntitiesQueryBase query1 = connection.GetSelectEntitiesQueryBase<Good>())
                {
                    query1.AddWholeTree();
                    query1.AddToResultset<Good, int>(good => good.ID);
                    query1.Where.Expression<Good>(good => good.ID == SqlFunction.Value<int>(query.GetReference(nameof(Sale.Good))));

                    query.Where.Expression<Sale>(_ => SqlFunction.NotExists(query1));
                    query.AddOrderBy<Sale>(sale => sale.SalesDate);
                    query.Execute();
                    dynamic res = query.ReadAllDynamic();
                    var atLeastOne = (res as IEnumerable)?.GetEnumerator().MoveNext();
                    ClassicAssert.IsFalse(atLeastOne);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
            {
                query.Where.Expression<Employee>(emp => emp.EmpoyeeType1 == EmpoyeeType.Salesman);
                EntityCollection<Employee> res = query.ReadAll<Employee>();
                ClassicAssert.AreNotEqual(res.Count, 0);
                foreach (Employee employee in res)
                    ClassicAssert.AreEqual(EmpoyeeType.Salesman, employee.EmpoyeeType1);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
            {
                query.Where.Property(nameof(Employee.EmpoyeeType1)).Neq(EmpoyeeType.Salesman);
                EntityCollection<Employee> res = query.ReadAll<Employee>();
                ClassicAssert.AreNotEqual(res.Count, 0);
                foreach (Employee employee in res)
                    ClassicAssert.AreNotEqual(EmpoyeeType.Salesman, employee.EmpoyeeType1);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ReferencePerson == null);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.IsNull(sale.ReferencePerson);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ReferencePerson != null);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.IsNotNull(sale.ReferencePerson);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID == 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.AreEqual(50, sale.ID);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID != 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.AreNotEqual(50, sale.ID);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID > 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.IsTrue(sale.ID > 50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID >= 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.IsTrue(sale.ID >= 50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID < 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.IsTrue(sale.ID < 50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID <= 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    ClassicAssert.IsTrue(sale.ID <= 50);
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Category>())
            {
                query.AddEntity(typeof(Good), nameof(Category.ID));
                query.AddEntity(typeof(Sale), $"{nameof(Good)}.{nameof(Good.ID)}");
                query.AddToResultset<Category, int>(cat => cat.ID, "Id");
                query.AddToResultset<Category, string>(cat => cat.Name, "Name");
                query.AddToResultset<Sale, double>(sale => SqlFunction.Sum(sale.Total), "SalesTotal");
                query.AddGroupBy<Category>(cat => cat.ID);
                query.AddOrderBy<Category>(cat => cat.Name);
                dynamic res = query.ReadAllDynamic();
                foreach (var r in res)
                    ClassicAssert.AreEqual(sales.Where(sale => sale.Good.Category.ID == r.Id).Sum(sale => sale.Total), r.SalesTotal, 0.00000001);
            }

            Category cat2;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Category>())
            {
                query.AddOrderBy<Category>(c => c.ID);
                query.Execute();
                cat2 = query.ReadAll<Category>()[1];
            }

            EntityCollection<Good> catGoods;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Good>())
            {
                query.Where.Expression<Category>(c => c.ID == cat2.ID);
                catGoods = query.ReadAll<Good>();
            }

            ClassicAssert.AreNotEqual(0, catGoods.Count);
            ClassicAssert.IsTrue(catGoods.Count > 2);

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Category>())
            {
                query.Where.Expression<Category>(c => c.ID == cat2.ID);

                query.FindType(typeof(Category));
                query.AddEntity(typeof(Good), TableJoinType.None);
                query.AddEntity(typeof(Good), TableJoinType.None);

                query.Where.Expression<Category, Good[]>((c, g) => c == g[0].Category && g[0].ID == catGoods[0].ID);
                query.Where.Expression<Category, Good[]>((c, g) => c == g[1].Category && g[1].ID == catGoods[1].ID);

                query.AddToResultset<Category, int>(c => c.ID, "ID");
                query.AddToResultset<Category, string>(c => c.Name, "Name");
                query.AddToResultset<Good[], string>(g => g[0].Name, "Good1");
                query.AddToResultset<Good[], string>(g => g[1].Name, "Good2");
                query.AddOrderBy<Good[]>(g => g[1].Name);

                query.Execute();
                IList<dynamic> res = query.ReadAllDynamic();
                ClassicAssert.AreEqual(1, res.Count);
                dynamic r0 = res[0];
                ClassicAssert.AreEqual(cat2.ID, r0.ID);
                ClassicAssert.AreEqual(cat2.Name, r0.Name);
                ClassicAssert.AreEqual(catGoods[0].Name, r0.Good1);
                ClassicAssert.AreEqual(catGoods[1].Name, r0.Good2);
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Category>())
            {
                query.Where.Expression<Category>(c => c.ID == cat2.ID);

                query.FindType(typeof(Category));
                query.AddEntity<Category, Good[]>(typeof(Good), TableJoinType.Left, (c, g) => c == g[0].Category && g[0].ID == catGoods[0].ID);
                query.AddEntity<Category, Good[]>(typeof(Good), TableJoinType.Left, (c, g) => c == g[1].Category && g[1].ID == catGoods[1].ID);

                query.AddToResultset<Category, int>(c => c.ID, "ID");
                query.AddToResultset<Category, string>(c => c.Name, "Name");
                query.AddToResultset<Good[], string>(g => g[0].Name, "Good1");
                query.AddToResultset<Good[], string>(g => g[1].Name, "Good2");
                query.AddOrderBy<Good[]>(g => g[1].Name);

                query.Execute();
                IList<dynamic> res = query.ReadAllDynamic();
                ClassicAssert.AreEqual(1, res.Count);
                dynamic r0 = res[0];
                ClassicAssert.AreEqual(cat2.ID, r0.ID);
                ClassicAssert.AreEqual(cat2.Name, r0.Name);
                ClassicAssert.AreEqual(catGoods[0].Name, r0.Good1);
                ClassicAssert.AreEqual(catGoods[1].Name, r0.Good2);
            }
        }

        private static void TestLinqQueryable(SqlDbConnection connection)
        {
            QueryableEntityProvider provider = new QueryableEntityProvider(new ExistingConnectionFactory(connection));
            QueryableEntity<Sale> sales = provider.Entities<Sale>();
            Sale[] allSales, tempSales;
            int[] ids;
            DateTime[] dates;

            allSales = sales.OrderBy(sale => sale.SalesDate).ToArray();
            Sale prevSale = null;
            foreach (Sale sale in allSales)
            {
                if (prevSale != null)
                    ClassicAssert.IsTrue(sale.SalesDate >= prevSale.SalesDate);
                prevSale = sale;
            }

            allSales = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).ToArray();
            prevSale = null;
            foreach (Sale sale in allSales)
            {
                if (prevSale != null)
                {
                    ClassicAssert.IsTrue(string.Compare(sale.Good.Name, prevSale.Good.Name) > 0 ||
                        (string.Compare(sale.Good.Name, prevSale.Good.Name) == 0 &&
                         sale.SalesDate > prevSale.SalesDate));
                }

                prevSale = sale;
            }

            tempSales = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).Take(5).Skip(10).ToArray();
            ClassicAssert.AreEqual(5, tempSales.Length);
            for (int i = 0; i < tempSales.Length; i++)
                ClassicAssert.AreEqual(allSales[i + 10].ID, tempSales[i].ID);

            ids = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).Select(sale => sale.ID).ToArray();
            dates = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).Select(sale => sale.SalesDate).ToArray();

            for (int i = 0; i < allSales.Length; i++)
            {
                ClassicAssert.AreEqual(allSales[i].ID, ids[i]);
                ClassicAssert.AreEqual(allSales[i].SalesDate, dates[i]);
            }

            foreach (var v in sales.GroupBy(sale => sale.Good.ID).Select(r => new { Good = r.Key, Total = r.Sum(o => o.Total), Avg = r.Average(o => o.Total), LastTransaction = r.Max(o => o.SalesDate) }))
            {
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good).Sum(o => o.Total), v.Total, 1e-5);
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good).Average(o => o.Total), v.Avg, 1e-5);
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good).Max(o => o.SalesDate), v.LastTransaction);
            }

            foreach (var v in sales.GroupBy(sale => new { Good = sale.Good.ID, Person = sale.SalesPerson.ID }).Select(r => new { r.Key.Good, r.Key.Person, CountOf = r.Count(), FirstTransaction = r.Min(o => o.SalesDate) }))
            {
#pragma warning disable S2971 // "IEnumerable" LINQs should be simplified
#pragma warning disable RCS1077 // Optimize LINQ method call.
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good && o.SalesPerson.ID == v.Person).Count(), v.CountOf);
#pragma warning restore RCS1077 // Optimize LINQ method call.
#pragma warning restore S2971 // "IEnumerable" LINQs should be simplified
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good && o.SalesPerson.ID == v.Person).Min(o => o.SalesDate), v.FirstTransaction);
            }

            foreach (var v in sales.Where(sale => Math.Abs(sale.Total) > 60))
            {
                ClassicAssert.IsTrue(v.Total > 60);
            }

            foreach (var v in sales.Where(sale => sale.Good.Name.StartsWith('T') && sale.Total > 60).Select(r => new { Id = r.Good.ID, r.Good.Name, r.Total }))
            {
                ClassicAssert.IsTrue(v.Total > 60);
                ClassicAssert.IsTrue(v.Name.StartsWith('T'));
            }
        }

        private static void TestLinqQueryable1(SqlDbConnection connection)
        {
            QueryableEntityProvider provider = new QueryableEntityProvider(new ExistingConnectionFactory(connection));
            QueryableEntity<Sale> sales = provider.Entities<Sale>();
            Sale[] allSales = (from s in sales orderby s.SalesDate select s).ToArray();
            Sale prevSale = null;
            foreach (Sale sale in allSales)
            {
                if (prevSale != null)
                    ClassicAssert.IsTrue(sale.SalesDate >= prevSale.SalesDate);
                prevSale = sale;
            }

            allSales = (from s in sales orderby new { s.Good.Name, s.SalesDate } select s).ToArray();
            prevSale = null;
            foreach (Sale sale in allSales)
            {
                if (prevSale != null)
                {
                    ClassicAssert.IsTrue(string.Compare(sale.Good.Name, prevSale.Good.Name) > 0 ||
                        (string.Compare(sale.Good.Name, prevSale.Good.Name) == 0 &&
                         sale.SalesDate > prevSale.SalesDate));
                }

                prevSale = sale;
            }

            var query = from s in sales group s by s.Good.ID into g select new { Good = g.Key, Count = g.Count(), Total = g.Sum(o => o.Total), Avg = g.Average(o => o.Total), LastTransaction = g.Max(o => o.SalesDate) };
            foreach (var v in query)
            {
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good).Sum(o => o.Total), v.Total, 1e-5);
#pragma warning disable S2971 // "IEnumerable" LINQs should be simplified
#pragma warning disable RCS1077 // Optimize LINQ method call.
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good).Count(), v.Count, 1e-5);
#pragma warning restore RCS1077 // Optimize LINQ method call.
#pragma warning restore S2971 // "IEnumerable" LINQs should be simplified
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good).Average(o => o.Total), v.Avg, 1e-5);
                ClassicAssert.AreEqual(allSales.Where(o => o.Good.ID == v.Good).Max(o => o.SalesDate), v.LastTransaction);
            }
        }

        #endregion Static entity

        #region Dynamic Entity

        public class DynamicDictionary : DynamicEntity
        {
            private static readonly DynamicEntityProperty[] mFields = new DynamicEntityProperty[]
            {
                new DynamicEntityProperty() {Name = "ID", PropertyType = typeof(int), EntityPropertyAttribute = new EntityPropertyAttribute() {AutoId = true}},
                new DynamicEntityProperty() {Name = "Name", PropertyType = typeof(string), EntityPropertyAttribute = new EntityPropertyAttribute() {Field = "name", Size = 32, Sorted = true}},
            };

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties() => mFields;

            private static readonly EntityAttribute mEntityAttribute = new EntityAttribute() { Table = "dyndict" };

            public override EntityAttribute EntityAttribute => mEntityAttribute;
        }

        public static void TestDynamicEntity(SqlDbConnection connection)
        {
            using (EntityQuery drop = connection.GetDropEntityQuery<DynamicDictionary>())
                drop.Execute();

            using (EntityQuery create = connection.GetCreateEntityQuery<DynamicDictionary>())
                create.Execute();

            List<dynamic> records = new List<dynamic>();

            for (int i = 0; i < 10; i++)
            {
                records.Add(new DynamicDictionary());
                records[i].Name = $"dictrecord{i}";
                using (ModifyEntityQuery query = connection.GetInsertEntityQuery<DynamicDictionary>())
                    query.Execute(records[i]);
                ClassicAssert.IsTrue(records[i].ID > 0);
            }

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<DynamicDictionary>())
                ClassicAssert.AreEqual(10, query.RowCount);

            dynamic r5 = null;

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.AddOrderBy("Name");
                ICollection<dynamic> res = query.ReadAllDynamic();
                ClassicAssert.AreEqual(10, res.Count);
                string lastName = null;
                foreach (dynamic r in res)
                {
                    ClassicAssert.IsTrue(r.ID > 0);
                    if (r.ID == 5)
                    {
                        r5 = r;
                    }
                    if (lastName != null)
                        ClassicAssert.IsTrue(string.CompareOrdinal(lastName, r.Name) <= 0);
                    lastName = r.Name;
                }
            }

            ClassicAssert.IsNotNull(r5);
            using (ModifyEntityQuery query = connection.GetDeleteEntityQuery<DynamicDictionary>())
                query.Execute(r5);

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<DynamicDictionary>())
                ClassicAssert.AreEqual(9, query.RowCount);

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.Where.Property("ID").Eq((int)r5.ID);
                query.Execute();
                ClassicAssert.IsNull(query.ReadOneDynamic());
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.Where.Property("ID").Eq().Value((int)(r5.ID + 1));
                query.Execute();
                ClassicAssert.IsNotNull(query.ReadOneDynamic());
            }

            #region test dynamic query building

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.Where.Property("ID").Eq().Value((int)(r5.ID + 1));
                query.Execute();
                ClassicAssert.IsNotNull(query.ReadOneDynamic());
            }

            #endregion test dynamic query building
        }

        #endregion Dynamic Entity
    }
}