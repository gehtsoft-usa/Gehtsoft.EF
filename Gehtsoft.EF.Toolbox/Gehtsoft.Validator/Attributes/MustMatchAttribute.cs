using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.Validator
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MustMatchAttribute : ValidatorAttributeBase
    {
        public string Pattern { get; set; }

        public MustMatchAttribute()
        {
        }
        public MustMatchAttribute(string pattern)
        {
            Pattern = pattern;
        }
    }
}
