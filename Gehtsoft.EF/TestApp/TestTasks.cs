using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using NUnit.Framework;

namespace TestApp
{
    class TestTasksImpl
    {
        [Entity]
        public class MTTestEntity
        {
            [AutoId]
            public int AutoId { get; set; }
            [EntityProperty(Size = 32)]
            public string TaskId { get; set; }
        }

        public static void Test(SqlDbConnection connection)
        {
            using (var query = connection.GetDropEntityQuery<MTTestEntity>())
                query.Execute();
            using (var query = connection.GetCreateEntityQuery<MTTestEntity>())
                query.Execute();

            bool hasError = false;
            int completedCount = 0;

            for (int i = 0; i < 10; i++)
            {
                string taskId = $"TaskId {i}";
                Task.Run(() =>
                {

                    for (int j = 0; j < 10; j++)
                    {
                        try
                        {
                            using (connection.Lock())
                            {
                                using (var query = connection.GetInsertEntityQuery<MTTestEntity>())
                                {
                                    MTTestEntity e = new MTTestEntity();
                                    e.TaskId = taskId;
                                    query.Execute(e);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("{0}", e.ToString());
                            hasError = true;
                            return;
                        }
                    }

                    Interlocked.Increment(ref completedCount);
                    return;
                });
            }

            CancellationTokenSource source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromSeconds(3));
            Task wt = Task.Run(() =>
            {
                while (true)
                {
                    using (connection.Lock())
                    {
                        using (var query = connection.GetSelectEntitiesCountQuery<MTTestEntity>())
                        {
                            Console.WriteLine("{0}", query.RowCount);
                            query.Execute();
                            if (query.RowCount >= 100)
                                return;
                        }
                    }
                    Task.Delay(1).Wait();
                }
            }, source.Token);

            wt.Wait();

            Assert.IsFalse(hasError);
            Assert.AreEqual(10, completedCount);

            using (var query = connection.GetSelectEntitiesCountQuery<MTTestEntity>())
            {
                var cc = query.RowCount;
                Assert.AreEqual(cc, 100);
            }

        }
    }
}
