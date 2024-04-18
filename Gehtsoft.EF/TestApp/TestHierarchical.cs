using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace TestApp
{
    internal static class TestHierarchical
    {
        private static readonly TableDescriptor gHierarchicalTable = new TableDescriptor
            (
                "hierarchicaltest",
                new TableDescriptor.ColumnInfo[]
                {
                    new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true},
                    new TableDescriptor.ColumnInfo { Name = "parent", DbType = DbType.Int32, Sorted = true, Nullable = true},
                    new TableDescriptor.ColumnInfo { Name = "data", DbType = DbType.Int32, Sorted = true},
                }
            );

        public static void Do(SqlDbConnection connection)
        {
            DropTableBuilder dbuilder = connection.GetDropTableBuilder(gHierarchicalTable);
            CreateTableBuilder cbuilder = connection.GetCreateTableBuilder(gHierarchicalTable);
            InsertQueryBuilder ibuilder = connection.GetInsertQueryBuilder(gHierarchicalTable);

            SqlDbQuery query;

            using (query = connection.GetQuery(dbuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(cbuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(ibuilder))
            {
                //create tree
                // 1
                // + 2
                //   + 4
                //   + 5
                // + 3
                //   + 6
                //     + 8
                //   + 7
                // + 9
                query.BindParam("id", 1);
                query.BindNull("parent", DbType.Int32);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 2);
                query.BindParam("parent", 1);
                query.BindParam("data", 0);
                query.ExecuteNoData();
                query.BindParam("id", 3);
                query.BindParam("parent", 1);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 4);
                query.BindParam("parent", 2);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 5);
                query.BindParam("parent", 2);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 6);
                query.BindParam("parent", 3);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 7);
                query.BindParam("parent", 3);
                query.BindParam("data", 1);
                query.ExecuteNoData();

                query.BindParam("id", 8);
                query.BindParam("parent", 6);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 9);
                query.BindParam("parent", 1);
                query.BindParam("data", 0);
                query.ExecuteNoData();
            }

            //read whole tree
            HierarchicalSelectQueryBuilder hbuilder = connection.GetHierarchicalSelectQueryBuilder(gHierarchicalTable, gHierarchicalTable["parent"], null);
            using (query = connection.GetQuery(hbuilder))
            {
                query.ExecuteReader();
                int rc = 0;
                while (query.ReadNext())
                {
                    rc++;
                    switch (query.GetValue<int>("id"))
                    {
                        case 1:
                            ClassicAssert.AreEqual(1, query.GetValue<int>("level"));
                            ClassicAssert.IsTrue(query.IsNull("parent"));
                            break;
                        case 2:
                            ClassicAssert.AreEqual(2, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(1, query.GetValue<int>("parent"));
                            break;
                        case 3:
                            ClassicAssert.AreEqual(2, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(1, query.GetValue<int>("parent"));
                            break;
                        case 4:
                            ClassicAssert.AreEqual(3, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(2, query.GetValue<int>("parent"));
                            break;
                        case 5:
                            ClassicAssert.AreEqual(3, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(2, query.GetValue<int>("parent"));
                            break;
                        case 6:
                            ClassicAssert.AreEqual(3, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(3, query.GetValue<int>("parent"));
                            break;
                        case 7:
                            ClassicAssert.AreEqual(3, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(3, query.GetValue<int>("parent"));
                            break;
                        case 8:
                            ClassicAssert.AreEqual(4, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(6, query.GetValue<int>("parent"));
                            break;
                        case 9:
                            ClassicAssert.AreEqual(2, query.GetValue<int>("level"));
                            ClassicAssert.AreEqual(1, query.GetValue<int>("parent"));
                            break;
                        default:
                            ClassicAssert.Fail("Unknown ID");
                            break;
                    }
                }
                ClassicAssert.AreEqual(9, rc);
            }

            hbuilder = connection.GetHierarchicalSelectQueryBuilder(gHierarchicalTable, gHierarchicalTable["parent"], "root");
            using (query = connection.GetQuery(hbuilder))
            {
                query.BindParam("root", 3);
                query.ExecuteReader();
                int rc = 0;
                while (query.ReadNext())
                {
                    rc++;
                    switch (query.GetValue<int>("id"))
                    {
                        case 3:
                            ClassicAssert.AreEqual(1, query.GetValue<int>("level"));
                            break;
                        case 6:
                            ClassicAssert.AreEqual(2, query.GetValue<int>("level"));
                            break;
                        case 7:
                            ClassicAssert.AreEqual(2, query.GetValue<int>("level"));
                            break;
                        case 8:
                            ClassicAssert.AreEqual(3, query.GetValue<int>("level"));
                            break;
                        default:
                            ClassicAssert.Fail("Unknown ID");
                            break;
                    }
                }
                ClassicAssert.AreEqual(4, rc);
            }
            hbuilder = connection.GetHierarchicalSelectQueryBuilder(gHierarchicalTable, gHierarchicalTable["parent"], "root");
            hbuilder.IdOnlyMode = true;
            using (query = connection.GetQuery(hbuilder))
            {
                query.BindParam("root", 3);
                query.ExecuteReader();
                ClassicAssert.AreEqual(1, query.FieldCount);
                int rc = 0;
                while (query.ReadNext())
                {
                    rc++;
                    switch (query.GetValue<int>("id"))
                    {
                        case 3:
                            break;
                        case 6:
                            break;
                        case 7:
                            break;
                        case 8:
                            break;
                        default:
                            ClassicAssert.Fail("Unknown ID");
                            break;
                    }
                }
                ClassicAssert.AreEqual(4, rc);
            }
        }
    }
}

