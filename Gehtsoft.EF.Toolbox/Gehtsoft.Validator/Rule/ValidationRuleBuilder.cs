using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class ValidationRuleBuilder
    {
        public ValidationRule Rule { get; private set; }
        public BaseValidator Validator { get; private set; }

        public ValidationRuleBuilder(BaseValidator validator, ValidationRule validationRule)
        {
            Rule = validationRule;
            Validator = validator;
        }

        public ValidationRuleBuilder Must(IValidationPredicate predicate)
        {
            Rule.Validator = predicate;
            return this;
        }

        public ValidationRuleBuilder WhenValue(IValidationPredicate predicate)
        {
            Rule.WhenValue = predicate;
            return this;
        }

        public ValidationRuleBuilder UnlessValue(IValidationPredicate predicate)
        {
            Rule.UnlessValue = predicate;
            return this;
        }

        public ValidationRuleBuilder WhenEntity(IValidationPredicate predicate)
        {
            Rule.WhenEntity = predicate;
            return this;
        }

        public ValidationRuleBuilder UnlessEntity(IValidationPredicate predicate)
        {
            Rule.UnlessEntity = predicate;
            return this;
        }

        public ValidationRuleBuilder Null()
        {
            Rule.Validator = new IsNullPredicate(Rule.Target.ValueType);
            return this;
        }

        public ValidationRuleBuilder NotNull()
        {
            Rule.Validator = new IsNotNullPredicate(Rule.Target.ValueType);
            return this;
        }

        public ValidationRuleBuilder NotNullOrEmpty()
        {
            Rule.Validator = new IsNotNullOrEmptyPredicate(Rule.Target.ValueType);
            return this;
        }

        public ValidationRuleBuilder NotNullOrWhitespace()
        {
            Rule.Validator = new IsNotNullOrWhitespacePredicate(Rule.Target.ValueType);
            return this;
        }

        public ValidationRuleBuilder ShorterThan(int length)
        {
            Rule.Validator = new IsShorterThanPredicate(Rule.Target.ValueType, length);
            return this;
        }

        public ValidationRuleBuilder DoesMatch(string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
        {
            Rule.Validator = new DoesMatchPredicate(Rule.Target.ValueType, pattern, options, timeout);
            return this;
        }

        public ValidationRuleBuilder DoesNotMatch(string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
        {
            Rule.Validator = new DoesNotMatchPredicate(Rule.Target.ValueType, pattern, options, timeout);
            return this;
        }

        public ValidationRuleBuilder EnumIsCorrect()
        {
            Rule.Validator = new IsEnumValueCorrectPredicate(Rule.Target.ValueType);
            return this;
        }

        public ValidationRuleBuilder EnumIsCorrect(Type enumType)
        {
            Rule.Validator = new IsEnumValueCorrectPredicate(enumType);
            return this;
        }

        public ValidationRuleBuilder WithCode(int code)
        {
            Rule.Code = code;
            return this;
        }

        public ValidationRuleBuilder ServerOnly()
        {
            Rule.IgnoreOnClient = true;
            return this;
        }

        public ValidationRuleBuilder WithMessage(string message)
        {
            Rule.Message = ValidationMessageResolverFactory.GetResolver(Rule.EntityType).Resolve(Rule.EntityType, Rule.Target, Rule.Code, message);
            return this;
        }

        public ValidationRuleBuilder ValidateUsing(Type validatorType)
        {
            Rule.AnotherValidatorType = validatorType;
            Rule.AnotherValidatorArgs = null;
            return this;
        }

        public ValidationRuleBuilder ValidateUsing(Type validatorType, object[] args)
        {
            Rule.AnotherValidatorType = validatorType;
            Rule.AnotherValidatorArgs = args;
            return this;
        }

        public ValidationRuleBuilder ValidateUsing(IBaseValidator validator)
        {
            Rule.AnotherValidatorInstance = validator;
            Rule.AnotherValidatorType = null;
            Rule.AnotherValidatorArgs = null;

            return this;
        }

        public ValidationRuleBuilder WhenNotNull() => WhenValue(new IsNotNullPredicate(Rule.Target.ValueType));

        internal void ReplaceTarget(ValidationTarget target)
        {
            Rule.Target = target;
        }

        public ValidationRuleBuilder EmailAddress()
        {
            Rule.Validator = new EmailAddressPredicate();
            return this;
        }

        public ValidationRuleBuilder NotSQLInjection()
        {
            Rule.Validator = new SQLInjectionPredicate();
            return this;
        }

        public ValidationRuleBuilder NotHTML()
        {
            Rule.Validator = new HTMLInjectionPredicate();
            return this;
        }

        public ValidationRuleBuilder PhoneNumber()
        {
            Rule.Validator = new PhoneNumberPredicate();
            return this;
        }

        public ValidationRuleBuilder CreditCardNumber()
        {
            Rule.Validator = new CreditCardNumberPredicate(Rule.RuleValueType);
            return this;
        }
    }
}