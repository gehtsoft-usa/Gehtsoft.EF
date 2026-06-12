using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Gehtsoft.EF.Test.Utils
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
