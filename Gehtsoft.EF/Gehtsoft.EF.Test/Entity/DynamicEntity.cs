using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Microsoft.CSharp.RuntimeBinder;
using Xunit;

namespace Gehtsoft.EF.Test.Entity
{
    public class DynamicEntityTest
    {
        public class DynamicTestEntity : DynamicEntity
        {
            public override EntityAttribute EntityAttribute
            {
                get
                {
                    return new EntityAttribute()
                    {
                        Scope = "dynamicentity",
                        Table = "dynamictable",
                    };
                }
            }

            protected override IEnumerable<IDynamicEntityProperty> InitializeProperties()
            {
                yield return new DynamicEntityProperty(typeof(int), "Property1", new EntityPropertyAttribute()
                {
                    PrimaryKey = true,
                    Autoincrement = true,
                });

                yield return new DynamicEntityProperty(typeof(string), "Property2", new EntityPropertyAttribute()
                {
                    Sorted = true,
                    Nullable = true,
                    Size = 84,
                });

                yield return new DynamicEntityProperty(typeof(DateTime), "Property3", new EntityPropertyAttribute()
                {
                });

                yield return new DynamicEntityProperty(typeof(DateTime?), "Property4", new EntityPropertyAttribute()
                {
                    Nullable = true
                });
            }
        }

        [Fact]
        public void DynamicBehavior()
        {
            dynamic entity = new DynamicTestEntity();

            ((object)entity.Property1).Should().Be(0);
            entity.Property1 = 10;
            ((object)entity.Property1).Should().Be(10);

            entity.Property1 = 11;
            ((object)entity.Property1).Should().Be(11);

            ((object)entity.Property2).Should().Be(null);
            entity.Property2 = "abcd";
            ((object)entity.Property2).Should().Be("abcd");

            ((object)entity.Property3).Should().Be(new DateTime(0));
            entity.Property3 = new DateTime(2010, 12, 25);
            ((object)entity.Property3).Should().Be(new DateTime(2010, 12, 25));

            ((object)entity.Property4).Should().Be(null);
            entity.Property4 = new DateTime(2010, 12, 26);
            ((object)entity.Property4).Should().Be(new DateTime(2010, 12, 26));

            entity.Property4 = null;
            ((object)entity.Property4).Should().Be(null);

            ((Action)(() => GetNonExistentProperty(entity))).Should().Throw<RuntimeBinderException>();
        }

        private object GetNonExistentProperty(dynamic entity) => entity.RandomProperty;
    }
}
