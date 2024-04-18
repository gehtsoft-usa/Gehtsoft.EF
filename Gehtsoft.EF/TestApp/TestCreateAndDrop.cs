using System;
using System.Data;
using System.IO;
using System.Text;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using NUnit.Framework;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using NUnit.Framework.Legacy;

namespace TestApp
{
    internal static class TestCreateAndDrop
    {
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

#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable S1144   // Unused set
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

        public static void Do(SqlDbConnection connection)
        {
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

            ClassicAssert.NotNull(schema);
            ClassicAssert.IsFalse(schema.Contains("createdroptest"));
            ClassicAssert.IsFalse(schema.Contains("createdroptest1"));

            using (query = connection.GetQuery(cbuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(cbuilder1))
                query.ExecuteNoData();

            using (query = connection.GetQuery(idxbuilder))
                query.ExecuteNoData();

            schema = connection.Schema();

            ClassicAssert.NotNull(schema);
            ClassicAssert.IsTrue(schema.Contains("createdroptest"));
            ClassicAssert.IsTrue(schema.Contains("createdroptest", "vint_pk"));
            ClassicAssert.IsTrue(schema.Contains("createdroptest", "vblob"));
            ClassicAssert.IsTrue(schema.Contains("createdroptest", "vbool"));
            ClassicAssert.IsTrue(schema.Contains("createdroptest1"));
            ClassicAssert.IsTrue(schema.Contains("createdroptest1", "vint_pk"));
            ClassicAssert.IsFalse(schema.Contains("createdroptest2"));

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

                ClassicAssert.AreEqual(1, id);

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

                ClassicAssert.AreEqual(2, e1.vint_pk);

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

                ClassicAssert.AreEqual(3, e1.vint_pk);
            }

            SelectQueryBuilder sbuilder = connection.GetSelectQueryBuilder(gCreateDropTable);
            sbuilder.AddOrderBy(gCreateDropTable["vint_pk"]);
            SelectQueryResultBinder binder = new SelectQueryResultBinder(typeof(TestEntity));
            binder.AutoBindType();

            TestEntity e;
            using (query = connection.GetQuery(sbuilder))
            {
                query.ExecuteReader();
                ClassicAssert.IsTrue(query.ReadNext());
                e = binder.Read<TestEntity>(query);

                ClassicAssert.AreEqual(1, e.vint_pk);
                ClassicAssert.AreEqual("string1", e.vstring);
                ClassicAssert.IsNull(e.vclob);
                ClassicAssert.IsNull(e.vblob);
                ClassicAssert.AreEqual(456, e.vint);
                ClassicAssert.AreEqual(123.45, e.vreal);
                ClassicAssert.AreEqual(true, e.vbool);
                ClassicAssert.AreEqual(dt1, e.vdate);

                ClassicAssert.IsTrue(query.ReadNext());
                ClassicAssert.AreEqual(2, query.GetValue<int>("vint_pk"));
                ClassicAssert.AreEqual("string2", query.GetValue<string>("vstring"));
                ClassicAssert.AreEqual(gClob, query.GetValue<string>("vclob"));
                ClassicAssert.AreEqual(gBlob, query.GetValue<byte[]>("vblob"));
                ClassicAssert.AreEqual(123, query.GetValue<int>("vint"));
                ClassicAssert.AreEqual(789.12, query.GetValue<double>("vreal"));
                ClassicAssert.AreEqual(false, query.GetValue<bool>("vbool"));
                ClassicAssert.AreEqual(dt2, query.GetValue<DateTime>("vdate"));

                ClassicAssert.AreEqual(123, query.GetValue<int?>("vint"));
                ClassicAssert.AreEqual(789.12, query.GetValue<double?>("vreal"));
                ClassicAssert.AreEqual(false, query.GetValue<bool?>("vbool"));
                ClassicAssert.AreEqual(dt2, query.GetValue<DateTime?>("vdate"));

                ClassicAssert.IsTrue(query.ReadNext());
                ClassicAssert.IsTrue(query.IsNull("vstring"));
                ClassicAssert.IsTrue(query.IsNull("vclob"));
                ClassicAssert.IsTrue(query.IsNull("vblob"));
                ClassicAssert.IsTrue(query.IsNull("vint"));
                ClassicAssert.IsTrue(query.IsNull("vreal"));
                ClassicAssert.IsTrue(query.IsNull("vbool"));
                ClassicAssert.IsTrue(query.IsNull("vdate"));

                ClassicAssert.AreEqual(null, query.GetValue<string>("vstring"));
                ClassicAssert.AreEqual(null, query.GetValue<string>("vclob"));
                ClassicAssert.AreEqual(null, query.GetValue<byte[]>("vblob"));
                ClassicAssert.AreEqual(null, query.GetValue<int?>("vint"));
                ClassicAssert.AreEqual(null, query.GetValue<double?>("vreal"));
                ClassicAssert.AreEqual(null, query.GetValue<bool?>("vbool"));
                ClassicAssert.AreEqual(null, query.GetValue<DateTime?>("vdate"));
                ClassicAssert.AreEqual(0, query.GetValue<int>("vint"));
                ClassicAssert.AreEqual(0, query.GetValue<double>("vreal"));
                ClassicAssert.AreEqual(false, query.GetValue<bool>("vbool"));
                ClassicAssert.AreEqual(new DateTime(0, DateTimeKind.Unspecified), query.GetValue<DateTime>("vdate"));
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
                ClassicAssert.IsTrue(query.ReadNext());
                var arr = query.GetValue<byte[]>(0);
                ClassicAssert.AreEqual(arr.Length, gBlob.Length);
                ClassicAssert.AreEqual(arr, gBlob);
            }

            using (query = connection.GetQuery())
            {
                query.CommandText = "select vblob from createdroptest where vint_pk = 2";
                query.ReadBlobAsStream = true;
                query.ExecuteReader();
                ClassicAssert.IsTrue(query.ReadNext());
                using (Stream s = query.GetStream(0))
                {
                    using (MemoryStream copy = new MemoryStream())
                    {
                        s.CopyTo(copy);
                        copy.Flush();

                        var arr = copy.ToArray();
                        ClassicAssert.AreEqual(arr.Length, gBlob.Length);
                        ClassicAssert.AreEqual(arr, gBlob);
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
                ClassicAssert.IsTrue(query.ReadNext());
                ClassicAssert.AreEqual(1, query.GetValue<int>("vint_pk"));
                ClassicAssert.AreEqual("newstring1", query.GetValue<string>("vstring"));
                ClassicAssert.IsNull(query.GetValue<string>("vclob"));
                ClassicAssert.IsNull(query.GetValue<byte[]>("vblob"));
                ClassicAssert.AreEqual(123.45, query.GetValue<double>("vreal"));
                ClassicAssert.AreEqual(true, query.GetValue<bool>("vbool"));
                ClassicAssert.AreEqual(dt1, query.GetValue<DateTime>("vdate"));
                ClassicAssert.IsFalse(query.ReadNext());
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

