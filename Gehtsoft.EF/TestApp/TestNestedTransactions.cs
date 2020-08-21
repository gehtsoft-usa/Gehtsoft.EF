using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using NUnit.Framework;

namespace TestApp
{
    public class NestedTransactionsTest
    {
        static TableDescriptor gTable = new TableDescriptor
        (
            "transactiontest",
            new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo { Name = "vint_pk", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true},
                new TableDescriptor.ColumnInfo { Name = "vstring", DbType = DbType.String, Size = 32},
            }
        );


        public static void Do(SqlDbConnection connection)
        {
            Assert.AreEqual(SqlDbLanguageSpecifics.TransactionSupport.Nested, connection.GetLanguageSpecifics().SupportsTransactions);

            DropTableBuilder dbuilder = connection.GetDropTableBuilder(gTable);
            CreateTableBuilder cbuilder = connection.GetCreateTableBuilder(gTable);
            InsertQueryBuilder ibuilder = connection.GetInsertQueryBuilder(gTable);

            SqlDbQuery query;

            using (query = connection.GetQuery(dbuilder))
                query.ExecuteNoData();
            TableDescriptor[] schema = connection.Schema();
            Assert.NotNull(schema);
            Assert.IsFalse(schema.Contains(gTable.Name));
            using (query = connection.GetQuery(cbuilder))
                query.ExecuteNoData();

            using (SqlDbTransaction t1 = connection.BeginTransaction())
            {
                using (query = connection.GetQuery(ibuilder))
                {
                    query.BindParam("vstring", "s1");
                    query.ExecuteNoData();
                }

                using (SqlDbTransaction t2 = connection.BeginTransaction())
                {
                    using (query = connection.GetQuery(ibuilder))
                    {
                        query.BindParam("vstring", "s2");
                        query.ExecuteNoData();
                    }
                    t2.Commit();
                }

                using (SqlDbTransaction t3 = connection.BeginTransaction())
                {
                    using (query = connection.GetQuery(ibuilder))
                    {
                        query.BindParam("vstring", "s3");
                        query.ExecuteNoData();
                    }
                    t3.Rollback();
                }

                using (query = connection.GetQuery(ibuilder))
                {
                    query.BindParam("vstring", "s4");
                    query.ExecuteNoData();
                }
                t1.Commit();
            }

            SelectQueryBuilder sbuilder = new SelectQueryBuilder(connection.GetLanguageSpecifics(), gTable);
            sbuilder.AddToResultset(AggFn.Count);
            sbuilder.Where.Property(gTable["vstring"]).Is(CmpOp.Eq).Parameter("vstring");

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s1");
                query.ExecuteReader();
                query.ReadNext();
                Assert.AreEqual(1, query.GetValue<int>(0));
            }

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s2");
                query.ExecuteReader();
                query.ReadNext();
                Assert.AreEqual(1, query.GetValue<int>(0));
            }

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s3");
                query.ExecuteReader();
                query.ReadNext();
                Assert.AreEqual(0, query.GetValue<int>(0));
            }

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s4");
                query.ExecuteReader();
                query.ReadNext();
                Assert.AreEqual(1, query.GetValue<int>(0));
            }

        }

    }
}
