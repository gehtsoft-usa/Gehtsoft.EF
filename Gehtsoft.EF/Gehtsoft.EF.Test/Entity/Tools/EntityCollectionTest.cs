using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class EntityCollectionTest
    {
        [Entity]
        public class Data
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Fact]
        public void Add()
        {
            var collection = new EntityCollection<Data>();
            collection.Add(new Data() { Id = 1, Name = "name1" });
            collection.Should().HaveCount(1);
            collection[0].Id.Should().Be(1);
            collection.Add(new Data() { Id = 2, Name = "name2" });
            collection.Should().HaveCount(2);
            collection[0].Id.Should().Be(1);
            collection[1].Id.Should().Be(2);
        }

        [Fact]
        public void Insert()
        {
            var collection = new EntityCollection<Data>();
            collection.Insert(0, new Data() { Id = 1, Name = "name1" });
            collection.Should().HaveCount(1);
            collection[0].Id.Should().Be(1);

            collection.Insert(0, new Data() { Id = 2, Name = "name2" });
            collection.Should().HaveCount(2);
            collection[0].Id.Should().Be(2);
            collection[1].Id.Should().Be(1);

            collection.Insert(1, new Data() { Id = 3, Name = "name3" });
            collection.Should().HaveCount(3);
            collection[0].Id.Should().Be(2);
            collection[1].Id.Should().Be(3);
            collection[2].Id.Should().Be(1);

            ((Action)(() => collection.Insert(10, new Data()))).Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void RemoveAt()
        {
            var collection = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name5" },
                new Data() { Id = 5, Name = "name5" }
            };

            collection.RemoveAt(0);
            collection.Should().HaveCount(4);
            collection.Should().NotContain(e => e.Id == 1);

            collection.RemoveAt(3);
            collection.Should().HaveCount(3);
            collection.Should().NotContain(e => e.Id == 5);

            collection.RemoveAt(1);
            collection.Should().HaveCount(2);
            collection.Should().NotContain(e => e.Id == 3);
        }

        [Fact]
        public void RemoveByRef()
        {
            var collection = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            collection.Remove(collection[1]);
            collection.Should().HaveCount(4);
            collection.Should().NotContain(e => e.Id == 2);
            collection.Should().Contain(e => e.Id == 1);
            collection.Should().Contain(e => e.Id == 3);
            collection.Should().Contain(e => e.Id == 4);
            collection.Should().Contain(e => e.Id == 5);
        }

        [Fact]
        public void RemoveByValue()
        {
            var collection = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            collection.Remove(new Data() { Id = 2, Name = "name2" });
            collection.Should().HaveCount(4);
            collection.Should().NotContain(e => e.Id == 2);
            collection.Should().Contain(e => e.Id == 1);
            collection.Should().Contain(e => e.Id == 3);
            collection.Should().Contain(e => e.Id == 4);
            collection.Should().Contain(e => e.Id == 5);
        }


        [Fact]
        public void Enumerator()
        {
            var collection = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            ((IEnumerable<Data>)collection).Should().Contain(e => e.Id == 1);
            ((IEnumerable<Data>)collection).Should().Contain(e => e.Id == 2);
            ((IEnumerable<Data>)collection).Should().Contain(e => e.Id == 3);
            ((IEnumerable<Data>)collection).Should().Contain(e => e.Id == 4);
            ((IEnumerable<Data>)collection).Should().Contain(e => e.Id == 5);
        }

        [Fact]
        public void CompareDifferentSize()
        {
            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            var collection2 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
            };

            collection1.Equals(collection2).Should().BeFalse();
        }

        [Fact]
        public void CompareDifferentContent()
        {
            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            var collection2 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name6" }
            };

            collection1.Equals(collection2).Should().BeFalse();
        }
        
        [Fact]
        public void CompareSameContent()
        {
            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            var collection2 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            collection1.Equals(collection2).Should().BeTrue();
        }

        [Fact]
        public void Clone()
        {
            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            var collection2 = collection1.Clone();

            collection2.Should().NotBeSameAs(collection1);
            collection2.Equals(collection1).Should().BeTrue();
        }

        [Fact]
        public void ToArray()
        {
            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            var arr = collection1.ToArray();

            arr.Should().Contain(e => ReferenceEquals(e, collection1[0]));
            arr.Should().Contain(e => ReferenceEquals(e, collection1[1]));
            arr.Should().Contain(e => ReferenceEquals(e, collection1[2]));
            arr.Should().Contain(e => ReferenceEquals(e, collection1[3]));
            arr.Should().Contain(e => ReferenceEquals(e, collection1[4]));
        }

        [Fact]
        public void AddRange()
        {
            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };

            var collection2 = new EntityCollection<Data>();
            collection2.AddRange(collection1);
            collection2.Should().BeEquivalentTo(collection1);
        }

        [Fact]
        public void Clear()
        {
            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 3, Name = "name3" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };
            collection1.Should().HaveCount(5);
            collection1.Clear();
            collection1.Should().HaveCount(0);
        }

        [Fact]
        public void Countains_Success()
        {
            var d3 = new Data() { Id = 3, Name = "name3" };

            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                d3,
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };
            collection1.Contains(d3).Should().BeTrue();
        }

        [Fact]
        public void Countains_Fail()
        {
            var d3 = new Data() { Id = 3, Name = "name3" };

            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };
            collection1.Contains(d3).Should().BeFalse();
        }

        [Fact]
        public void IndexOf_Success()
        {
            var d3 = new Data() { Id = 3, Name = "name3" };

            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                d3,
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };
            collection1.IndexOf(d3).Should().Be(2);
        }

        [Fact]
        public void IndexOf_Fail()
        {
            var d3 = new Data() { Id = 3, Name = "name3" };

            var collection1 = new EntityCollection<Data>()
            {
                new Data() { Id = 1, Name = "name1" },
                new Data() { Id = 2, Name = "name2" },
                new Data() { Id = 4, Name = "name4" },
                new Data() { Id = 5, Name = "name5" }
            };
            collection1.IndexOf(d3).Should().BeLessThan(0);
        }

        [Fact]
        public void Events()
        {
            int index;
            bool add, change, remove;

            var collection1 = new EntityCollection<Data>();

            collection1.AfterInsert += (s, idx) =>
            {
                add = true;
                index = idx;
            };

            collection1.OnChange += (s, idx) =>
            {
                change = true;
                index = idx;
            };

            collection1.BeforeDelete += (s, idx) =>
            {
                remove = true;
                index = idx;
            };


            index = -1;
            add = change = remove = false;
           
            collection1.Add(new Data());

            add.Should().BeTrue();
            change.Should().BeFalse();
            remove.Should().BeFalse();
            index.Should().Be(0);


            index = -1;
            add = change = remove = false;

            collection1.Add(new Data());

            add.Should().BeTrue();
            change.Should().BeFalse();
            remove.Should().BeFalse();
            index.Should().Be(1);

            index = -1;
            add = change = remove = false;

            collection1.Add(new Data());

            add.Should().BeTrue();
            change.Should().BeFalse();
            remove.Should().BeFalse();
            index.Should().Be(2);

            index = -1;
            add = change = remove = false;

            collection1.Insert(0, new Data());

            add.Should().BeTrue();
            change.Should().BeFalse();
            remove.Should().BeFalse();
            index.Should().Be(0);

            index = -1;
            add = change = remove = false;

            collection1[1] = new Data();

            add.Should().BeFalse();
            change.Should().BeTrue();
            remove.Should().BeFalse();
            index.Should().Be(1);


            index = -1;
            add = change = remove = false;

            collection1.RemoveAt(1);

            add.Should().BeFalse();
            change.Should().BeFalse();
            remove.Should().BeTrue();
            index.Should().Be(1);
        }


    }
}
