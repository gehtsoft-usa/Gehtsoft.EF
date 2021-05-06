using System;

namespace Gehtsoft.Validator
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MustBeInRangeAttribute : ValidatorAttributeBase
    {
        public object Mininum { get; set; } = null;
        public bool MinimumInclusive { get; set; } = true;
        public object Maximum { get; set; } = null;
        public bool MaximumInclusive { get; set; } = true;

        public MustBeInRangeAttribute()
        {
        }
    }
}