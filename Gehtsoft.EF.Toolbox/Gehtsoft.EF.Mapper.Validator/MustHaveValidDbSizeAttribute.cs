using System;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Mapper.Validator
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MustHaveValidDbSizeAttribute : ValidatorAttributeBase
    {
    }
}