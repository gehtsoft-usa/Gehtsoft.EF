using System;
using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.DataSelecting
{
    // A realistic Customer scenario: an entity with two indexed plain columns and a nested JSON
    // document (primitives, an array, and sub-objects, several paths indexed). 50 deterministic
    // rows are inserted, then a set of real-life selection queries are run THREE ways each -
    // pure SQL, entity query with a string path, and entity query with a LINQ expression - and all
    // three are checked against expectations recomputed from the in-memory source list.
    //
    // Both array-element paths ($.ChildrenAge[0]) and sub-object paths ($.Address.State) are used in
    // WHERE and in projections.
    public class JsonRealLifeTest : IClassFixture<JsonRealLifeTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonRealLifeTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        // ---- the nested JSON document (default System.Text.Json => PascalCase keys) ----

        public class Card
        {
            public string Name { get; set; }
            public string Number { get; set; }
            public int ExpMo { get; set; }
            public int ExpYr { get; set; }
            public int Code { get; set; }
            public string Zip { get; set; }
        }

        public class Address
        {
            public string Street { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Zip { get; set; }
        }

        public class CustomerData
        {
            public decimal Income { get; set; }
            public DateTime DoB { get; set; }
            public bool Married { get; set; }
            public int Children { get; set; }
            public int[] ChildrenAge { get; set; }
            public Card Card { get; set; }
            public Address Address { get; set; }
        }

        [Entity(Scope = "crm_real", Table = "customer_real")]
        public class Customer
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [EntityProperty(Field = "first_name", Size = 64, Sorted = true)]
            public string FirstName { get; set; }

            [EntityProperty(Field = "last_name", Size = 64, Sorted = true, Nullable = true)]
            public string LastName { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            [JsonIndex("$.Income", DbType.Currency)]
            [JsonIndex("$.DoB", DbType.DateTime)]
            [JsonIndex("$.Married", DbType.Boolean)]
            [JsonIndex("$.Address.State", DbType.String)]
            [JsonIndex("$.Address.Zip", DbType.String)]
            public CustomerData Data { get; set; }
        }

        private static TableDescriptor Table => AllEntities.Inst[typeof(Customer)].TableDescriptor;

        private static readonly string[] FirstNames = { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth" };
        private static readonly string[] LastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };
        private static readonly string[] States = { "CA", "NY", "TX", "FL", "WA" };
        private static readonly string[] Cities = { "Los Angeles", "New York", "Houston", "Miami", "Seattle" };

        private static readonly DateTime BirthBase = new DateTime(1960, 1, 1);

        // The whole scenario runs once per driver (setup is expensive); every query below runs in
        // all three forms and is asserted against the in-memory expectation.
        private const decimal IncomeThreshold = 100000m;
        private const int FirstChildAgeThreshold = 10;
        private static readonly DateTime DobThreshold = new DateTime(1980, 1, 1);
        private const string DobThresholdIso = "1980-01-01T00:00:00";
        private const decimal FullClauseMinIncome = 50000m;      // Q7 WHERE lower bound
        private const decimal HavingSumThreshold = 1000000m;     // Q7 HAVING lower bound

        private static List<Customer> BuildCustomers()
        {
            var list = new List<Customer>();
            for (int i = 0; i < 50; i++)
            {
                int childCount = 1 + (i % 3);   // 1..3 - every customer has at least one child
                int[] ages = new int[childCount];
                for (int j = 0; j < childCount; j++)
                    ages[j] = 2 + (j * 3) + (i % 12);

                var customer = new Customer
                {
                    FirstName = FirstNames[i % FirstNames.Length],
                    LastName = (i % 7 == 0) ? null : LastNames[i % LastNames.Length],
                    Data = new CustomerData
                    {
                        Income = 40000m + (i * 2500m) + ((i % 4) * 125.50m),
                        DoB = BirthBase.AddDays(i * 200),
                        Married = (i % 3) != 0,
                        Children = childCount,
                        ChildrenAge = ages,
                        Card = new Card
                        {
                            Name = FirstNames[i % FirstNames.Length],
                            Number = $"4000-0000-0000-{1000 + i}",
                            ExpMo = (i % 12) + 1,
                            ExpYr = 2026 + (i % 6),
                            Code = 100 + i,
                            Zip = $"{90000 + i}",
                        },
                        Address = new Address
                        {
                            Street = $"{100 + i} Main St",
                            City = Cities[i % Cities.Length],
                            State = States[i % States.Length],
                            Zip = $"{90000 + i}",
                        },
                    },
                };
                list.Add(customer);
            }
            return list;
        }

        private static int? FirstChildAge(Customer c)
            => (c.Data.ChildrenAge != null && c.Data.ChildrenAge.Length > 0) ? c.Data.ChildrenAge[0] : (int?)null;

        private static List<int> Ids(IEnumerable<Customer> list)
        {
            var ids = new List<int>();
            foreach (var c in list)
                ids.Add(c.Id);
            ids.Sort();
            return ids;
        }

        private static List<int> Sorted(List<int> ids)
        {
            ids.Sort();
            return ids;
        }

        // --- pure SQL: run a prepared select (binding any parameters) and collect column 0 as ints ---
        private static List<int> PureSqlIds(SqlDbConnection connection, SelectQueryBuilder select, Action<SqlDbQuery> bind = null)
        {
            var ids = new List<int>();
            using (var query = connection.GetQuery(select))
            {
                bind?.Invoke(query);
                query.ExecuteReader();
                while (query.ReadNext())
                    ids.Add(query.GetValue<int>(0));
            }
            return Sorted(ids);
        }

        // --- entity: execute and collect ids of the read entities ---
        private static List<int> EntityIds(SelectEntitiesQuery query)
        {
            query.Execute();
            return Ids(query.ReadAll<Customer>());
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void RealLife_Customer_Queries(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var table = Table;

            using (var q = connection.GetDropEntityQuery<Customer>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<Customer>())
                q.Execute();

            try
            {
                var source = BuildCustomers();
                using (var q = connection.GetInsertEntityQuery<Customer>())
                    foreach (var c in source)
                        q.Execute(c);

                // ============================================================
                // Q1: customers in state "CA"  (sub-object string path, WHERE)
                // ============================================================
                var expectedCa = new List<int>();
                foreach (var c in source)
                    if (c.Data.Address.State == "CA")
                        expectedCa.Add(c.Id);
                Sorted(expectedCa);
                expectedCa.Should().NotBeEmpty();

                // pure SQL
                var s1 = connection.GetSelectQueryBuilder(table);
                s1.AddToResultset(table["Id"]);
                s1.Where.JsonValue(table["Data"], "$.Address.State", DbType.String).Eq().Parameter("p_state");
                PureSqlIds(connection, s1, q => q.BindParam<string>("p_state", "CA")).Should().Equal(expectedCa);

                // entity, string path
                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>("Data", "$.Address.State", DbType.String).Eq().Value("CA");
                    EntityIds(q).Should().Equal(expectedCa);
                }

                // entity, LINQ expression
                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>(c => c.Data.Address.State).Eq().Value("CA");
                    EntityIds(q).Should().Equal(expectedCa);
                }

                // ============================================================
                // Q2: income >= 100000  (numeric top-level JSON value, WHERE)
                // ============================================================
                var expectedRich = new List<int>();
                foreach (var c in source)
                    if (c.Data.Income >= IncomeThreshold)
                        expectedRich.Add(c.Id);
                Sorted(expectedRich);
                expectedRich.Should().NotBeEmpty();

                var s2 = connection.GetSelectQueryBuilder(table);
                s2.AddToResultset(table["Id"]);
                s2.Where.JsonValue(table["Data"], "$.Income", DbType.Currency).Ge().Parameter("p_income");
                PureSqlIds(connection, s2, q => q.BindParam<decimal>("p_income", IncomeThreshold)).Should().Equal(expectedRich);

                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>("Data", "$.Income", DbType.Currency).Ge().Value(IncomeThreshold);
                    EntityIds(q).Should().Equal(expectedRich);
                }

                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>(c => c.Data.Income).Ge().Value(IncomeThreshold);
                    EntityIds(q).Should().Equal(expectedRich);
                }

                // ============================================================
                // Q3: first child's age >= 10  (array-element path, WHERE)
                // ============================================================
                var expectedBigKids = new List<int>();
                foreach (var c in source)
                {
                    int? age0 = FirstChildAge(c);
                    if (age0.HasValue && age0.Value >= FirstChildAgeThreshold)
                        expectedBigKids.Add(c.Id);
                }
                Sorted(expectedBigKids);
                expectedBigKids.Should().NotBeEmpty();

                var s3 = connection.GetSelectQueryBuilder(table);
                s3.AddToResultset(table["Id"]);
                s3.Where.JsonValue(table["Data"], "$.ChildrenAge[0]", DbType.Int32).Ge().Parameter("p_age");
                PureSqlIds(connection, s3, q => q.BindParam<int>("p_age", FirstChildAgeThreshold)).Should().Equal(expectedBigKids);

                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>("Data", "$.ChildrenAge[0]", DbType.Int32).Ge().Value(FirstChildAgeThreshold);
                    EntityIds(q).Should().Equal(expectedBigKids);
                }

                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>(c => c.Data.ChildrenAge[0]).Ge().Value(FirstChildAgeThreshold);
                    EntityIds(q).Should().Equal(expectedBigKids);
                }

                // ============================================================
                // Q4: born on/after 1980  (DateTime stored as ISO string, WHERE)
                // ============================================================
                var expectedYoung = new List<int>();
                foreach (var c in source)
                    if (c.Data.DoB >= DobThreshold)
                        expectedYoung.Add(c.Id);
                Sorted(expectedYoung);
                expectedYoung.Should().NotBeEmpty();

                var s4 = connection.GetSelectQueryBuilder(table);
                s4.AddToResultset(table["Id"]);
                s4.Where.JsonValue(table["Data"], "$.DoB", DbType.String).Ge().Parameter("p_dob");
                PureSqlIds(connection, s4, q => q.BindParam<string>("p_dob", DobThresholdIso)).Should().Equal(expectedYoung);

                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>("Data", "$.DoB", DbType.String).Ge().Value(DobThresholdIso);
                    EntityIds(q).Should().Equal(expectedYoung);
                }

                // LINQ: force the DateTime leaf to be extracted/compared as an ISO string
                using (var q = connection.GetSelectEntitiesQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>(c => c.Data.DoB, DbType.String).Ge().Value(DobThresholdIso);
                    EntityIds(q).Should().Equal(expectedYoung);
                }

                // ============================================================
                // Q5: projection - id, city (sub-object), first child's age (array), income;
                //     ordered by income descending. (sub-object + array in PROJECTION)
                // ============================================================
                var expectedOrder = new List<Customer>(source);
                expectedOrder.Sort((a, b) => b.Data.Income.CompareTo(a.Data.Income));
                var expectedOrderIds = new List<int>();
                foreach (var c in expectedOrder)
                    expectedOrderIds.Add(c.Id);

                AssertProjection(PureSqlProjection(connection, table), source, expectedOrderIds);
                AssertProjection(EntityStringProjection(connection), source, expectedOrderIds);
                AssertProjection(EntityLinqProjection(connection), source, expectedOrderIds);

                // ============================================================
                // Q6: SUM(income) and COUNT grouped by state (sub-object in GROUP BY / aggregation)
                // ============================================================
                var expectedCount = new Dictionary<string, int>();
                var expectedSum = new Dictionary<string, decimal>();
                foreach (var c in source)
                {
                    string st = c.Data.Address.State;
                    expectedCount.TryGetValue(st, out int n);
                    expectedCount[st] = n + 1;
                    expectedSum.TryGetValue(st, out decimal sum);
                    expectedSum[st] = sum + c.Data.Income;
                }

                AssertGrouping(PureSqlGrouping(connection, table), expectedCount, expectedSum);
                AssertGrouping(EntityStringGrouping(connection), expectedCount, expectedSum);
                AssertGrouping(EntityLinqGrouping(connection), expectedCount, expectedSum);

                // ============================================================
                // Q7: JSON used in EVERY select clause at once -
                //     resultset (state + SUM income), WHERE (income >= 50000),
                //     GROUP BY (state), HAVING (SUM income > 1,000,000), ORDER BY (state asc).
                // ============================================================
                var fcSum = new Dictionary<string, decimal>();
                foreach (var c in source)
                {
                    if (c.Data.Income < FullClauseMinIncome)
                        continue;
                    string st = c.Data.Address.State;
                    fcSum.TryGetValue(st, out decimal sum);
                    fcSum[st] = sum + c.Data.Income;
                }
                var fcStates = new List<string>();
                foreach (var kv in fcSum)
                    if (kv.Value > HavingSumThreshold)
                        fcStates.Add(kv.Key);
                fcStates.Sort(StringComparer.Ordinal);
                fcStates.Should().HaveCountGreaterThan(0).And.HaveCountLessThan(5, "the HAVING clause must actually filter some groups out");

                AssertFullClause(PureSqlFullClause(connection, table), fcStates, fcSum);
                AssertFullClause(EntityStringFullClause(connection), fcStates, fcSum);
                AssertFullClause(EntityLinqFullClause(connection), fcStates, fcSum);

                // ============================================================
                // Q8: COUNT query type with a JSON WHERE (income >= 100000)
                // ============================================================
                int expectedRichCount = expectedRich.Count;

                var cs = connection.GetSelectQueryBuilder(table);
                cs.AddToResultset(AggFn.Count, table["Id"]);
                cs.Where.JsonValue(table["Data"], "$.Income", DbType.Currency).Ge().Parameter("p_cnt");
                using (var q = connection.GetQuery(cs))
                {
                    q.BindParam<decimal>("p_cnt", IncomeThreshold);
                    q.ExecuteReader();
                    q.ReadNext();
                    q.GetValue<int>(0).Should().Be(expectedRichCount);
                }

                using (var q = connection.GetSelectEntitiesCountQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>("Data", "$.Income", DbType.Currency).Ge().Value(IncomeThreshold);
                    q.RowCount.Should().Be(expectedRichCount);
                }

                using (var q = connection.GetSelectEntitiesCountQuery<Customer>())
                {
                    q.Where.JsonPropertyOf<Customer>(c => c.Data.Income).Ge().Value(IncomeThreshold);
                    q.RowCount.Should().Be(expectedRichCount);
                }

                // ============================================================
                // Q9: DELETE query type with a JSON WHERE (three predicates, three forms)
                // ============================================================
                var live = new List<Customer>(source);
                CountAll(connection).Should().Be(live.Count);

                // entity, string path: delete all in state "WA"
                using (var d = connection.GetMultiDeleteEntityQuery<Customer>())
                {
                    d.Where.JsonPropertyOf<Customer>("Data", "$.Address.State", DbType.String).Eq().Value("WA");
                    d.Execute();
                }
                live.RemoveAll(c => c.Data.Address.State == "WA");
                CountAll(connection).Should().Be(live.Count);

                // entity, LINQ: delete all in state "TX"
                using (var d = connection.GetMultiDeleteEntityQuery<Customer>())
                {
                    d.Where.JsonPropertyOf<Customer>(c => c.Data.Address.State).Eq().Value("TX");
                    d.Execute();
                }
                live.RemoveAll(c => c.Data.Address.State == "TX");
                CountAll(connection).Should().Be(live.Count);

                // pure SQL: delete the low earners (income < 45000)
                var del = connection.GetDeleteQueryBuilder(table);
                del.Where.JsonValue(table["Data"], "$.Income", DbType.Currency).Ls().Parameter("p_low");
                using (var q = connection.GetQuery(del))
                {
                    q.BindParam<decimal>("p_low", 45000m);
                    q.ExecuteNoData();
                }
                live.RemoveAll(c => c.Data.Income < 45000m);
                CountAll(connection).Should().Be(live.Count);
            }
            finally
            {
                using var q = connection.GetDropEntityQuery<Customer>();
                q.Execute();
            }
        }

        // ---------- projection rows ----------

        private sealed class ProjRow
        {
            public int Id;
            public string City;
            public int? Age0;
            public decimal Income;
        }

        private void AssertProjection(List<ProjRow> rows, List<Customer> source, List<int> expectedOrderIds)
        {
            var byId = new Dictionary<int, Customer>();
            foreach (var c in source)
                byId[c.Id] = c;

            var actualOrderIds = new List<int>();
            foreach (var r in rows)
            {
                actualOrderIds.Add(r.Id);
                var c = byId[r.Id];
                r.City.Should().Be(c.Data.Address.City);
                r.Age0.Should().Be(FirstChildAge(c));
                r.Income.Should().BeApproximately(c.Data.Income, 0.05m);
            }
            actualOrderIds.Should().Equal(expectedOrderIds);
        }

        private static List<ProjRow> PureSqlProjection(SqlDbConnection connection, TableDescriptor table)
        {
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(table["Id"]);
            select.AddJsonValueToResultset(table["Data"], "$.Address.City", DbType.String, "city");
            select.AddJsonValueToResultset(table["Data"], "$.ChildrenAge[0]", DbType.Int32, "age0");
            select.AddJsonValueToResultset(table["Data"], "$.Income", DbType.Currency, "income");
            select.AddJsonValueToOrderBy(table["Data"], "$.Income", DbType.Currency, SortDir.Desc);

            var rows = new List<ProjRow>();
            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                while (query.ReadNext())
                    rows.Add(new ProjRow
                    {
                        Id = query.GetValue<int>(0),
                        City = query.GetValue<string>(1),
                        Age0 = query.IsNull(2) ? (int?)null : query.GetValue<int>(2),
                        Income = query.GetValue<decimal>(3),
                    });
            }
            return rows;
        }

        private static List<ProjRow> EntityStringProjection(SqlDbConnection connection)
        {
            using var q = connection.GetSelectEntitiesQueryBase(typeof(Customer));
            q.AddToResultset(typeof(Customer), 0, "Id", "cid");
            q.AddJsonValueToResultset<Customer>("Data", "$.Address.City", DbType.String, "city");
            q.AddJsonValueToResultset<Customer>("Data", "$.ChildrenAge[0]", DbType.Int32, "age0");
            q.AddJsonValueToResultset<Customer>("Data", "$.Income", DbType.Currency, "income");
            q.AddJsonValueToOrderBy<Customer>("Data", "$.Income", DbType.Currency, SortDir.Desc);
            return ReadProjection(q);
        }

        private static List<ProjRow> EntityLinqProjection(SqlDbConnection connection)
        {
            using var q = connection.GetSelectEntitiesQueryBase(typeof(Customer));
            q.AddToResultset(typeof(Customer), 0, "Id", "cid");
            q.AddJsonValueToResultset<Customer>(c => c.Data.Address.City, "city");
            q.AddJsonValueToResultset<Customer>(c => c.Data.ChildrenAge[0], "age0");
            q.AddJsonValueToResultset<Customer>(c => c.Data.Income, "income");
            q.AddJsonValueToOrderBy<Customer>(c => c.Data.Income, SortDir.Desc);
            return ReadProjection(q);
        }

        private static List<ProjRow> ReadProjection(SelectEntitiesQueryBase q)
        {
            var rows = new List<ProjRow>();
            q.Execute();
            foreach (dynamic row in q.ReadAllDynamic())
            {
                var d = (IDictionary<string, object>)row;
                rows.Add(new ProjRow
                {
                    Id = Convert.ToInt32(d["cid"]),
                    City = (string)d["city"],
                    Age0 = d["age0"] == null ? (int?)null : Convert.ToInt32(d["age0"]),
                    Income = Convert.ToDecimal(d["income"]),
                });
            }
            return rows;
        }

        // ---------- grouping rows ----------

        private void AssertGrouping(Dictionary<string, Tuple<int, decimal>> actual, Dictionary<string, int> expectedCount, Dictionary<string, decimal> expectedSum)
        {
            actual.Should().HaveCount(expectedCount.Count);
            foreach (var kv in expectedCount)
            {
                actual.Should().ContainKey(kv.Key);
                actual[kv.Key].Item1.Should().Be(kv.Value);
                actual[kv.Key].Item2.Should().BeApproximately(expectedSum[kv.Key], 0.5m);
            }
        }

        private static Dictionary<string, Tuple<int, decimal>> PureSqlGrouping(SqlDbConnection connection, TableDescriptor table)
        {
            var select = connection.GetSelectQueryBuilder(table);
            select.AddJsonValueToResultset(table["Data"], "$.Address.State", DbType.String, "state");
            // COUNT(DISTINCT income): incomes are globally distinct so this equals the group size
            select.AddJsonValueToResultset(AggFn.Count, table["Data"], "$.Income", DbType.Currency, "cnt");
            select.AddJsonValueToResultset(AggFn.Sum, table["Data"], "$.Income", DbType.Currency, "total");
            select.AddJsonValueToGroupBy(table["Data"], "$.Address.State", DbType.String);

            var result = new Dictionary<string, Tuple<int, decimal>>();
            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                while (query.ReadNext())
                    result[query.GetValue<string>(0)] = new Tuple<int, decimal>(query.GetValue<int>(1), query.GetValue<decimal>(2));
            }
            return result;
        }

        private static Dictionary<string, Tuple<int, decimal>> EntityStringGrouping(SqlDbConnection connection)
        {
            using var q = connection.GetSelectEntitiesQueryBase(typeof(Customer));
            q.AddJsonValueToResultset<Customer>("Data", "$.Address.State", DbType.String, "state");
            q.AddJsonValueToResultset<Customer>(AggFn.Count, "Data", "$.Income", DbType.Currency, "cnt");
            q.AddJsonValueToResultset<Customer>(AggFn.Sum, "Data", "$.Income", DbType.Currency, "total");
            q.AddJsonValueToGroupBy<Customer>("Data", "$.Address.State", DbType.String);
            return ReadGrouping(q);
        }

        private static Dictionary<string, Tuple<int, decimal>> EntityLinqGrouping(SqlDbConnection connection)
        {
            using var q = connection.GetSelectEntitiesQueryBase(typeof(Customer));
            q.AddJsonValueToResultset<Customer>(c => c.Data.Address.State, "state");
            q.AddJsonValueToResultset<Customer>(AggFn.Count, c => c.Data.Income, "cnt");
            q.AddJsonValueToResultset<Customer>(AggFn.Sum, c => c.Data.Income, "total", DbType.Currency);
            q.AddJsonValueToGroupBy<Customer>(c => c.Data.Address.State);
            return ReadGrouping(q);
        }

        private static Dictionary<string, Tuple<int, decimal>> ReadGrouping(SelectEntitiesQueryBase q)
        {
            var result = new Dictionary<string, Tuple<int, decimal>>();
            q.Execute();
            foreach (dynamic row in q.ReadAllDynamic())
            {
                var d = (IDictionary<string, object>)row;
                result[(string)d["state"]] = new Tuple<int, decimal>(Convert.ToInt32(d["cnt"]), Convert.ToDecimal(d["total"]));
            }
            return result;
        }

        // ---------- full-clause query (resultset + where + group by + having + order by) ----------

        private void AssertFullClause(List<Tuple<string, decimal>> rows, List<string> expectedStates, Dictionary<string, decimal> expectedSum)
        {
            var actualStates = new List<string>();
            foreach (var r in rows)
            {
                actualStates.Add(r.Item1);
                r.Item2.Should().BeApproximately(expectedSum[r.Item1], 0.5m);
            }
            actualStates.Should().Equal(expectedStates);
        }

        private static List<Tuple<string, decimal>> PureSqlFullClause(SqlDbConnection connection, TableDescriptor table)
        {
            var select = connection.GetSelectQueryBuilder(table);
            select.AddJsonValueToResultset(table["Data"], "$.Address.State", DbType.String, "state");
            select.AddJsonValueToResultset(AggFn.Sum, table["Data"], "$.Income", DbType.Currency, "total");
            select.Where.JsonValue(table["Data"], "$.Income", DbType.Currency).Ge().Parameter("p_min");
            select.AddJsonValueToGroupBy(table["Data"], "$.Address.State", DbType.String);
            select.Having.JsonValue(table["Data"], "$.Income", DbType.Currency).Sum().Gt().Parameter("p_hav");
            select.AddJsonValueToOrderBy(table["Data"], "$.Address.State", DbType.String, SortDir.Asc);

            var rows = new List<Tuple<string, decimal>>();
            using (var query = connection.GetQuery(select))
            {
                query.BindParam<decimal>("p_min", FullClauseMinIncome);
                query.BindParam<decimal>("p_hav", HavingSumThreshold);
                query.ExecuteReader();
                while (query.ReadNext())
                    rows.Add(new Tuple<string, decimal>(query.GetValue<string>(0), query.GetValue<decimal>(1)));
            }
            return rows;
        }

        private static List<Tuple<string, decimal>> EntityStringFullClause(SqlDbConnection connection)
        {
            using var q = connection.GetSelectEntitiesQueryBase(typeof(Customer));
            q.AddJsonValueToResultset<Customer>("Data", "$.Address.State", DbType.String, "state");
            q.AddJsonValueToResultset<Customer>(AggFn.Sum, "Data", "$.Income", DbType.Currency, "total");
            q.Where.JsonPropertyOf<Customer>("Data", "$.Income", DbType.Currency).Ge().Value(FullClauseMinIncome);
            q.AddJsonValueToGroupBy<Customer>("Data", "$.Address.State", DbType.String);
            q.Having.JsonPropertyOf<Customer>("Data", "$.Income", DbType.Currency).Sum().Gt().Value(HavingSumThreshold);
            q.AddJsonValueToOrderBy<Customer>("Data", "$.Address.State", DbType.String, SortDir.Asc);
            return ReadFullClause(q);
        }

        private static List<Tuple<string, decimal>> EntityLinqFullClause(SqlDbConnection connection)
        {
            using var q = connection.GetSelectEntitiesQueryBase(typeof(Customer));
            q.AddJsonValueToResultset<Customer>(c => c.Data.Address.State, "state");
            q.AddJsonValueToResultset<Customer>(AggFn.Sum, c => c.Data.Income, "total", DbType.Currency);
            q.Where.JsonPropertyOf<Customer>(c => c.Data.Income).Ge().Value(FullClauseMinIncome);
            q.AddJsonValueToGroupBy<Customer>(c => c.Data.Address.State);
            q.Having.JsonPropertyOf<Customer>(c => c.Data.Income).Sum().Gt().Value(HavingSumThreshold);
            q.AddJsonValueToOrderBy<Customer>(c => c.Data.Address.State, SortDir.Asc);
            return ReadFullClause(q);
        }

        private static List<Tuple<string, decimal>> ReadFullClause(SelectEntitiesQueryBase q)
        {
            var rows = new List<Tuple<string, decimal>>();
            q.Execute();
            foreach (dynamic row in q.ReadAllDynamic())
            {
                var d = (IDictionary<string, object>)row;
                rows.Add(new Tuple<string, decimal>((string)d["state"], Convert.ToDecimal(d["total"])));
            }
            return rows;
        }

        private static int CountAll(SqlDbConnection connection)
        {
            using var q = connection.GetSelectEntitiesCountQuery<Customer>();
            return q.RowCount;
        }
    }
}
