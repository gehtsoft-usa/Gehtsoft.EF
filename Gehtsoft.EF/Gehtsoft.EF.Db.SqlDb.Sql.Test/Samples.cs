using System;
using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Xunit;
using FluentAssertions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public sealed class NorthwindFixture : IDisposable
    {
        private SqlDbConnection Connection { get; }
        private SqlCodeDomBuilder Builder { get; }

        public SqlCodeDomEnvironment CreateEnvironment() => Builder.NewEnvironment(Connection);

        public NorthwindFixture()
        {
            Connection = SqliteDbConnectionFactory.CreateMemory();

            var entities = EntityFinder.FindEntities(new[] { typeof(Northwind.Category).Assembly }, "northwind", false);
            Builder = new SqlCodeDomBuilder();
            Builder.Build(entities, "northwind");

            Snapshot northwindData = new Snapshot();
            northwindData.CreateAsync(Connection).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Connection.Dispose();
        }
    }

    public class Samples : IClassFixture<NorthwindFixture>
    {
        private readonly NorthwindFixture mNorthwind;

        private SqlCodeDomEnvironment CreateEnvironment() => mNorthwind.CreateEnvironment();

        public Samples(NorthwindFixture northwind)
        {
            mNorthwind = northwind;
        }

        public void SampleSelect1()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT * FROM Category");
            var categories = statement(null);
            categories.Should().NotBeNull();
            foreach (var category in categories)
                Console.WriteLine("{0} {1}", category.CategoryID, category.CategoryName, category.Description);
        }

        public void SampleSelect2()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT * FROM Category WHERE CategoryID > 3");
            var categories = statement(null);
            foreach (var category in categories)
                Console.WriteLine("{0} {1}", category.CategoryID, category.CategoryName, category.Description);
        }

        public void SampleSelect3()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT * FROM Category WHERE CategoryID > ?CategoryID");
            var categories = statement(new Dictionary<string, object> { { "CategoryID", 3 } });
            foreach (var category in categories)
                Console.WriteLine("{0} {1}", category.CategoryID, category.CategoryName, category.Description);
        }
    }
}
