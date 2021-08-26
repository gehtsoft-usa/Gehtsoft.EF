using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity
{
    public class CompositeIndexTest
    {
        [Fact]
        public void Name()
        {
            CompositeIndex md = new CompositeIndex("test1");
            md.Name.Should().Be("test1");
        }

        [Fact]
        public void Add1()
        {
            CompositeIndex md = new CompositeIndex("test1")
            {
                "field1"
            };
            md.Should().HaveCount(1);
            md.Should().Contain(f => f.Name == "field1" && f.Function == null && f.Direction == SortDir.Asc);
        }

        [Fact]
        public void Add2()
        {
            CompositeIndex md = new CompositeIndex("test1")
            {
                { "field1", SortDir.Desc }
            };
            md.Should().HaveCount(1);
            md.Should().Contain(f => f.Name == "field1" && f.Function == null && f.Direction == SortDir.Desc);
        }

        [Fact]
        public void Add3()
        {
            CompositeIndex md = new CompositeIndex("test1")
            {
                { SqlFunctionId.Abs, "field1" }
            };
            md.Should().HaveCount(1);
            md.Should().Contain(f => f.Name == "field1" && f.Function == SqlFunctionId.Abs && f.Direction == SortDir.Asc);
        }

        [Fact]
        public void Add4()
        {
            CompositeIndex md = new CompositeIndex("test1")
            {
                { SqlFunctionId.Upper, "field2", SortDir.Desc }
            };
            md.Should().HaveCount(1);
            md.Should().Contain(f => f.Name == "field2" && f.Function == SqlFunctionId.Upper && f.Direction == SortDir.Desc);
        }

        [Fact]
        public void Add5()
        {
            CompositeIndex md = new CompositeIndex("test1")
            {
                "field1",
                "field2"
            };
            md.Should().HaveCount(2);
            md.Should().HaveOneElementAfterTheOther(f => f.Name == "field1", f => f.Name == "field2");
        }
    }
}
