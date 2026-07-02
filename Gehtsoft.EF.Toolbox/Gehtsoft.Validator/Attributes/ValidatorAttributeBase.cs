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

        private int mWithCode;

        /// <summary>
        /// The validation code applied to the rule generated from this attribute. Assigning it
        /// automatically sets <see cref="HasCode"/> to true. The property is a plain
        /// <see cref="int"/> (not <c>int?</c>) so it can be used as a named attribute argument.
        /// </summary>
        public int WithCode
        {
            get => mWithCode;
            set
            {
                mWithCode = value;
                HasCode = true;
            }
        }

        /// <summary>
        /// True when <see cref="WithCode"/> has been explicitly assigned on this attribute.
        /// </summary>
        public bool HasCode { get; private set; }

        public string WithMessage { get; set; } = null;
        public bool ForElement { get; set; } = false;
    }
}