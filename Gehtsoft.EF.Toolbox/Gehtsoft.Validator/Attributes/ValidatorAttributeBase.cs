using System;

namespace Gehtsoft.Validator
{
    [AttributeUsage(AttributeTargets.Property)]
#pragma warning disable S3376 // Attribute, EventArgs, and Exception type names should end with the type being extended
    // Ignored in sake of backward compatibility.
    public abstract class ValidatorAttributeBase : Attribute
#pragma warning restore S3376 
    {
        protected ValidatorAttributeBase()
        {
        }

        public int? WidthCode { get; set; } = null;
        public string WithMessage { get; set; } = null;
        public bool ForElement { get; set; } = false;
    }
}