using System;

namespace Gehtsoft.Validator
{
    public class ValidatorAttributeBase : Attribute
    {
        protected ValidatorAttributeBase()
        {

        }

        public int? WidthCode { get; set; } = null;
        public string WithMessage { get; set; } = null;
        public bool ForElement { get; set; } = false;
    }
}