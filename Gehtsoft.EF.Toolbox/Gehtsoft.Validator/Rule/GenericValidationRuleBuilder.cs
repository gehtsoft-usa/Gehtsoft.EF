using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Gehtsoft.Validator
{
    public class GenericValidationRuleBuilder<TE, TV> : ValidationRuleBuilder
    {
        public GenericValidationRuleBuilder(BaseValidator validator, ValidationRule validationRule) : base(validator, validationRule)
        {
        }

        public GenericValidationRuleBuilder<TE, TV> Must(Expression<Func<TV, bool>> predicate)
        {
            base.Must(new ExpressionPredicate<TV>(predicate, typeof(TE) == typeof(TV)));
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> EntityMust(Expression<Func<TE, bool>> predicate)
        {
            base.Must(new ExpressionPredicate<TE>(predicate, true));
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> WhenValue(Expression<Func<TV, bool>> predicate)
        {
            base.WhenValue(new ExpressionPredicate<TV>(predicate, false));
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> WhenEntity(Expression<Func<TE, bool>> predicate)
        {
            base.WhenEntity(new ExpressionPredicate<TE>(predicate, true));
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> UnlessValue(Expression<Func<TV, bool>> predicate)
        {
            base.UnlessValue(new ExpressionPredicate<TV>(predicate, false));
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> UnlessEntity(Expression<Func<TE, bool>> predicate)
        {
            base.UnlessEntity(new ExpressionPredicate<TE>(predicate, true));
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> Null()
        {
            base.Null();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> NotNull()
        {
            base.NotNull();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> NotNullOrEmpty()
        {
            base.NotNullOrEmpty();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> NotNullOrWhitespace()
        {
            base.NotNullOrWhitespace();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> ShorterThan(int length)
        {
            Rule.Validator = new IsShorterThanPredicate(Rule.Target.ValueType, length);
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> DoesMatch(string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
        {
            base.DoesMatch(pattern, options, timeout);
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> DoesNotMatch(string pattern, RegexOptions? options = null, TimeSpan? timeout = null)
        {
            base.DoesNotMatch(pattern, options, timeout);
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> EnumIsCorrect()
        {
            base.EnumIsCorrect();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> EnumIsCorrect(Type enumType)
        {
            base.EnumIsCorrect(enumType);
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> EnumIsCorrect<T>() => EnumIsCorrect(typeof(T));

        public GenericValidationRuleBuilder<TE, TV> Between(TV minValue, TV maxValue) => Between(minValue, true, maxValue, true);

        public GenericValidationRuleBuilder<TE, TV> Between(TV minValue, bool minInclusive, TV maxValue, bool maxInclusive)
        {
            base.Must(new ValueIsBetweenPredicate(typeof(TV), minValue, minInclusive, maxValue, maxInclusive));
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> WithCode(int code)
        {
            base.WithCode(code);
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> WithMessage(string message)
        {
            base.WithMessage(message);
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> WhenNotNull()
        {
            WhenValue(new IsNotNullPredicate(Rule.Target.ValueType));
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> ValidateUsing(Type validatorType)
        {
            base.ValidateUsing(validatorType);
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> ValidateUsing(Type validatorType, object[] args)
        {
            base.ValidateUsing(validatorType, args);
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> ValidateUsing(IBaseValidator validator)
        {
            base.ValidateUsing(validator);
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> ValidateUsing<TX>() where TX : BaseValidator, new() => ValidateUsing(typeof(TX));

        public GenericValidationRuleBuilder<TE, TV> ValidateUsing<TX>(object[] args) where TX : BaseValidator => ValidateUsing(typeof(TX), args);

        public new GenericValidationRuleBuilder<TE, TV> EmailAddress()
        {
            base.EmailAddress();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> NotHTML()
        {
            base.NotHTML();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> NotSQLInjection()
        {
            base.NotSQLInjection();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> PhoneNumber()
        {
            base.PhoneNumber();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> CreditCardNumber()
        {
            base.CreditCardNumber();
            return this;
        }

        public new GenericValidationRuleBuilder<TE, TV> ServerOnly()
        {
            base.ServerOnly();
            return this;
        }

        public GenericValidationRuleBuilder<TE, TV> Otherwise()
        {
            ValidationRule newRule = Validator.Rule(Rule.Target).Rule;

            if (Rule.WhenValue == null && Rule.UnlessValue == null && Rule.WhenEntity == null && Rule.UnlessEntity == null)
                throw new InvalidOperationException("The rule does not have any when or unless conditions, so otherwise rule will never be executed");

            if (Rule.WhenValue != null)
                newRule.UnlessValue = Rule.WhenValue;
            else if (Rule.UnlessValue != null)
                newRule.WhenValue = Rule.UnlessValue;
            else if (Rule.WhenEntity != null)
                newRule.UnlessEntity = Rule.WhenEntity;
            else if (Rule.UnlessEntity != null)
                newRule.WhenEntity = Rule.UnlessEntity;

            newRule.IgnoreOnClient = Rule.IgnoreOnClient;

            GenericValidationRuleBuilder<TE, TV> newBuilder = new GenericValidationRuleBuilder<TE, TV>(Validator, newRule);

            return newBuilder;
        }

        public GenericValidationRuleBuilder<TE, TV> Also()
        {
            ValidationRule newRule = Validator.Rule(Rule.Target).Rule;
            newRule.WhenValue = Rule.WhenValue;
            newRule.WhenEntity = Rule.WhenEntity;
            newRule.UnlessValue = Rule.UnlessValue;
            newRule.UnlessEntity = Rule.UnlessEntity;

            newRule.IgnoreOnClient = Rule.IgnoreOnClient;

            GenericValidationRuleBuilder<TE, TV> newBuilder = new GenericValidationRuleBuilder<TE, TV>(Validator, newRule);

            return newBuilder;
        }
    }
}