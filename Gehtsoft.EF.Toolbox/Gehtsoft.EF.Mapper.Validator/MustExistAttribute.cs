using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Mapper.Validator
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MustExistAttribute : ValidatorAttributeBase
    {
    }
}
