using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Gehtsoft.EF.Test
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TestOrderAttribute : Attribute
    {
        public int Order { get; set; }
        
        public TestOrderAttribute()
        {
            Order = int.MaxValue;
        }

        public TestOrderAttribute(int order)
        {
            Order = order;
        }
    }
}
