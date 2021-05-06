using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.PostgresDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.Tools.TypeUtils;
using NUnit.Framework;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;

namespace TestApp
{
    [TestFixture]
    public class DebugTests
    {
        [Entity]
        public class Employee
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Sorted = true)]
            public int Code { get; set; }
        }

        [Ignore("Debug Test")]
        [Test]
        [Explicit]
        public void Test()
        {
            using (var connection = SqliteDbConnectionFactory.CreateMemory())
            {
                using (var query = connection.GetCreateEntityQuery<Employee>())
                    query.Execute();

                Random r = new Random();
                using (var t = connection.BeginTransaction())
                {
                    using (var query = connection.GetInsertEntityQuery<Employee>())
                    {
                        for (int i = 0; i < 10000; i++)
                        {
                            Employee e = new Employee
                            {
                                Code = r.Next(1, 10)
                            };
                            query.Execute(e);
                        }
                    }

                    t.Commit();
                }

                SelectQueryBuilder builder;
                int count = 0;
                using (SelectEntitiesQueryBase q1 = connection.GetGenericSelectEntityQuery<Employee>())
                {
                    q1.Distinct = true;
                    q1.AddToResultset(nameof(Employee.Code), "code");
                    builder = new SelectQueryBuilder(connection.GetLanguageSpecifics(), q1.SelectBuilder.SelectQueryBuilder.QueryTableDescriptor);
                    builder.AddToResultset(AggFn.Count);
                    using (SqlDbQuery q2 = connection.GetQuery(builder))
                    {
                        q2.ExecuteReader();
                        if (!q2.ReadNext())
                            count = 0;
                        else
                            count = q2.GetValue<int>(0);
                    }
                }
                Console.Write("{0}", count);

                var entity = AllEntities.Inst[typeof(Employee)];
                builder = new SelectQueryBuilder(connection.GetLanguageSpecifics(), entity.TableDescriptor);
                builder.AddExpressionToResultset($"count (distinct {entity[nameof(Employee.Code)].Name})", DbType.Int32, true, "mycount");
                using (SqlDbQuery q2 = connection.GetQuery(builder))
                {
                    q2.ExecuteReader();
                    if (!q2.ReadNext())
                        count = 0;
                    else
                        count = q2.GetValue<int>(0);
                }

                Console.Write("{0}", count);

                using (SqlDbQuery q1 = connection.GetQuery("select count(distinct code) from employee"))
                {
                    q1.ExecuteReader();
                    q1.ReadNext();
                    if (!q1.ReadNext())
                        count = 0;
                    else
                        count = q1.GetValue<int>(0);
                }

                Console.Write("{0}", count);
            }
        }
    }
}
