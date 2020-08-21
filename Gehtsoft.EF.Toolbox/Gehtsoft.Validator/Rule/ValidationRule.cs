using System;

namespace Gehtsoft.Validator
{
    public interface IValidationRule
    {
        IValidationTarget Target { get; }
        IValidationPredicate Validator { get; }
        IValidationPredicate WhenValue { get; }
        IValidationPredicate WhenEntity { get; }
        IValidationPredicate UnlessValue { get; }
        IValidationPredicate UnlessEntity { get; }
        int Code { get; }
        string Message { get; }
        bool HasAnotherValidator { get; }
        IBaseValidator AnotherValidator { get; }
        bool IgnoreOnClient { get; }
    }

    public class ValidationRule : IValidationRule
    {
        public Type EntityType { get; set; }
        public Type RuleValueType { get; set; }
        public ValidationTarget Target { get; set; }
        IValidationTarget IValidationRule.Target => Target;
        public IValidationPredicate Validator { get; set; }
        public IValidationPredicate WhenValue { get; set; }
        public IValidationPredicate WhenEntity { get; set; }
        public IValidationPredicate UnlessValue { get;  set; }
        public IValidationPredicate UnlessEntity { get;  set; }
        public int Code { get;  set; }
        public string Message { get;  set; }
        public bool HasAnotherValidator => AnotherValidatorType != null || mAnotherValidator != null;
        public Type AnotherValidatorType { get; set; }
        public bool IgnoreOnClient { get;  set; }
        internal object[] AnotherValidatorArgs { get; set; }
        
        public IBaseValidator AnotherValidatorInstance 
        { 
            get => AnotherValidator;
            set => mAnotherValidator = value;
        }
        private IBaseValidator mAnotherValidator;
        
        public IBaseValidator AnotherValidator => HasAnotherValidator ? (mAnotherValidator ?? (mAnotherValidator = (IBaseValidator) (AnotherValidatorArgs == null ? Activator.CreateInstance(AnotherValidatorType) : Activator.CreateInstance(AnotherValidatorType, AnotherValidatorArgs)))) : null;

        internal ValidationRule(Type te, Type tv)
        {
            EntityType = te;
            RuleValueType = tv;
        }
    }
}
