using System;

namespace Gehtsoft.Validator
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MustBeShorterThanAttribute : ValidatorAttributeBase
    {
        public int Length { get; set; }

        public MustBeShorterThanAttribute() 
        {

        }

        public MustBeShorterThanAttribute(int length)
        {
            Length = length;
        }
    }
}