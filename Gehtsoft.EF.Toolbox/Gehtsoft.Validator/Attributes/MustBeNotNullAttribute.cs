using System;

namespace Gehtsoft.Validator
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MustBeNotNullAttribute : ValidatorAttributeBase
    {
    }
}