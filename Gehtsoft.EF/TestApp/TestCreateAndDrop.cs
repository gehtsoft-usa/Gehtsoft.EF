using System;
using System.Data;
using System.IO;
using System.Text;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using MongoDB.Driver;
using NUnit.Framework;

namespace TestApp
{
    static class TestCreateAndDrop
    {
        static TableDescriptor gCreateDropTable = new TableDescriptor
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

        static TableDescriptor gCreateDropTable1 = new TableDescriptor
        (
            "createdroptest1",
            new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo {Name = "vint_pk", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true},
                new TableDescriptor.ColumnInfo {Name = "vstring", DbType = DbType.String, Size = 32, Nullable = true},
            }
        );

        class entity
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

        private static object lobMutex = new object();
        private static byte[] gBlob = null;
        private static string gClob = null;

        private const int lobsize = 64 * 1024 - 1;

        public static void Do(SqlDbConnection connection)
        {
            lock (lobMutex)
            {
                Random r = new Random((int) DateTime.Now.Ticks % 65536);
                if (gBlob == null)
                    gBlob = new byte[lobsize];
                for (int i = 0; i < gBlob.Length; i++)
                    gBlob[i] = (byte) r.Next(256);

                StringBuilder b = new StringBuilder();
                for (int i = 0; i < lobsize; i++)
                    b.Append((char) ('A' + r.Next(27)));
                gClob = b.ToString();
            }


            DropTableBuilder dbuilder = connection.GetDropTableBuilder(gCreateDropTable);
            CreateTableBuilder cbuilder = connection.GetCreateTableBuilder(gCreateDropTable);
            InsertQueryBuilder ibuilder = connection.GetInsertQueryBuilder(gCreateDropTable);
            DropTableBuilder dbuilder1 = connection.GetDropTableBuilder(gCreateDropTable1);
            CreateTableBuilder cbuilder1 = connection.GetCreateTableBuilder(gCreateDropTable1);

            SqlDbQuery query;

            using (query = connection.GetQuery(dbuilder))
                query.ExecuteNoData();
            using (query = connection.GetQuery(dbuilder1))
                query.ExecuteNoData();

            TableDescriptor[] schema = connection.Schema();

            Assert.NotNull(schema);
            Assert.IsFalse(schema.Contains("createdroptest"));
            Assert.IsFalse(schema.Contains("createdroptest1"));

            using (query = connection.GetQuery(cbuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(cbuilder1))
                query.ExecuteNoData();

            schema = connection.Schema();


            Assert.NotNull(schema);
            Assert.IsTrue(schema.Contains("createdroptest"));
            Assert.IsTrue(schema.Contains("createdroptest", "vint_pk"));
            Assert.IsTrue(schema.Contains("createdroptest", "vblob"));
            Assert.IsTrue(schema.Contains("createdroptest", "vbool"));
            Assert.IsTrue(schema.Contains("createdroptest1"));
            Assert.IsTrue(schema.Contains("createdroptest1", "vint_pk"));
            Assert.IsFalse(schema.Contains("createdroptest2"));

            DateTime dt1 = DateTime.Now;
            dt1 = new DateTime(dt1.Year, dt1.Month, dt1.Day, dt1.Hour, dt1.Minute, dt1.Second);
            DateTime dt2 = new DateTime(2006, 12, 8);

            UpdateQueryToTypeBinder ubinder = new UpdateQueryToTypeBinder(typeof(entity));
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

                Assert.AreEqual(1, id);

                entity e1 = new entity()
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

                Assert.AreEqual(2, e1.vint_pk);

                e1 = new entity()
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

                Assert.AreEqual(3, e1.vint_pk);
            }

            SelectQueryBuilder sbuilder = connection.GetSelectQueryBuilder(gCreateDropTable);
            sbuilder.AddOrderBy(gCreateDropTable["vint_pk"]);
            SelectQueryTypeBinder binder = new SelectQueryTypeBinder(typeof(entity));
            binder.AutoBind(null);

            entity e;
            using (query = connection.GetQuery(sbuilder))
            {
                query.ExecuteReader();
                Assert.IsTrue(query.ReadNext());
                e = binder.Read<entity>(query);

                Assert.AreEqual(1, e.vint_pk);
                Assert.AreEqual("string1", e.vstring);
                Assert.IsNull(e.vclob);
                Assert.IsNull(e.vblob);
                Assert.AreEqual(456, e.vint);
                Assert.AreEqual(123.45, e.vreal);
                Assert.AreEqual(true, e.vbool);
                Assert.AreEqual(dt1, e.vdate);

                Assert.IsTrue(query.ReadNext());
                Assert.AreEqual(2, query.GetValue<int>("vint_pk"));
                Assert.AreEqual("string2", query.GetValue<string>("vstring"));
                Assert.AreEqual(gClob, query.GetValue<string>("vclob"));
                Assert.AreEqual(gBlob, query.GetValue<byte[]>("vblob"));
                Assert.AreEqual(123, query.GetValue<int>("vint"));
                Assert.AreEqual(789.12, query.GetValue<double>("vreal"));
                Assert.AreEqual(false, query.GetValue<bool>("vbool"));
                Assert.AreEqual(dt2, query.GetValue<DateTime>("vdate"));

                Assert.AreEqual(123, query.GetValue<int?>("vint"));
                Assert.AreEqual(789.12, query.GetValue<double?>("vreal"));
                Assert.AreEqual(false, query.GetValue<bool?>("vbool"));
                Assert.AreEqual(dt2, query.GetValue<DateTime?>("vdate"));

                Assert.IsTrue(query.ReadNext());
                Assert.IsTrue(query.IsNull("vstring"));
                Assert.IsTrue(query.IsNull("vclob"));
                Assert.IsTrue(query.IsNull("vblob"));
                Assert.IsTrue(query.IsNull("vint"));
                Assert.IsTrue(query.IsNull("vreal"));
                Assert.IsTrue(query.IsNull("vbool"));
                Assert.IsTrue(query.IsNull("vdate"));

                Assert.AreEqual(null, query.GetValue<string>("vstring"));
                Assert.AreEqual(null, query.GetValue<string>("vclob"));
                Assert.AreEqual(null, query.GetValue<byte[]>("vblob"));
                Assert.AreEqual(null, query.GetValue<int?>("vint"));
                Assert.AreEqual(null, query.GetValue<double?>("vreal"));
                Assert.AreEqual(null, query.GetValue<bool?>("vbool"));
                Assert.AreEqual(null, query.GetValue<DateTime?>("vdate"));
                Assert.AreEqual(0, query.GetValue<int>("vint"));
                Assert.AreEqual(0, query.GetValue<double>("vreal"));
                Assert.AreEqual(false, query.GetValue<bool>("vbool"));
                Assert.AreEqual(new DateTime(0), query.GetValue<DateTime>("vdate"));
            }

            bool oracle = connection.ConnectionType == "oracle";
            SelectQueryBuilder insert1Select = new SelectQueryBuilder(connection.GetLanguageSpecifics(), gCreateDropTable);
            if (oracle)
                insert1Select.AddToResultset(gCreateDropTable["vint_pk"]);
            insert1Select.AddToResultset(gCreateDropTable["vstring"]);
            insert1Select.Where.Property(gCreateDropTable["vint_pk"]).Eq().Value(2);
            InsertSelectQueryBuilder insert1FromSelect = new InsertSelectQueryBuilder(connection.GetLanguageSpecifics(), gCreateDropTable1, insert1Select, oracle ? false : true);
            using (query = connection.GetQuery(insert1FromSelect))
                query.ExecuteNoData();

            using (query = connection.GetQuery($"select * from {gCreateDropTable1.Name}"))
            {
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>(0).Should().Be(oracle ? 2 : 1);
                query.GetValue<string>(1).Should().Be("string2");
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
                query.ReadBlobAsStream = true;
                query.ExecuteReader();
                Assert.IsTrue(query.ReadNext());
                using (Stream s = query.GetStream(0))
                {
                    MemoryStream copy = new MemoryStream();
                    s.CopyTo(copy);
                    Assert.AreEqual(copy.Length, gBlob.Length);
                    Assert.AreEqual(copy.ToArray(), gBlob);
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
                Assert.IsTrue(query.ReadNext());
                Assert.AreEqual(1, query.GetValue<int>("vint_pk"));
                Assert.AreEqual("newstring1", query.GetValue<string>("vstring"));
                Assert.IsNull(query.GetValue<string>("vclob"));
                Assert.IsNull(query.GetValue<byte[]>("vblob"));
                Assert.AreEqual(123.45, query.GetValue<double>("vreal"));
                Assert.AreEqual(true, query.GetValue<bool>("vbool"));
                Assert.AreEqual(dt1, query.GetValue<DateTime>("vdate"));
                Assert.IsFalse(query.ReadNext());
            }

                
            

        }
    }
}

