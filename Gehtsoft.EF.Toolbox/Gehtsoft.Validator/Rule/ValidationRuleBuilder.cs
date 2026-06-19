using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class ValidationRuleBuilder
    {
        public ValidationRule Rule { get; }
        public BaseValidator Validator { get; }

        public ValidationRuleBuilder(BaseValidator validator, ValidationRule validationRule)
        {
            Rule = validationRule;
            Validator = validator;
        }

        public ValidationRuleBuilder Must(IValidationPredicate predicate)
        {
            CheckSideConsistency(predicate, "validation");
            Rule.Validator = predicate;
            return this;
        }

        public ValidationRuleBuilder WhenValue(IValidationPredicate predicate)
        {
            CheckSideConsistency(predicate, "condition");
            Rule.WhenValue = predicate;
            return this;
        }

        public ValidationRuleBuilder UnlessValue(IValidationPredicate predicate)
        {
            CheckSideConsistency(predicate, "condition");
            Rule.UnlessValue = predicate;
            return this;
        }

        public ValidationRuleBuilder WhenEntity(IValidationPredicate predicate)
        {
            CheckSideConsistency(predicate, "condition");
            Rule.WhenEntity = predicate;
            return this;
        }

        public ValidationRuleBuilder UnlessEntity(IValidationPredicate predicate)
        {
            CheckSideConsistency(predicate, "condition");
            Rule.UnlessEntity = predicate;
            return this;
        }

        /// <summary>
        /// Explicitly sets the side on which the rule is executed. Setting
        /// <see cref="RuleExecutionSide.Both"/> requires every predicate of the rule
        /// to be expressible as a client-side script.
        /// </summary>
        public ValidationRuleBuilder SetSide(RuleExecutionSide side)
        {
            if (side == RuleExecutionSide.Both)
            {
                EnsureClientCompatible(Rule.Validator, "validation");
                EnsureClientCompatible(Rule.WhenValue, "condition");
                EnsureClientCompatible(Rule.WhenEntity, "condition");
                EnsureClientCompatible(Rule.UnlessValue, "condition");
                EnsureClientCompatible(Rule.UnlessEntity, "condition");
            }
            Rule.ExplicitSide = side;
            return this;
        }

        private void CheckSideConsistency(IValidationPredicate predicate, string role)
        {
            if (Rule.ExplicitSide == RuleExecutionSide.Both)
                EnsureClientCompatible(predicate, role);
        }

        private static void EnsureClientCompatible(IValidationPredicate predicate, string role)
        {
            if (predicate != null && predicate.Side == RuleExecutionSide.Server)
                throw new InvalidOperationException($"The {role} predicate supports server-side execution only, so the rule cannot be executed on both sides");
        }

        public ValidationRuleBuilder Null() => Must(new IsNullPredicate(Rule.Target.ValueType));

        public ValidationRuleBuilder NotNull() => Must(new IsNotNullPredicate(Rule.Target.ValueType));

        public ValidationRuleBuilder NotNullOrEmpty() => Must(new IsNotNullOrEmptyPredicate(Rule.Target.ValueType));

        public ValidationRuleBuilder NotNullOrWhitespace() => Must(new IsNotNullOrWhitespacePredicate(Rule.Target.ValueType));

        public ValidationRuleBuilder ShorterThan(int length) => Must(new IsShorterThanPredicate(Rule.Target.ValueType, length));

        public ValidationRuleBuilder DoesMatch(string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
            => Must(new DoesMatchPredicate(Rule.Target.ValueType, pattern, options, timeout));

        public ValidationRuleBuilder DoesNotMatch(string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
            => Must(new DoesNotMatchPredicate(Rule.Target.ValueType, pattern, options, timeout));

        public ValidationRuleBuilder EnumIsCorrect()
            => Must(new IsEnumValueCorrectPredicate(Rule.Target.ValueType));

        public ValidationRuleBuilder EnumIsCorrect(Type enumType)
            => Must(new IsEnumValueCorrectPredicate(enumType));

        public ValidationRuleBuilder WithCode(int code)
        {
            Rule.Code = code;
            return this;
        }

        public ValidationRuleBuilder ServerOnly() => SetSide(RuleExecutionSide.Server);

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

        public ValidationRuleBuilder EmailAddress() => Must(new EmailAddressPredicate());

        public ValidationRuleBuilder NotSQLInjection() => Must(new SqlInjectionPredicate());

        public ValidationRuleBuilder NotHTML() => Must(new HtmlInjectionPredicate());

        public ValidationRuleBuilder PhoneNumber() => Must(new PhoneNumberPredicate());

        public ValidationRuleBuilder CreditCardNumber() => Must(new CreditCardNumberPredicate(Rule.RuleValueType));
    }
}