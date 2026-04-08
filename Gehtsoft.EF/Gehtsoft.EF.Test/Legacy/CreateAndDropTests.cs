using System;
using System.Data;
using System.IO;
using System.Text;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class CreateAndDropTests : IClassFixture<CreateAndDropTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public CreateAndDropTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        private static readonly TableDescriptor gCreateDropTable = new TableDescriptor
        (
            "createdroptest",
            new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo {Name = "vint_pk", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true},
                new TableDescriptor.ColumnInfo {Name = "vstring", DbType = DbType.String, Size = 32, Nullable = true},
                new TableDescriptor.ColumnInfo {Name = "vclob", DbType = DbType.String, Nullable = true},
                new TableDescriptor.ColumnInfo {Name = "vblob", DbType = DbType.Binary, Nullable = true},
                new TableDescriptor.ColumnInfo {Name = "vint", DbType = DbType.Int32, Nullable = true},
                new TableDescriptor.ColumnInfo {Name = "vreal", DbType = DbType.Double, Size = 16, Precision = 2, Nullable = true},
                new TableDescriptor.ColumnInfo {Name = "vdate", DbType = DbType.DateTime, Sorted = true, Nullable = true},
                new TableDescriptor.ColumnInfo {Name = "vbool", DbType = DbType.Boolean, Nullable = true},
            }
        );

        private static readonly TableDescriptor gCreateDropTable1 = new TableDescriptor
        (
            "createdroptest1",
            new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo {Name = "vint_pk", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true},
                new TableDescriptor.ColumnInfo {Name = "vstring", DbType = DbType.String, Size = 32, Nullable = true},
            }
        );

#pragma warning disable IDE1006
#pragma warning disable S1144
        private class TestEntity
        {
            public int vint_pk { get; set; }
            public string vstring { get; set; }
            public string vclob { get; set; }
            public double? vreal { get; set; }
            public byte[] vblob { get; set; }
            public DateTime? vdate { get; set; }
            public bool? vbool { get; set; }
            public int? vint { get; set; }
        }
#pragma warning restore IDE1006, S1144

        private static readonly object lobMutex = new object();
        private static byte[] gBlob = null;
        private static string gClob = null;

        private const int lobsize = 64 * 1024 - 1;

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateDropAndCrud(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            lock (lobMutex)
            {
                Random r = new Random((int)DateTime.Now.Ticks % 65536);
                if (gBlob == null)
                    gBlob = new byte[lobsize];
                for (int i = 0; i < gBlob.Length; i++)
                    gBlob[i] = (byte)r.Next(256);

                StringBuilder b = new StringBuilder();
                for (int i = 0; i < lobsize; i++)
                    b.Append((char)('A' + r.Next(27)));
                gClob = b.ToString();
            }

            var viewDropBuilder = connection.GetDropViewBuilder("createdroptest_view");
            var dbuilder = connection.GetDropTableBuilder(gCreateDropTable);
            var cbuilder = connection.GetCreateTableBuilder(gCreateDropTable);
            var idxbuilder = connection.GetCreateIndexBuilder(gCreateDropTable1, new CompositeIndex("custom") { "vstring" });
            var ibuilder = connection.GetInsertQueryBuilder(gCreateDropTable);
            var didxbuilder = connection.GetDropIndexBuilder(gCreateDropTable1, "custom");
            var dbuilder1 = connection.GetDropTableBuilder(gCreateDropTable1);
            var cbuilder1 = connection.GetCreateTableBuilder(gCreateDropTable1);

            SqlDbQuery query;

            if (connection.DoesObjectExist(gCreateDropTable1.Name, null, "table"))
            {
                using (query = connection.GetQuery(didxbuilder))
                    query.ExecuteNoData();
            }
            using (query = connection.GetQuery(viewDropBuilder))
                query.ExecuteNoData();
            using (query = connection.GetQuery(dbuilder))
                query.ExecuteNoData();
            using (query = connection.GetQuery(dbuilder1))
                query.ExecuteNoData();

            TableDescriptor[] schema = connection.Schema();

            schema.Should().NotBeNull();
            schema.Contains("createdroptest").Should().BeFalse();
            schema.Contains("createdroptest1").Should().BeFalse();

            using (query = connection.GetQuery(cbuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(cbuilder1))
                query.ExecuteNoData();

            using (query = connection.GetQuery(idxbuilder))
                query.ExecuteNoData();

            schema = connection.Schema();

            schema.Should().NotBeNull();
            schema.Contains("createdroptest").Should().BeTrue();
            schema.Contains("createdroptest", "vint_pk").Should().BeTrue();
            schema.Contains("createdroptest", "vblob").Should().BeTrue();
            schema.Contains("createdroptest", "vbool").Should().BeTrue();
            schema.Contains("createdroptest1").Should().BeTrue();
            schema.Contains("createdroptest1", "vint_pk").Should().BeTrue();
            schema.Contains("createdroptest2").Should().BeFalse();

            DateTime dt1 = DateTime.Now;
            dt1 = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt1.Hour, dt1.Minute, dt1.Second, DateTimeKind.Unspecified);
            DateTime dt2 = new DateTime(2006, 12, 8, 0, 0, 0, DateTimeKind.Unspecified);

            UpdateQueryToTypeBinder ubinder = new UpdateQueryToTypeBinder(typeof(TestEntity));
            ubinder.AutoBind(gCreateDropTable);

            using (query = connection.GetQuery(ibuilder))
            {
                query.BindParam("vstring", "string1");
                query.BindNull("vclob", DbType.String);
                query.BindNull("vblob", DbType.Binary);
                query.BindParam("vint", 456);
                query.BindParam("vreal", 123.45);
                query.BindParam("vbool", true);
                query.BindParam("vdate", dt1);

                int id = -1;
                if (query.LanguageSpecifics.AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.FirstResultset)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        id = query.GetValue<int>(0);
                }
                else
                {
                    query.BindParam("vint_pk", DbType.Int32, ParameterDirection.Output);
                    query.ExecuteNoData();
                    id = query.GetParamValue<int>("vint_pk");
                }

                id.Should().Be(1);

                TestEntity e1 = new TestEntity()
                {
                    vstring = "string2",
                    vclob = gClob,
                    vblob = gBlob,
                    vreal = 789.12,
                    vbool = false,
                    vdate = dt2,
                    vint = 123,
                };

                ubinder.BindAndExecute(query, e1);

                e1.vint_pk.Should().Be(2);

                e1 = new TestEntity()
                {
                    vstring = null,
                    vclob = null,
                    vblob = null,
                    vreal = null,
                    vbool = null,
                    vdate = null,
                    vint = null,
                };

                ubinder.BindAndExecute(query, e1);

                e1.vint_pk.Should().Be(3);
            }

            SelectQueryBuilder sbuilder = connection.GetSelectQueryBuilder(gCreateDropTable);
            sbuilder.AddOrderBy(gCreateDropTable["vint_pk"]);
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(TestEntity));
            binder.AutoBindType();

            TestEntity e;
            using (query = connection.GetQuery(sbuilder))
            {
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                e = binder.Read<TestEntity>(query);

                e.vint_pk.Should().Be(1);
                e.vstring.Should().Be("string1");
                e.vclob.Should().BeNull();
                e.vblob.Should().BeNull();
                e.vint.Should().Be(456);
                e.vreal.Should().Be(123.45);
                e.vbool.Should().Be(true);
                e.vdate.Should().Be(dt1);

                query.ReadNext().Should().BeTrue();
                query.GetValue<int>("vint_pk").Should().Be(2);
                query.GetValue<string>("vstring").Should().Be("string2");
                query.GetValue<string>("vclob").Should().Be(gClob);
                Assert.Equal(gBlob, query.GetValue<byte[]>("vblob"));
                query.GetValue<int>("vint").Should().Be(123);
                query.GetValue<double>("vreal").Should().Be(789.12);
                query.GetValue<bool>("vbool").Should().Be(false);
                query.GetValue<DateTime>("vdate").Should().Be(dt2);

                query.GetValue<int?>("vint").Should().Be(123);
                query.GetValue<double?>("vreal").Should().Be(789.12);
                query.GetValue<bool?>("vbool").Should().Be(false);
                query.GetValue<DateTime?>("vdate").Should().Be(dt2);

                query.ReadNext().Should().BeTrue();
                query.IsNull("vstring").Should().BeTrue();
                query.IsNull("vclob").Should().BeTrue();
                query.IsNull("vblob").Should().BeTrue();
                query.IsNull("vint").Should().BeTrue();
                query.IsNull("vreal").Should().BeTrue();
                query.IsNull("vbool").Should().BeTrue();
                query.IsNull("vdate").Should().BeTrue();

                query.GetValue<string>("vstring").Should().BeNull();
                query.GetValue<string>("vclob").Should().BeNull();
                query.GetValue<byte[]>("vblob").Should().BeNull();
                query.GetValue<int?>("vint").Should().BeNull();
                query.GetValue<double?>("vreal").Should().BeNull();
                query.GetValue<bool?>("vbool").Should().BeNull();
                query.GetValue<DateTime?>("vdate").Should().BeNull();
                query.GetValue<int>("vint").Should().Be(0);
                query.GetValue<double>("vreal").Should().Be(0);
                query.GetValue<bool>("vbool").Should().Be(false);
                query.GetValue<DateTime>("vdate").Should().Be(new DateTime(0, DateTimeKind.Unspecified));
            }

            SelectQueryBuilder insert1Select = connection.GetSelectQueryBuilder(gCreateDropTable);
            insert1Select.AddToResultset(gCreateDropTable["vstring"]);
            insert1Select.Where.Property(gCreateDropTable["vint_pk"]).Eq().Value(2);
            InsertSelectQueryBuilder insert1FromSelect = connection.GetInsertSelectQueryBuilder(gCreateDropTable1, insert1Select, false);
            using (query = connection.GetQuery(insert1FromSelect))
            {
                if (query.LanguageSpecifics.AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                    query.BindOutput("vint_pk", DbType.Int32);
                query.ExecuteNoData();
            }

            using (query = connection.GetQuery($"select * from {gCreateDropTable1.Name}"))
            {
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>(0).Should().Be(1);
                query.GetValue<string>(1).Should().Be("string2");
                query.ReadNext().Should().BeFalse();
            }

            var updateBuilder = connection.GetUpdateQueryBuilder(gCreateDropTable1);
            updateBuilder.AddUpdateColumnExpression(gCreateDropTable1["vstring"], connection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat, new string[] { "vstring", "@p" }));
            using (query = connection.GetQuery(updateBuilder))
            {
                query.BindParam("p", "x");
                query.ExecuteNoData().Should().Be(1);
            }

            using (query = connection.GetQuery($"select * from {gCreateDropTable1.Name}"))
            {
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>(0).Should().Be(1);
                query.GetValue<string>(1).Should().Be("string2x");
                query.ReadNext().Should().BeFalse();
            }

            UpdateQueryBuilder ubuilder = connection.GetUpdateQueryBuilder(gCreateDropTable);
            ubuilder.AddWhereFilterPrimaryKey();
            ubuilder.AddUpdateColumn(gCreateDropTable["vstring"]);

            using (query = connection.GetQuery(ubuilder))
            {
                query.BindParam("vint_pk", 1);
                query.BindParam("vstring", "newstring1");
                query.ExecuteNoData();
            }

            using (query = connection.GetQuery())
            {
                query.CommandText = "select vblob from createdroptest where vint_pk = 2";
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                var arr = query.GetValue<byte[]>(0);
                arr.Length.Should().Be(gBlob.Length);
                Assert.Equal(gBlob, arr);
            }

            using (query = connection.GetQuery())
            {
                query.CommandText = "select vblob from createdroptest where vint_pk = 2";
                query.ReadBlobAsStream = true;
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                using (Stream s = query.GetStream(0))
                {
                    using (MemoryStream copy = new MemoryStream())
                    {
                        s.CopyTo(copy);
                        copy.Flush();

                        var arr = copy.ToArray();
                        arr.Length.Should().Be(gBlob.Length);
                        Assert.Equal(gBlob, arr);
                    }
                }
            }

            DeleteQueryBuilder delbuilder = connection.GetDeleteQueryBuilder(gCreateDropTable);
            delbuilder.AddWhereFilterPrimaryKey();

            using (query = connection.GetQuery(delbuilder))
            {
                query.BindParam("vint_pk", 2);
                query.ExecuteNoData();
                query.BindParam("vint_pk", 3);
                query.ExecuteNoData();
            }

            using (query = connection.GetQuery())
            {
                query.CommandText = "select * from createdroptest order by vint_pk";
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>("vint_pk").Should().Be(1);
                query.GetValue<string>("vstring").Should().Be("newstring1");
                query.GetValue<string>("vclob").Should().BeNull();
                query.GetValue<byte[]>("vblob").Should().BeNull();
                query.GetValue<double>("vreal").Should().Be(123.45);
                query.GetValue<bool>("vbool").Should().Be(true);
                query.GetValue<DateTime>("vdate").Should().Be(dt1);
                query.ReadNext().Should().BeFalse();
            }

            var viewSelectBuilder = connection.GetSelectQueryBuilder(gCreateDropTable);
            viewSelectBuilder.AddToResultset(gCreateDropTable["vint_pk"], "pk");
            viewSelectBuilder.AddToResultset(gCreateDropTable["vstring"], "note");

            var viewCreateBuilder = connection.GetCreateViewBuilder("createdroptest_view", viewSelectBuilder);

            using (query = connection.GetQuery(viewDropBuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(viewCreateBuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery("select * from createdroptest_view"))
            {
                query.ExecuteReader();
                query.FieldCount.Should().Be(2);
                query.Field(0).Name.Should().Match(v => v.Equals("pk", StringComparison.OrdinalIgnoreCase));
                query.Field(1).Name.Should().Match(v => v.Equals("note", StringComparison.OrdinalIgnoreCase));
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>(0).Should().Be(1);
                query.GetValue<string>(1).Should().Be("newstring1");
            }
        }
    }
}
