using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;
using Vt = Gehtsoft.EF.Db.SqlDb.EntityQueries.DynamicPropertyValueType;

namespace Gehtsoft.EF.Test.DynamicProperties.DataSelecting
{
    /// <summary>
    /// A real-life scenario suite around an "account application" whose fields are all dynamic
    /// properties (Occupation/DoB/Employed/Rejected/ChildrenCount/Income). One in-memory SQLite
    /// database, a deterministic 50-row seed, and the typical processing flows an application system
    /// runs: screening (WHERE across every type/operator), decisioning (mass update / mass delete /
    /// single CRUD), reporting (projection / ORDER BY / GROUP BY / HAVING / aggregates), individual
    /// review (load), and a paginated viewer.
    /// </summary>
    public class DynamicPropertiesApplicationScenarioTest
    {
        [Entity(Scope = "dp_app", Table = "dp_app")]
        [DynamicProperties]
        public class Application : IDynamicPropertiesOwner
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 64, Nullable = true)] public string Applicant { get; set; }
            [EntityProperty(Nullable = true)] public int? Score { get; set; }
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private const string Table = "dp_app";
        private const string Props = "dp_app_props";
        private const double IncomeFloor = 60000.0;

        // A fixed "today" so age comparisons are deterministic regardless of when the test runs.
        private static readonly DateTime Reference = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static DateTime Age18Cutoff => Reference.AddYears(-18);

        private static readonly string[] Occupations = { "Engineer", "Teacher", "Doctor", "Artist", "Clerk" };

        // The deterministic model of applicant i (i = 0..49).
        private static string Applicant(int i) => $"A{i:D2}";
        private static string Occupation(int i) => Occupations[i % 5];
        private static int Age(int i) => 16 + i;                                  // 16..65, unique
        private static DateTime DoB(int i) => Reference.AddYears(-Age(i)).AddDays(-1);
        private static bool Employed(int i) => (i % 3) != 0;                       // 33 employed, 17 not
        private static int Children(int i) => i % 5;                              // 0..4
        private static double Income(int i) => 20000.0 + 2000.0 * i;              // 20000..118000, unique

        private static void Seed(SqlDbConnection c)
        {
            for (int i = 0; i < 50; i++)
            {
                var e = new Application { Applicant = Applicant(i) };
                var bag = e.InitializeDynamicProperties();
                bag.Set("Occupation", Occupation(i));
                bag.Set("DoB", DoB(i));
                bag.Set("Employed", Employed(i));
                bag.Set("Rejected", false);
                bag.Set("ChildrenCount", Children(i));
                bag.Set("Income", Income(i));
                using var q = c.GetInsertEntityQuery<Application>();
                q.Execute(e);
            }
        }

        private static SqlDbConnection Fresh()
        {
            var c = SqliteDbConnectionFactory.CreateMemory();
            using (var q = c.GetCreateEntityQuery<Application>()) q.Execute();
            Seed(c);
            return c;
        }

        // ---- helpers ----

        private static long Scalar(SqlDbConnection c, string sql)
        {
            using var q = c.GetQuery(sql, true);
            q.ExecuteReader();
            q.ReadNext();
            return q.GetValue<long>(0);
        }

        private static long Owners(SqlDbConnection c) => Scalar(c, $"SELECT COUNT(*) FROM {Table}");
        private static long PropRows(SqlDbConnection c) => Scalar(c, $"SELECT COUNT(*) FROM {Props}");
        private static long PropRows(SqlDbConnection c, string name) => Scalar(c, $"SELECT COUNT(*) FROM {Props} WHERE name = '{name}'");

        private static int Count(SqlDbConnection c, Action<EntityQueryConditionBuilder> where)
        {
            using var q = c.GetSelectEntitiesCountQuery<Application>();
            where?.Invoke(q.Where);
            return q.RowCount;
        }

        private static List<string> Applicants(SqlDbConnection c, Action<EntityQueryConditionBuilder> where)
        {
            using var q = c.GetSelectEntitiesQuery<Application>();
            where?.Invoke(q.Where);
            var list = new List<string>();
            foreach (var a in q.ReadAll<Application>())
                list.Add(a.Applicant);
            return list;
        }

        private static List<IDictionary<string, object>> Rows(SelectEntitiesQueryBase q)
        {
            var list = new List<IDictionary<string, object>>();
            foreach (var r in q.ReadAllDynamic())
                list.Add((IDictionary<string, object>)r);
            return list;
        }

        private static Application Load(SqlDbConnection c, string applicant, bool preload)
        {
            using var q = c.GetSelectEntitiesQuery<Application>();
            q.PreloadProperties = preload;
            q.Where.Property(nameof(Application.Applicant)).Eq(applicant);
            if (preload)
            {
                var all = new List<Application>(q.ReadAll<Application>());
                return all.Count > 0 ? all[0] : null;
            }
            return q.ReadOne<Application>();
        }

        // ======================= A. Intake =======================

        [Fact]
        public void S01_Seed_CreatesOwnersAndPropertyRows()
        {
            using var c = Fresh();
            Owners(c).Should().Be(50);
            PropRows(c).Should().Be(50 * 6);   // six properties per applicant
        }

        // ======================= B. Screening (WHERE) =======================

        [Fact]
        public void S02_Minors_AreUnderEighteen()
        {
            using var c = Fresh();
            // DoB later than (today - 18y) => younger than 18 => i in {0,1}
            Count(c, w => w.DynamicPropertyOf<Application>("DoB").Gt(Age18Cutoff)).Should().Be(2);
        }

        [Fact]
        public void S03_EmployedApplicants()
        {
            using var c = Fresh();
            Count(c, w => w.DynamicPropertyOf<Application>("Employed").Eq(true)).Should().Be(33);
        }

        [Fact]
        public void S04_LowIncomeUnemployed()
        {
            using var c = Fresh();
            // Employed == false AND Income < 60000 => i in {0,3,6,9,12,15,18} => 7
            Count(c, w => w.DynamicPropertyOf<Application>("Employed").Eq(false)
                           .And().DynamicPropertyOf<Application>("Income").Ls(IncomeFloor)).Should().Be(7);
        }

        [Fact]
        public void S05_OccupationLookup()
        {
            using var c = Fresh();
            Count(c, w => w.DynamicPropertyOf<Application>("Occupation").Eq("Engineer")).Should().Be(10);
            Count(c, w => w.DynamicPropertyOf<Application>("Occupation").Like("Eng%")).Should().Be(10);
        }

        [Fact]
        public void S06_FamilySize()
        {
            using var c = Fresh();
            Count(c, w => w.DynamicPropertyOf<Application>("ChildrenCount").Ge(3)).Should().Be(20);
            Count(c, w => w.DynamicPropertyOf<Application>("ChildrenCount").Gt(0)).Should().Be(40);
        }

        [Fact]
        public void S07_CompositeEligibility()
        {
            using var c = Fresh();
            // Employed AND Income >= floor AND 18+ AND not Rejected => i in 20..49 with i%3!=0 => 20
            var names = Applicants(c, w => w.DynamicPropertyOf<Application>("Employed").Eq(true)
                                            .And().DynamicPropertyOf<Application>("Income").Ge(IncomeFloor)
                                            .And().DynamicPropertyOf<Application>("DoB").Le(Age18Cutoff)
                                            .And().DynamicPropertyOf<Application>("Rejected").Eq(false));
            names.Should().HaveCount(20);
        }

        [Fact]
        public void S08_CompositeEligibility_CountQueryParity()
        {
            using var c = Fresh();
            Action<EntityQueryConditionBuilder> rule = w =>
                w.DynamicPropertyOf<Application>("Employed").Eq(true)
                 .And().DynamicPropertyOf<Application>("Income").Ge(IncomeFloor)
                 .And().DynamicPropertyOf<Application>("DoB").Le(Age18Cutoff)
                 .And().DynamicPropertyOf<Application>("Rejected").Eq(false);

            Count(c, rule).Should().Be(Applicants(c, rule).Count);
        }

        // ======================= C. Decisioning =======================

        [Fact]
        public void S09_MassAutoReject_ByPropertyCondition()
        {
            using var c = Fresh();
            using (var q = c.GetMultiUpdateEntityQuery<Application>())
            {
                q.SetDynamicProperty("Rejected", true);
                q.Where.DynamicPropertyOf<Application>("Employed").Eq(false)
                       .And().DynamicPropertyOf<Application>("Income").Ls(IncomeFloor);
                q.Execute();
            }

            Count(c, w => w.DynamicPropertyOf<Application>("Rejected").Eq(true)).Should().Be(7);
            Count(c, w => w.DynamicPropertyOf<Application>("Rejected").Eq(false)).Should().Be(43);
        }

        [Fact]
        public void S10_MixedMassUpdate_OwnerColumnAndProperty()
        {
            using var c = Fresh();
            using (var q = c.GetMultiUpdateEntityQuery<Application>())
            {
                q.AddUpdateColumn(nameof(Application.Score), 100);   // owner column
                q.SetDynamicProperty("Reviewed", true);             // new dynamic property
                q.Where.DynamicPropertyOf<Application>("Employed").Eq(true);
                q.Execute();
            }

            Scalar(c, $"SELECT COUNT(*) FROM {Table} WHERE score = 100").Should().Be(33);
            Scalar(c, $"SELECT COUNT(*) FROM {Table} WHERE score IS NULL").Should().Be(17);
            PropRows(c, "Reviewed").Should().Be(33);
        }

        [Fact]
        public void S11_MassRemoveProperty()
        {
            using var c = Fresh();
            using (var q = c.GetMultiUpdateEntityQuery<Application>())
            {
                q.RemoveDynamicProperty("Occupation");
                q.Where.DynamicPropertyOf<Application>("Employed").Eq(false);   // 17 unemployed
                q.Execute();
            }

            PropRows(c, "Occupation").Should().Be(33);   // 50 - 17

            // an unemployed applicant no longer projects an Occupation; an employed one still does
            Load(c, Applicant(0), preload: true).DynamicProperties.Contains("Occupation").Should().BeFalse();
            Load(c, Applicant(1), preload: true).DynamicProperties.Get<string>("Occupation").Should().Be(Occupation(1));
        }

        [Fact]
        public void S12_SingleUpdate_NetChanges()
        {
            using var c = Fresh();
            var a = Load(c, Applicant(10), preload: true);
            a.DynamicProperties.Set("Notes", "vip");      // add
            a.DynamicProperties.Set("Income", 999999.0);  // change
            a.DynamicProperties.Remove("ChildrenCount");  // remove
            using (var q = c.GetUpdateEntityQuery<Application>()) q.Execute(a);

            var r = Load(c, Applicant(10), preload: true);
            r.DynamicProperties.Get<string>("Notes").Should().Be("vip");
            r.DynamicProperties.Get<double>("Income").Should().Be(999999.0);
            r.DynamicProperties.Contains("ChildrenCount").Should().BeFalse();
            r.DynamicProperties.Get<string>("Occupation").Should().Be(Occupation(10)); // untouched
        }

        [Fact]
        public void S13_MassDelete_ByDynamicProperty_Cascades()
        {
            using var c = Fresh();
            using (var q = c.GetMultiUpdateEntityQuery<Application>())
            {
                q.SetDynamicProperty("Rejected", true);
                q.Where.DynamicPropertyOf<Application>("Employed").Eq(false)
                       .And().DynamicPropertyOf<Application>("Income").Ls(IncomeFloor);
                q.Execute();
            }

            using (var q = c.GetMultiDeleteEntityQuery<Application>())
            {
                q.Where.DynamicPropertyOf<Application>("Rejected").Eq(true);
                q.Execute();
            }

            Owners(c).Should().Be(43);
            PropRows(c).Should().Be(43 * 6);   // the 7 rejected applicants' property rows are gone too
        }

        [Fact]
        public void S14_MassDelete_ByRegularColumn_Cascades()
        {
            using var c = Fresh();
            using (var q = c.GetMultiDeleteEntityQuery<Application>())
            {
                q.Where.Property(nameof(Application.Applicant)).Like("A0%");   // A00..A09 => 10
                q.Execute();
            }

            Owners(c).Should().Be(40);
            PropRows(c).Should().Be(40 * 6);
        }

        [Fact]
        public void S15_SingleDelete_Cascades()
        {
            using var c = Fresh();
            var a = Load(c, Applicant(5), preload: false);
            int id = a.Id;
            using (var q = c.GetDeleteEntityQuery<Application>()) q.Execute(a);

            Scalar(c, $"SELECT COUNT(*) FROM {Props} WHERE owner = {id}").Should().Be(0);
            Owners(c).Should().Be(49);
            PropRows(c).Should().Be(49 * 6);
        }

        [Fact]
        public void S16_InsertSelect_IsRejected()
        {
            using var c = Fresh();
            using var src = c.GetSelectEntitiesQueryBase<Application>();
            Action act = () => c.GetInsertSelectEntityQuery<Application>(src);
            act.Should().Throw<NotSupportedException>();
        }

        // ======================= D. Reporting & analytics =======================

        [Fact]
        public void S17_RankedByIncome_Descending()
        {
            using var c = Fresh();
            using var q = c.GetSelectEntitiesQueryBase<Application>();
            q.AddToResultset(typeof(Application), nameof(Application.Applicant), "applicant");
            q.AddDynamicPropertyToResultset<Application>("Income", Vt.Real, "income");
            q.Where.DynamicPropertyOf<Application>("Rejected").Eq(false);
            q.AddDynamicPropertyToOrderBy<Application>("Income", Vt.Real, SortDir.Desc);

            var rows = Rows(q);
            rows.Should().HaveCount(50);
            ((string)rows[0]["applicant"]).Should().Be(Applicant(49)); // highest income
            ((double)rows[0]["income"]).Should().Be(Income(49));
            for (int k = 1; k < rows.Count; k++)
                ((double)rows[k]["income"]).Should().BeLessThanOrEqualTo((double)rows[k - 1]["income"]);
        }

        [Fact]
        public void S18_ApplicantsPerOccupation()
        {
            using var c = Fresh();
            using var q = c.GetSelectEntitiesQueryBase<Application>();
            q.AddDynamicPropertyToResultset<Application>("Occupation", Vt.String, "occ");
            q.AddToResultset(AggFn.Count, typeof(Application), nameof(Application.Id), "cnt");
            q.AddDynamicPropertyToGroupBy<Application>("Occupation", Vt.String);

            var perOcc = new Dictionary<string, int>();
            foreach (var row in Rows(q))
                perOcc[(string)row["occ"]] = Convert.ToInt32(row["cnt"]);

            perOcc.Should().HaveCount(5);
            foreach (var occ in Occupations)
                perOcc[occ].Should().Be(10);
        }

        [Fact]
        public void S19_AverageIncomePerOccupation()
        {
            using var c = Fresh();
            using var q = c.GetSelectEntitiesQueryBase<Application>();
            q.AddDynamicPropertyToResultset<Application>("Occupation", Vt.String, "occ");
            q.AddDynamicPropertyToResultset<Application>(AggFn.Avg, "Income", Vt.Real, "avg");
            q.AddDynamicPropertyToGroupBy<Application>("Occupation", Vt.String);

            var avg = new Dictionary<string, double>();
            foreach (var row in Rows(q))
                avg[(string)row["occ"]] = (double)row["avg"];

            // occupation k (i%5==k): avg income = 65000 + 2000*k
            avg["Engineer"].Should().BeApproximately(65000, 0.001);
            avg["Teacher"].Should().BeApproximately(67000, 0.001);
            avg["Doctor"].Should().BeApproximately(69000, 0.001);
            avg["Artist"].Should().BeApproximately(71000, 0.001);
            avg["Clerk"].Should().BeApproximately(73000, 0.001);
        }

        [Fact]
        public void S20_WellPaidOccupations_Having()
        {
            using var c = Fresh();
            using var q = c.GetSelectEntitiesQueryBase<Application>();
            q.AddDynamicPropertyToResultset<Application>("Occupation", Vt.String, "occ");
            q.AddDynamicPropertyToResultset<Application>(AggFn.Avg, "Income", Vt.Real, "avg");
            q.AddDynamicPropertyToGroupBy<Application>("Occupation", Vt.String);
            q.HavingDynamicPropertyOf<Application>("Income", Vt.Real).Avg().Gt(70000.0);

            var occs = new List<string>();
            foreach (var row in Rows(q))
                occs.Add((string)row["occ"]);

            occs.Should().BeEquivalentTo(new[] { "Artist", "Clerk" });
        }

        [Fact]
        public void S21_OldestAndYoungest()
        {
            using var c = Fresh();
            using var q = c.GetSelectEntitiesQueryBase<Application>();
            q.AddDynamicPropertyToResultset<Application>(AggFn.Min, "DoB", Vt.DateTime, "oldest");
            q.AddDynamicPropertyToResultset<Application>(AggFn.Max, "DoB", Vt.DateTime, "youngest");

            var row = (IDictionary<string, object>)q.ReadOneDynamic();
            ((DateTime)row["oldest"]).Should().Be(DoB(49));    // age 65
            ((DateTime)row["youngest"]).Should().Be(DoB(0));   // age 16
        }

        [Fact]
        public void S22_ApprovedPoolTotals()
        {
            using var c = Fresh();
            using var q = c.GetSelectEntitiesQueryBase<Application>();
            q.AddDynamicPropertyToResultset<Application>(AggFn.Sum, "Income", Vt.Real, "totalIncome");
            q.AddDynamicPropertyToResultset<Application>(AggFn.Sum, "ChildrenCount", Vt.Integer, "totalKids");
            q.Where.DynamicPropertyOf<Application>("Rejected").Eq(false);

            var row = (IDictionary<string, object>)q.ReadOneDynamic();
            ((double)row["totalIncome"]).Should().Be(3_450_000.0);  // sum of 20000+2000i, i=0..49
            Convert.ToInt32(row["totalKids"]).Should().Be(100);     // (0+1+2+3+4)*10
        }

        [Fact]
        public void S23_MultiFieldReport()
        {
            using var c = Fresh();
            using var q = c.GetSelectEntitiesQueryBase<Application>();
            q.AddToResultset(typeof(Application), nameof(Application.Applicant), "applicant");
            q.AddDynamicPropertyToResultset<Application>("Occupation", Vt.String, "occ");
            q.AddDynamicPropertyToResultset<Application>("Income", Vt.Real, "income");
            q.AddDynamicPropertyToResultset<Application>("Employed", Vt.Boolean, "employed");
            q.AddDynamicPropertyToResultset<Application>("ChildrenCount", Vt.Integer, "kids");
            q.Where.DynamicPropertyOf<Application>("Rejected").Eq(false);

            IDictionary<string, object> a25 = null;
            foreach (var row in Rows(q))
                if ((string)row["applicant"] == Applicant(25))
                    a25 = row;

            a25.Should().NotBeNull();
            ((string)a25["occ"]).Should().Be(Occupation(25));     // Engineer
            ((double)a25["income"]).Should().Be(Income(25));      // 70000
            ((bool)a25["employed"]).Should().Be(Employed(25));    // true
            Convert.ToInt32(a25["kids"]).Should().Be(Children(25)); // 0
        }

        // ======================= E. Individual review & bulk load =======================

        [Fact]
        public void S24_FullApplicantProfile()
        {
            using var c = Fresh();
            var a = Load(c, Applicant(7), preload: false);
            a.DynamicProperties.Should().BeNull();  // not loaded yet

            c.LoadPropertiesFor(a);
            var bag = a.DynamicProperties;
            bag.Should().NotBeNull();
            bag.Get<string>("Occupation").Should().Be(Occupation(7));   // Doctor
            bag.Get<DateTime>("DoB").Should().Be(DoB(7));
            bag.Get<bool>("Employed").Should().Be(Employed(7));         // true
            bag.Get<bool>("Rejected").Should().BeFalse();
            bag.Get<int>("ChildrenCount").Should().Be(Children(7));     // 2
            bag.Get<double>("Income").Should().Be(Income(7));           // 34000
        }

        [Fact]
        public void S25_PreloadWholeQueue_AndScore()
        {
            using var c = Fresh();

            double expected = 0;
            for (int i = 0; i < 50; i++)
                if (Employed(i))
                    expected += Income(i);

            double sum = 0;
            using (var q = c.GetSelectEntitiesQuery<Application>())
            {
                q.PreloadProperties = true;
                foreach (var a in q.ReadAll<Application>())
                    if (a.DynamicProperties.Get<bool>("Employed"))
                        sum += a.DynamicProperties.Get<double>("Income");
            }

            sum.Should().Be(expected);
        }

        // ======================= Paginated viewer =======================

        [Fact]
        public void S26_PaginatedViewer_FilterSortPage()
        {
            using var c = Fresh();
            const int pageSize = 10;

            // total for "page N of M" - same filter as the pages
            int total = Count(c, w => w.DynamicPropertyOf<Application>("Employed").Eq(true));
            total.Should().Be(33);

            // expected: employed applicants, income descending (income grows with i, so i descending)
            var expectedIncomes = new List<double>();
            for (int i = 49; i >= 0; i--)
                if (Employed(i))
                    expectedIncomes.Add(Income(i));

            var pageSizes = new List<int>();
            var seenApplicants = new HashSet<string>();
            var gotIncomes = new List<double>();

            for (int page = 0; ; page++)
            {
                using var q = c.GetSelectEntitiesQueryBase<Application>();
                q.AddToResultset(typeof(Application), nameof(Application.Applicant), "applicant");
                q.AddDynamicPropertyToResultset<Application>("Occupation", Vt.String, "occ");
                q.AddDynamicPropertyToResultset<Application>("Income", Vt.Real, "income");
                q.AddDynamicPropertyToResultset<Application>("ChildrenCount", Vt.Integer, "kids");
                q.Where.DynamicPropertyOf<Application>("Employed").Eq(true);
                q.AddDynamicPropertyToOrderBy<Application>("Income", Vt.Real, SortDir.Desc);
                q.AddOrderBy(typeof(Application), nameof(Application.Id), SortDir.Asc);  // deterministic tie-break
                q.Skip = page * pageSize;
                q.Limit = pageSize;

                var rows = Rows(q);
                if (rows.Count == 0)
                    break;

                pageSizes.Add(rows.Count);
                foreach (var row in rows)
                {
                    seenApplicants.Add((string)row["applicant"]).Should().BeTrue("pages must not overlap");
                    gotIncomes.Add((double)row["income"]);
                }
            }

            pageSizes.Should().Equal(10, 10, 10, 3);         // 33 across four pages
            seenApplicants.Count.Should().Be(total);          // pages reassemble the full filtered set
            gotIncomes.Should().Equal(expectedIncomes);       // global order preserved across page boundaries
        }
    }
}
