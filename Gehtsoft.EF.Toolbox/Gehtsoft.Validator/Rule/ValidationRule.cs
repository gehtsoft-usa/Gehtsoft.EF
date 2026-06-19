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

        /// <summary>
        /// The side on which the rule is executed. When the side is not set explicitly,
        /// it is derived from the rule's validation predicate.
        /// </summary>
        RuleExecutionSide Side => IgnoreOnClient ? RuleExecutionSide.Server : RuleExecutionSide.Both;
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
        public IValidationPredicate UnlessValue { get; set; }
        public IValidationPredicate UnlessEntity { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public bool HasAnotherValidator => AnotherValidatorType != null || mAnotherValidator != null;
        public Type AnotherValidatorType { get; set; }
        internal RuleExecutionSide? ExplicitSide { get; set; }

        public RuleExecutionSide Side => ExplicitSide ?? Validator?.Side ?? RuleExecutionSide.Both;

        public bool IgnoreOnClient
        {
            get => Side == RuleExecutionSide.Server;
            set => ExplicitSide = value ? RuleExecutionSide.Server : (RuleExecutionSide?)null;
        }

        internal object[] AnotherValidatorArgs { get; set; }

        public IBaseValidator AnotherValidatorInstance
        {
            get => AnotherValidator;
            set => mAnotherValidator = value;
        }
        private IBaseValidator mAnotherValidator;

        public IBaseValidator AnotherValidator => HasAnotherValidator ? (mAnotherValidator ?? (mAnotherValidator = (IBaseValidator)(AnotherValidatorArgs == null ? Activator.CreateInstance(AnotherValidatorType) : Activator.CreateInstance(AnotherValidatorType, AnotherValidatorArgs)))) : null;

        internal ValidationRule(Type te, Type tv)
        {
            EntityType = te;
            RuleValueType = tv;
        }
    }
}
