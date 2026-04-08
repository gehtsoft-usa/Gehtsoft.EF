using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using AwesomeAssertions;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Legacy.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class EntityTests : IClassFixture<EntityTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public EntityTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        private static readonly GenericEntitySortOrder[] goodOrder = new GenericEntitySortOrder[] { new GenericEntitySortOrder(nameof(Good.Name)) };
        private static readonly GenericEntitySortOrder[] goodOrderRev = new GenericEntitySortOrder[] { new GenericEntitySortOrder(nameof(Good.Name), SortDir.Desc) };
        private static readonly GenericEntitySortOrder[] saleOrder = new GenericEntitySortOrder[]
        {
            new GenericEntitySortOrder($"{nameof(Sale.SalesPerson)}.{nameof(Employee.Name)}"),
            new GenericEntitySortOrder(nameof(Sale.SalesDate), SortDir.Desc),
            new GenericEntitySortOrder(nameof(Sale.ID))
        };

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

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql")]
        public void TestEntities(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            TestEntitiesImpl(connection, true);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "+mysql")]
        public void TestEntitiesNoHierarchical(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            TestEntitiesImpl(connection, false);
        }

        private void TestEntitiesImpl(SqlDbConnection connection, bool testHierarchical)
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
                t.DVal.Should().Be("1234");
                t.StringVal.Should().Be("default");
                t.BoolVal.Should().BeTrue();
                t.IntVal.Should().Be(123);
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

            callbacks1.Count.Should().Be(serializationCallbacks.Length);
            for (int i = 0; i < serializationCallbacks.Length; i++)
            {
                callbacks1[i].StringValue.Should().Be(serializationCallbacks[i].StringValue);
            }

            #endregion test callback

            #region create tables and data and read using regular queries

            TableDescriptor td = AllEntities.Inst[typeof(Employee)].TableDescriptor;
            td["ID"].Should().NotBeNull();
            td["ID"].DbType.Should().Be(DbType.Int32);
            td["ID"].Name.Should().Be("id");
            td["ID"].PrimaryKey.Should().BeTrue();
            td["ID"].Autoincrement.Should().BeTrue();
            td["ID"].Nullable.Should().BeFalse();

            td["LastCheck"].Should().NotBeNull();
            td["LastCheck"].DbType.Should().Be(DbType.DateTime);
            td["LastCheck"].Name.Should().Be("lastcheck");
            td["LastCheck"].PrimaryKey.Should().BeFalse();
            td["LastCheck"].Autoincrement.Should().BeFalse();
            td["LastCheck"].Nullable.Should().BeTrue();

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
                boss.ID.Should().BeGreaterThan(0);
                query.Execute(mgr1);
                query.Execute(mgr2);
                query.Execute(sm1);
                query.Execute(sm2);
                query.Execute(sm3);
                query.Execute(sm4);
                query.Execute(sm5);
            }

            connection.CanDelete<Employee>(mgr2).Should().BeFalse();
            connection.CanDelete<Employee>(sm5).Should().BeTrue();

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
                query.RowCount.Should().Be(7);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Employee)))
            {
                query.AddOrderBy(nameof(Employee.ID));
                query.Execute();
                EntityCollection<Employee> coll = query.ReadAll<EntityCollection<Employee>, Employee>();
                coll.Count.Should().Be(7);

                coll[0].Name.Should().Be(boss.Name);
                coll[0].EmpoyeeType1.Should().Be(EmpoyeeType.Manager);
                coll[0].LastCheck.Should().BeNull();

                coll[0].Manager.Should().BeNull();
                coll[1].Name.Should().Be(mgr1.Name);
                coll[1].Manager.Should().NotBeNull();

                coll[1].Manager.ID.Should().Be(boss.ID);
                coll[1].EmpoyeeType1.Should().Be(EmpoyeeType.Salesman);

                coll[4].Name.Should().Be("Salesman2");
                coll[4].LastCheck.Should().NotBeNull();
                coll[4].LastCheck.Should().Be(new DateTime(2015, 1, 2, 0, 0, 0, DateTimeKind.Unspecified));
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
                rs.Count.Should().Be(1);
                rs[0].ID.Should().Be(allEmployees[3].ID);
            }

            if (testHierarchical)
            {
                foreach (var e in allEmployees) e.Found = false;

                using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
                {
                    using (SelectEntitiesTreeQuery query1 = connection.GetSelectEntitiesTreeQuery<Employee>(false))
                    {
                        query1.AddToResultset(nameof(Employee.ID));
                        query.Where.Property(nameof(Employee.ID)).In(query1);
                        query.AddOrderBy($"{nameof(Employee.Manager)}.{nameof(Employee.ID)}");
                    }

                    EntityCollection<Employee> emps = query.ReadAll<Employee>();
                    emps.Count.Should().Be(allEmployees.Count);
                    foreach (Employee e1 in emps)
                    {
                        int idx = allEmployees.Find(e1);
                        idx.Should().NotBe(-1);
                        allEmployees[idx].Found = true;
                    }
                }

                allEmployees.All(e => e.Found).Should().BeTrue();

                foreach (var e in allEmployees) e.Found = false;

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
                    emps.Count.Should().Be(allEmployees.Count(e => e.Manager?.ID == mgr1.ID || e.ID == mgr1.ID));
                    foreach (Employee e1 in emps)
                    {
                        int idx = allEmployees.Find(e1);
                        idx.Should().NotBe(-1);
                        allEmployees[idx].Found = true;
                    }
                }

                allEmployees.Where(e => e.Manager?.ID == mgr1.ID || e.ID == mgr1.ID).All(e => e.Found).Should().BeTrue();
            }

            Category cat1 = new Category(100, "Food");
            Category cat2 = new Category(200, "Clothes");

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Category)))
            {
                query.Execute(cat1);
                cat1.ID.Should().Be(100);
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
                query.RowCount.Should().Be(5);
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
                query.GetValue<int>(0).Should().Be(5);
            }

            using (var query = connection.GetGenericSelectEntityQuery(typeof(Good)))
            {
                query.AddToResultset(AggFn.Count, nameof(Good.Category));
                query.Execute();
                query.ReadNext();
                query.GetValue<int>(0).Should().Be(2);
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
            good1.Category.Count.Should().NotBe(0);
            connection.CanDelete<Good>(good1).Should().BeFalse();
            connection.CanDelete<Employee>(sm4).Should().BeFalse();

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

                coll.Count.Should().Be(2);
                foreach (Category cat in coll)
                {
                    cat.Count1.Should().Be(cat.Count);
                    cat.Total1.Should().Be(cat.Total);
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Sale)))
            {
                query.AddOrderBy(nameof(Sale.ID));
                query.Skip = 10;
                query.Limit = 15;
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                coll.Count.Should().Be(15);
                coll[0].ID.Should().Be(sales[10].ID);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Sale)))
            {
                query.Where.Property($"{nameof(Sale.Good)}.{nameof(Good.ID)}").Eq(good2);
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                coll.Count.Should().NotBe(0);
                foreach (Sale sale in coll)
                    sale.Good.ID.Should().Be(good2.ID);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Sale)))
            {
                string param = query.Where.Property($"{nameof(Sale.Good)}.{nameof(Good.ID)}").Neq(good2.ID).ParameterName;
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                coll.Count.Should().NotBe(0);
                foreach (Sale sale in coll)
                    sale.Good.ID.Should().NotBe(good2.ID);

                query.BindParam(param, good3);
                query.Execute();
                coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                coll.Count.Should().NotBe(0);
                foreach (Sale sale in coll)
                    sale.Good.ID.Should().NotBe(good3.ID);
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
                    s.Checked.Should().BeFalse();
                    query.GetValue<double>("sale").Should().Be(s.Total);
                    s.Checked = true;
                }
            }

            foreach (SumOfSales s in ss)
            {
                s.Checked.Should().BeTrue();
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
                    s.ID.Should().Be((int)one.Id);
                    ((double)one.Sale).Should().Be(s.Total);
                    s.Checked = true;
                }
            }

            foreach (SumOfSales s in ss)
                s.Checked.Should().BeTrue();

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
                all.Count.Should().Be(5);
                foreach (var one in all)
                {
                    SumOfSales s = ss[one.ID];
                    s.Should().NotBeNull();
                    ((double)one.Total).Should().Be(s.Total);
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
                    sale.SalesDate.Should().NotBe(new DateTime(0, DateTimeKind.Unspecified));
                    sale.SalesPerson.Should().BeNull();
                    sale.Total.Should().Be(0);
                    sale.Good.Should().NotBeNull();
                    sale.Good.Category.Should().BeNull();
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                object[] goodids = new object[2] { good1.ID, good3.ID };
                query.Where.PropertyOf<Good>(nameof(Good.ID)).In().Values(goodids);
                query.Execute();
                EntityCollection<Sale> coll = query.ReadAll<EntityCollection<Sale>, Sale>();
                coll.Count.Should().BeGreaterThan(0);
                foreach (Sale sale in coll)
                    (sale.Good.ID == good1.ID || sale.Good.ID == good3.ID).Should().BeTrue();
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
                        ((double)row.Total).Should().Be(0);
                    }
                    else
                    {
                        SumOfSales one = ss[row.Id];
                        ((double)row.Total).Should().Be(one.Total);
                    }
                }
                emptyGoodFound.Should().BeTrue();
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
                result.Count.Should().Be(1);
                bool emptyGoodFound = false;
                foreach (var row in result)
                {
                    if (row.Id == emptyGood.ID)
                    {
                        emptyGoodFound = true;
                        ((double)row.Total).Should().Be(0);
                    }
                }
                emptyGoodFound.Should().BeTrue();
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

            averageSale.Should().BeGreaterThan(0);

            using (var query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Property(nameof(Sale.Total)).Ge(averageSale);
                var result = query.ReadAll<Sale>();
                result.Should().NotBeNull();
                result.Count.Should().NotBe(0);
                foreach (var sale in result)
                    sale.Total.Should().BeGreaterThanOrEqualTo(averageSale);
            }

            using (var query = connection.GetSelectEntitiesQuery<Sale>())
            {
                using (var subquery = connection.GetGenericSelectEntityQuery<Sale>())
                {
                    subquery.AddToResultset(AggFn.Avg, nameof(Sale.Total));
                    query.Where.Property(nameof(Sale.Total)).Ge().Query(subquery);
                }
                var result = query.ReadAll<Sale>();
                result.Should().NotBeNull();
                result.Count.Should().NotBe(0);
                foreach (var sale in result)
                    sale.Total.Should().BeGreaterThanOrEqualTo(averageSale);
            }

            using (var query = connection.GetGenericSelectEntityQuery<Sale>())
            {
                query.AddToResultset(nameof(Sale.Good), "Id");
                query.AddToResultset(AggFn.Avg, nameof(Sale.Total), "Avg");
                query.AddGroupBy(nameof(Sale.Good));
                query.Having.Property(nameof(Sale.Total)).Avg().Ge().Value(averageSale);
                var result = query.ReadAllDynamic();
                result.Should().NotBeNull();
                result.Count.Should().NotBe(0);
                foreach (var g in result)
                    ((double)g.Avg).Should().BeGreaterThanOrEqualTo(averageSale);
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
                result.Should().NotBeNull();
                result.Count.Should().NotBe(0);
                foreach (var g in result)
                    ((double)g.Avg).Should().BeGreaterThanOrEqualTo(averageSale);
            }

            #endregion create tables and data and read using regular queries

            #region test dynamic query building

            EntityCollection<Good> catGoods;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Good>())
            {
                query.Where.Property(nameof(Good.Category)).Eq(cat2);
                catGoods = query.ReadAll<Good>();
            }

            catGoods.Count.Should().NotBe(0);
            catGoods.Count.Should().BeGreaterThan(2);

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
                res.Count.Should().Be(1);
                dynamic r0 = res[0];
                ((int)r0.ID).Should().Be(cat2.ID);
                ((string)r0.Name).Should().Be(cat2.Name);
                ((string)r0.Good1).Should().Be(catGoods[0].Name);
                ((string)r0.Good2).Should().Be(catGoods[1].Name);
            }

            #endregion test dynamic query building

            #region Test generic entity reader

            GenericEntityAccessor<Good, int> goodEntityAccessor = new GenericEntityAccessor<Good, int>(connection);
            GoodFilter goodFilter = new GoodFilter();

            goodEntityAccessor.Count(null).Should().Be(5);
            goodFilter.Reset();
            goodFilter.NameIs = "Bread";
            goodEntityAccessor.Count(goodFilter).Should().Be(1);

            goodFilter.Reset();
            goodFilter.NameStartsWith = "B%";
            goodEntityAccessor.Count(goodFilter).Should().Be(1);

            goodFilter.Reset();
            goodFilter.Category = cat2;
            goodEntityAccessor.Count(goodFilter).Should().Be(3);

            TestGoodAccessorRead(goodEntityAccessor, null);
            TestGoodAccessorRead(goodEntityAccessor, goodFilter);

            Good newGood = new Good() { Category = cat1, Name = "newgood" };
            goodEntityAccessor.Save(newGood);
            goodEntityAccessor.Count(null).Should().Be(6);
            newGood.ID.Should().BeGreaterThanOrEqualTo(1);
            Good good = goodEntityAccessor.Get(newGood.ID);
            good.Should().NotBeNull();
            good.Name.Should().Be(newGood.Name);
            good.Category.ID.Should().Be(newGood.Category.ID);
            newGood.Name = "newgoodnewname";
            goodEntityAccessor.Save(newGood);
            goodEntityAccessor.Count(null).Should().Be(6);
            good.ID.Should().Be(newGood.ID);
            good.Name.Should().NotBe(newGood.Name);
            good = goodEntityAccessor.Get(newGood.ID);
            good.Name.Should().Be(newGood.Name);
            good.Name.Should().Be("newgoodnewname");
            good.Category.ID.Should().Be(newGood.Category.ID);
            goodEntityAccessor.Delete(good);
            goodEntityAccessor.Count(null).Should().Be(5);
            goodEntityAccessor.Get(newGood.ID).Should().BeNull();

            SalesFilter salesFilter = new SalesFilter();
            GenericEntityAccessor<Sale, int> saleAccessor = new GenericEntityAccessor<Sale, int>(connection);
            EntityCollection<Sale> saleCollection1, saleCollection2;

            salesFilter.Reset();
            int totalSalesCount, referencedSalesCount, notReferencedSalesCount;

            totalSalesCount = saleAccessor.Count(null);
            saleAccessor.Count(salesFilter).Should().Be(totalSalesCount);

            salesFilter.HasReferencePerson = true;
            referencedSalesCount = saleAccessor.Count(salesFilter);

            salesFilter.HasReferencePerson = false;
            notReferencedSalesCount = saleAccessor.Count(salesFilter);

            referencedSalesCount.Should().NotBe(0);
            notReferencedSalesCount.Should().NotBe(0);
            (notReferencedSalesCount + referencedSalesCount).Should().Be(totalSalesCount);
            totalSalesCount.Should().BeGreaterThan(20);

            saleCollection1 = saleAccessor.Read<EntityCollection<Sale>>(null, saleOrder, null, null);
            saleCollection2 = saleAccessor.Read<EntityCollection<Sale>>(null, saleOrder, 1, 10);
            saleCollection2.Count.Should().Be(10);
            saleCollection2[0].ID.Should().Be(saleCollection1[1].ID);

            salesFilter.Reset();
            salesFilter.GoodName = good1.Name;

            saleCollection1 = saleAccessor.Read<EntityCollection<Sale>>(salesFilter, saleOrder, null, null);
            saleCollection1.Count.Should().NotBe(0);
            Sale prevSale = null;
            foreach (Sale sale in saleCollection1)
            {
                sale.Good.Name.Should().Be(salesFilter.GoodName);
                if (prevSale != null)
                {
                    int r1 = string.Compare(prevSale.SalesPerson.Name, sale.SalesPerson.Name);
                    if (r1 == 0)
                    {
                        if (prevSale.SalesDate == sale.SalesDate)
                        {
                            prevSale.ID.Should().BeLessThan(sale.ID);
                        }
                        else if (prevSale.SalesDate < sale.SalesDate)
                        {
                            Assert.Fail();
                        }
                    }
                    else if (r1 > 0)
                    {
                        Assert.Fail();
                    }
                }
                prevSale = sale;
            }
            prevSale.Should().NotBeNull();

            prevSale = null;
            int cc = 0;
            while (true)
            {
                prevSale = saleAccessor.NextEntity(prevSale, saleOrder, salesFilter);
                if (prevSale == null)
                    break;
                prevSale.ID.Should().Be(saleCollection1[cc++].ID);
            }
            cc.Should().Be(saleCollection1.Count);

            prevSale = null;
            cc = saleCollection1.Count;
            while (true)
            {
                prevSale = saleAccessor.NextEntity(prevSale, saleOrder, salesFilter, true);
                if (prevSale == null)
                    break;
                prevSale.ID.Should().Be(saleCollection1[--cc].ID);
            }
            cc.Should().Be(0);

            Category cat3 = new Category() { ID = 500, Name = "newcat" };
            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Category>())
                query.Execute(cat3);

            GoodUpdateRecord goodUpdate = new GoodUpdateRecord() { Category = cat3 };
            goodFilter.Reset();
            goodFilter.Category = cat2;

            cc = goodEntityAccessor.Count(goodFilter);
            goodEntityAccessor.UpdateMultiple(goodFilter, goodUpdate).Should().Be(cc);

            EntityCollection<Good> goodCollection = goodEntityAccessor.Read<EntityCollection<Good>>(null, null, null, null);
            int cc1 = 0;
            foreach (Good g in goodCollection)
            {
                g.Category.ID.Should().NotBe(cat2.ID);
                if (g.Category.ID == cat3.ID)
                    cc1++;
            }

            cc1.Should().Be(cc);

            #endregion Test generic entity reader

            #region Test autoincrement flags for insert

            int lastID = 0;
            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Employee>())
            {
                Employee emp = new Employee() { ID = 0, Name = "dummy1" };
                query.Execute(emp);
                emp.ID.Should().NotBe(0);

                emp = new Employee() { ID = 10000, Name = "dummy2" };
                query.Execute(emp);
                emp.ID.Should().NotBe(10000);
                lastID = emp.ID;
            }

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Employee>(true))
            {
                Employee emp = new Employee() { ID = 0, Name = "dummy3" };
                query.Execute(emp);
                emp.ID.Should().Be(0);

                emp = new Employee() { ID = 10000, Name = "dummy4" };
                query.Execute(emp);
                emp.ID.Should().Be(10000);

                emp = new Employee() { ID = lastID + (connection.ConnectionType == "mysql" ? 2 : 1), Name = "dummy5" };
                query.Execute(emp);
                emp.ID.Should().Be(lastID + (connection.ConnectionType == "mysql" ? 2 : 1));
            }

            if (connection.ConnectionType == "oracle")
                ((OracleDbConnection)connection).UpdateSequence(typeof(Employee));

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Employee>())
            {
                Employee emp = new Employee() { ID = 0, Name = "dummy6" };
                query.Execute(emp);
                emp.ID.Should().NotBe(0);
            }

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<Employee>())
            {
                query.Where.Expression<Employee>(o => SqlFunction.Like(o.Name, "dummy%"));
                query.RowCount.Should().Be(6);
            }

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<Employee>())
            {
                query.Where.Expression<Employee>(o => SqlFunction.Like(SqlFunction.Upper(o.Name), SqlFunction.Upper("dummy%")));
                query.RowCount.Should().Be(6);
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
            rs.Should().NotBeNull();
            rs.Count.Should().NotBe(0);
            for (int i = 1; i < rs.Count; i++)
                string.Compare(rs[i - 1].Name, rs[i].Name).Should().BeLessThan(0);
            int cc = rs.Count;

            rs = goodEntityAccessor.Read<EntityCollection<Good>>(filter, goodOrderRev, null, null);
            rs.Should().NotBeNull();
            rs.Count.Should().Be(cc);
            for (int i = 1; i < rs.Count; i++)
                string.Compare(rs[i - 1].Name, rs[i].Name).Should().BeGreaterThan(0);

            rs = goodEntityAccessor.Read<EntityCollection<Good>>(filter, goodOrder, null, null);
            Good current = null;
            cc = 0;

            while (true)
            {
                int nextID = goodEntityAccessor.NextKey(current, goodOrder, filter, false);
                current = goodEntityAccessor.NextEntity(current, goodOrder, filter, false);
                if (current == null)
                {
                    nextID.Should().Be(0);
                    break;
                }
                current.ID.Should().Be(nextID);
                current.ID.Should().Be(rs[cc++].ID);
            }
            current = null;
            cc = rs.Count;
            while (true)
            {
                current = goodEntityAccessor.NextEntity(current, goodOrder, filter, true);
                if (current == null)
                    break;
                current.ID.Should().Be(rs[--cc].ID);
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
                    (sale.SalesDate > prevSale.SalesDate || (sale.SalesDate == prevSale.SalesDate && string.Compare(sale.Good.Name, prevSale.Good.Name) > 0)).Should().BeTrue();
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
                    ((double)r.total).Should().BeApproximately(sales.Where(sale => sale.SalesDate == r.saledate).Sum(sale => sale.Total), 0.000001);
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

                ((double)r.count).Should().Be(sales.Count);
                double min, max, sum, avg, fn;
                min = sales.Min(o => o.Total);
                max = sales.Max(o => o.Total);
                sum = sales.Sum(o => o.Total);
                avg = sales.Average(o => o.Total);

                min.Should().Be((double)r.min);
                max.Should().Be((double)r.max);
                sum.Should().Be((double)r.sum);
                avg.Should().Be((double)r.avg);
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
                    ((bool)(sale.Total > v && sale.Good.Category.ID == 100)).Should().BeTrue();
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
                        ((int)sale.Good.Category.ID).Should().Be(100);
                    }

                    atLeastOne.Should().BeTrue();
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
                        ((int)sale.Good.Category.ID).Should().NotBe(100);
                    }

                    atLeastOne.Should().BeTrue();
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
                    atLeastOne.Should().BeTrue();
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
                    atLeastOne.Should().BeFalse();
                }
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
            {
                query.Where.Expression<Employee>(emp => emp.EmpoyeeType1 == EmpoyeeType.Salesman);
                EntityCollection<Employee> res = query.ReadAll<Employee>();
                res.Count.Should().NotBe(0);
                foreach (Employee employee in res)
                    employee.EmpoyeeType1.Should().Be(EmpoyeeType.Salesman);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Employee>())
            {
                query.Where.Property(nameof(Employee.EmpoyeeType1)).Neq(EmpoyeeType.Salesman);
                EntityCollection<Employee> res = query.ReadAll<Employee>();
                res.Count.Should().NotBe(0);
                foreach (Employee employee in res)
                    employee.EmpoyeeType1.Should().NotBe(EmpoyeeType.Salesman);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ReferencePerson == null);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ReferencePerson.Should().BeNull();
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ReferencePerson != null);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ReferencePerson.Should().NotBeNull();
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID == 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ID.Should().Be(50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID != 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ID.Should().NotBe(50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID > 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ID.Should().BeGreaterThan(50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID >= 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ID.Should().BeGreaterThanOrEqualTo(50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID < 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ID.Should().BeLessThan(50);
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Sale>())
            {
                query.Where.Expression<Sale>(sale => sale.ID <= 50);
                EntityCollection<Sale> res = query.ReadAll<Sale>();
                foreach (Sale sale in res)
                    sale.ID.Should().BeLessThanOrEqualTo(50);
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
                    ((double)r.SalesTotal).Should().BeApproximately(sales.Where(sale => sale.Good.Category.ID == r.Id).Sum(sale => sale.Total), 0.00000001);
            }

            Category linqCat2;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Category>())
            {
                query.AddOrderBy<Category>(c => c.ID);
                query.Execute();
                linqCat2 = query.ReadAll<Category>()[1];
            }

            EntityCollection<Good> linqCatGoods;
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<Good>())
            {
                query.Where.Expression<Category>(c => c.ID == linqCat2.ID);
                linqCatGoods = query.ReadAll<Good>();
            }

            linqCatGoods.Count.Should().NotBe(0);
            linqCatGoods.Count.Should().BeGreaterThan(2);

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Category>())
            {
                query.Where.Expression<Category>(c => c.ID == linqCat2.ID);

                query.FindType(typeof(Category));
                query.AddEntity(typeof(Good), TableJoinType.None);
                query.AddEntity(typeof(Good), TableJoinType.None);

                query.Where.Expression<Category, Good[]>((c, g) => c == g[0].Category && g[0].ID == linqCatGoods[0].ID);
                query.Where.Expression<Category, Good[]>((c, g) => c == g[1].Category && g[1].ID == linqCatGoods[1].ID);

                query.AddToResultset<Category, int>(c => c.ID, "ID");
                query.AddToResultset<Category, string>(c => c.Name, "Name");
                query.AddToResultset<Good[], string>(g => g[0].Name, "Good1");
                query.AddToResultset<Good[], string>(g => g[1].Name, "Good2");
                query.AddOrderBy<Good[]>(g => g[1].Name);

                query.Execute();
                IList<dynamic> res = query.ReadAllDynamic();
                res.Count.Should().Be(1);
                dynamic r0 = res[0];
                ((int)r0.ID).Should().Be(linqCat2.ID);
                ((string)r0.Name).Should().Be(linqCat2.Name);
                ((string)r0.Good1).Should().Be(linqCatGoods[0].Name);
                ((string)r0.Good2).Should().Be(linqCatGoods[1].Name);
            }

            using (SelectEntitiesQueryBase query = connection.GetGenericSelectEntityQuery<Category>())
            {
                query.Where.Expression<Category>(c => c.ID == linqCat2.ID);

                query.FindType(typeof(Category));
                query.AddEntity<Category, Good[]>(typeof(Good), TableJoinType.Left, (c, g) => c == g[0].Category && g[0].ID == linqCatGoods[0].ID);
                query.AddEntity<Category, Good[]>(typeof(Good), TableJoinType.Left, (c, g) => c == g[1].Category && g[1].ID == linqCatGoods[1].ID);

                query.AddToResultset<Category, int>(c => c.ID, "ID");
                query.AddToResultset<Category, string>(c => c.Name, "Name");
                query.AddToResultset<Good[], string>(g => g[0].Name, "Good1");
                query.AddToResultset<Good[], string>(g => g[1].Name, "Good2");
                query.AddOrderBy<Good[]>(g => g[1].Name);

                query.Execute();
                IList<dynamic> res = query.ReadAllDynamic();
                res.Count.Should().Be(1);
                dynamic r0 = res[0];
                ((int)r0.ID).Should().Be(linqCat2.ID);
                ((string)r0.Name).Should().Be(linqCat2.Name);
                ((string)r0.Good1).Should().Be(linqCatGoods[0].Name);
                ((string)r0.Good2).Should().Be(linqCatGoods[1].Name);
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
                    sale.SalesDate.Should().BeOnOrAfter(prevSale.SalesDate);
                prevSale = sale;
            }

            allSales = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).ToArray();
            prevSale = null;
            foreach (Sale sale in allSales)
            {
                if (prevSale != null)
                {
                    (string.Compare(sale.Good.Name, prevSale.Good.Name) > 0 ||
                        (string.Compare(sale.Good.Name, prevSale.Good.Name) == 0 &&
                         sale.SalesDate > prevSale.SalesDate)).Should().BeTrue();
                }

                prevSale = sale;
            }

            tempSales = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).Take(5).Skip(10).ToArray();
            tempSales.Length.Should().Be(5);
            for (int i = 0; i < tempSales.Length; i++)
                tempSales[i].ID.Should().Be(allSales[i + 10].ID);

            ids = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).Select(sale => sale.ID).ToArray();
            dates = sales.OrderBy(sale => new { sale.Good.Name, sale.SalesDate }).Select(sale => sale.SalesDate).ToArray();

            for (int i = 0; i < allSales.Length; i++)
            {
                ids[i].Should().Be(allSales[i].ID);
                dates[i].Should().Be(allSales[i].SalesDate);
            }

            foreach (var v in sales.GroupBy(sale => sale.Good.ID).Select(r => new { Good = r.Key, Total = r.Sum(o => o.Total), Avg = r.Average(o => o.Total), LastTransaction = r.Max(o => o.SalesDate) }))
            {
                v.Total.Should().BeApproximately(allSales.Where(o => o.Good.ID == v.Good).Sum(o => o.Total), 1e-5);
                v.Avg.Should().BeApproximately(allSales.Where(o => o.Good.ID == v.Good).Average(o => o.Total), 1e-5);
                v.LastTransaction.Should().Be(allSales.Where(o => o.Good.ID == v.Good).Max(o => o.SalesDate));
            }

            foreach (var v in sales.GroupBy(sale => new { Good = sale.Good.ID, Person = sale.SalesPerson.ID }).Select(r => new { r.Key.Good, r.Key.Person, CountOf = r.Count(), FirstTransaction = r.Min(o => o.SalesDate) }))
            {
#pragma warning disable S2971 // "IEnumerable" LINQs should be simplified
#pragma warning disable RCS1077 // Optimize LINQ method call.
                v.CountOf.Should().Be(allSales.Where(o => o.Good.ID == v.Good && o.SalesPerson.ID == v.Person).Count());
#pragma warning restore RCS1077 // Optimize LINQ method call.
#pragma warning restore S2971 // "IEnumerable" LINQs should be simplified
                v.FirstTransaction.Should().Be(allSales.Where(o => o.Good.ID == v.Good && o.SalesPerson.ID == v.Person).Min(o => o.SalesDate));
            }

            foreach (var v in sales.Where(sale => Math.Abs(sale.Total) > 60))
            {
                v.Total.Should().BeGreaterThan(60);
            }

            foreach (var v in sales.Where(sale => sale.Good.Name.StartsWith('T') && sale.Total > 60).Select(r => new { Id = r.Good.ID, r.Good.Name, r.Total }))
            {
                v.Total.Should().BeGreaterThan(60);
                v.Name.Should().StartWith("T");
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
                    sale.SalesDate.Should().BeOnOrAfter(prevSale.SalesDate);
                prevSale = sale;
            }

            allSales = (from s in sales orderby new { s.Good.Name, s.SalesDate } select s).ToArray();
            prevSale = null;
            foreach (Sale sale in allSales)
            {
                if (prevSale != null)
                {
                    (string.Compare(sale.Good.Name, prevSale.Good.Name) > 0 ||
                        (string.Compare(sale.Good.Name, prevSale.Good.Name) == 0 &&
                         sale.SalesDate > prevSale.SalesDate)).Should().BeTrue();
                }

                prevSale = sale;
            }

            var query = from s in sales group s by s.Good.ID into g select new { Good = g.Key, Count = g.Count(), Total = g.Sum(o => o.Total), Avg = g.Average(o => o.Total), LastTransaction = g.Max(o => o.SalesDate) };
            foreach (var v in query)
            {
                v.Total.Should().BeApproximately(allSales.Where(o => o.Good.ID == v.Good).Sum(o => o.Total), 1e-5);
#pragma warning disable S2971 // "IEnumerable" LINQs should be simplified
#pragma warning disable RCS1077 // Optimize LINQ method call.
                v.Count.Should().Be(allSales.Where(o => o.Good.ID == v.Good).Count());
#pragma warning restore RCS1077 // Optimize LINQ method call.
#pragma warning restore S2971 // "IEnumerable" LINQs should be simplified
                v.Avg.Should().BeApproximately(allSales.Where(o => o.Good.ID == v.Good).Average(o => o.Total), 1e-5);
                v.LastTransaction.Should().Be(allSales.Where(o => o.Good.ID == v.Good).Max(o => o.SalesDate));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void DynamicEntity(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

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
                ((int)records[i].ID).Should().BeGreaterThan(0);
            }

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<DynamicDictionary>())
                query.RowCount.Should().Be(10);

            dynamic r5 = null;

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.AddOrderBy("Name");
                ICollection<dynamic> res = query.ReadAllDynamic();
                res.Count.Should().Be(10);
                string lastName = null;
                foreach (dynamic r in res)
                {
                    ((int)r.ID).Should().BeGreaterThan(0);
                    if (r.ID == 5)
                    {
                        r5 = r;
                    }
                    if (lastName != null)
                        ((int)string.CompareOrdinal(lastName, (string)r.Name)).Should().BeLessThanOrEqualTo(0);
                    lastName = r.Name;
                }
            }

            ((object)r5).Should().NotBeNull();
            using (ModifyEntityQuery query = connection.GetDeleteEntityQuery<DynamicDictionary>())
                query.Execute(r5);

            using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery<DynamicDictionary>())
                query.RowCount.Should().Be(9);

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.Where.Property("ID").Eq((int)r5.ID);
                query.Execute();
                ((object)query.ReadOneDynamic()).Should().BeNull();
            }

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.Where.Property("ID").Eq().Value((int)(r5.ID + 1));
                query.Execute();
                ((object)query.ReadOneDynamic()).Should().NotBeNull();
            }

            #region test dynamic query building

            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery<DynamicDictionary>())
            {
                query.Where.Property("ID").Eq().Value((int)(r5.ID + 1));
                query.Execute();
                ((object)query.ReadOneDynamic()).Should().NotBeNull();
            }

            #endregion test dynamic query building
        }
    }
}
