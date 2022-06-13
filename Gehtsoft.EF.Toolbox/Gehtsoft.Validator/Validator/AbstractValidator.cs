using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Gehtsoft.Validator
{
    public class AbstractValidator<T> : BaseValidator, IValidator<T>
    {
        public AbstractValidator() : base(typeof(T))
        {
        }

        public void When(Func<T, bool> predicate)
        {
            base.When(new FunctionPredicate<T>(predicate));
        }

        public void Unless(Func<T, bool> predicate)
        {
            base.Unless(new FunctionPredicate<T>(predicate));
        }

        public new GenericValidationRuleBuilder<T, T> RuleForEntity(string name = null)
        {
            return new GenericValidationRuleBuilder<T, T>(this, base.RuleForEntity(name).Rule);
        }

        public GenericValidationRuleBuilder<T, TV> RuleFor<TV>(string propertyName)
        {
            return new GenericValidationRuleBuilder<T, TV>(this, base.RuleFor(propertyName).Rule);
        }

        public GenericValidationRuleBuilder<T, TV> RuleForAll<TV>(string propertyName)
        {
            return new GenericValidationRuleBuilder<T, TV>(this, base.RuleForAll(propertyName).Rule);
        }

        public GenericValidationRuleBuilder<T, TV> RuleFor<TV>(Expression<Func<T, TV>> accessor, string name = null)
        {
            ValidationRule rule = new ValidationRule(typeof(T), typeof(TV))
            {
                Target = new FunctionValidationTarget<T, TV>(accessor, name)
            };
            mRules.Add(rule);
            return new GenericValidationRuleBuilder<T, TV>(this, rule);
        }

        public GenericValidationRuleBuilder<T, TV> RuleForAll<TV>(Expression<Func<T, IEnumerable<TV>>> accessor, string name = null)
        {
            ValidationRule rule = new ValidationRule(typeof(T), typeof(TV))
            {
                Target = new FunctionValidationArrayTarget<T, IEnumerable<TV>>(accessor, name)
            };
            mRules.Add(rule);
            return new GenericValidationRuleBuilder<T, TV>(this, rule);
        }

        public virtual ValidationResult Validate(T entity) => base.Validate(entity);
    }
}
