using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Gehtsoft.EF.Validator")]
[assembly: InternalsVisibleTo("Gehtsoft.EF.Mapper.Validator")]


namespace Gehtsoft.Validator
{
    public class BaseValidator : IBaseValidator, IAsyncBaseValidator
    {
        public Type ValidateType { get; private set; }
        protected List<ValidationRule> mRules = new List<ValidationRule>();
        protected IValidationPredicate mWhen;
        protected IValidationPredicate mUnless;

        public int Count => mRules.Count;
        public ValidationRule this[int index] => mRules[index];
        protected bool SkipIfNull { get; set; } = false;

        public BaseValidator(Type validateType)
        {
            ValidateType = validateType;
            PropertyInfo[] properties = validateType.GetTypeInfo().GetProperties();
            foreach (var property in properties)
            {
                IValidationPredicate predicate = null;
                ValidatorAttributeBase baseAttribute = null;

                {
                    MustMatchAttribute attribute = property.GetCustomAttribute<MustMatchAttribute>();
                    if (attribute != null)
                    {
                        predicate = new DoesMatchPredicate(typeof(string), attribute.Pattern);
                        baseAttribute = attribute;
                        AddRule(property.Name, property.PropertyType, baseAttribute.ForElement, predicate, baseAttribute.WidthCode, baseAttribute.WithMessage);
                    }
                }

                {
                    MustBeInRangeAttribute attribute = property.GetCustomAttribute<MustBeInRangeAttribute>();
                    if (attribute != null)
                    {
                        predicate = new ValueIsBetweenPredicate(property.PropertyType, attribute.Mininum, attribute.MinimumInclusive, attribute.Maximum, attribute.MaximumInclusive);
                        baseAttribute = attribute;
                        AddRule(property.Name, property.PropertyType, baseAttribute.ForElement, predicate, baseAttribute.WidthCode, baseAttribute.WithMessage);
                    }
                }

                {
                    MustBeNotNullAttribute attribute = property.GetCustomAttribute<MustBeNotNullAttribute>();
                    if (attribute != null)
                    {
                        predicate = new IsNotNullPredicate(property.PropertyType);
                        baseAttribute = attribute;
                        AddRule(property.Name, property.PropertyType, baseAttribute.ForElement, predicate, baseAttribute.WidthCode, baseAttribute.WithMessage);
                    }
                }

                {
                    MustBeNotNullOrWhitespaceAttribute attribute = property.GetCustomAttribute<MustBeNotNullOrWhitespaceAttribute>();
                    if (attribute != null)
                    {
                        predicate = new IsNotNullOrWhitespacePredicate(property.PropertyType);
                        baseAttribute = attribute;
                        AddRule(property.Name, property.PropertyType, baseAttribute.ForElement, predicate, baseAttribute.WidthCode, baseAttribute.WithMessage);
                    }
                }

                {
                    MustBeNotEmptyAttribute attribute = property.GetCustomAttribute<MustBeNotEmptyAttribute>();
                    if (attribute != null)
                    {
                        predicate = new IsNotNullOrEmptyPredicate(property.PropertyType);
                        baseAttribute = attribute;
                        AddRule(property.Name, property.PropertyType, baseAttribute.ForElement, predicate, baseAttribute.WidthCode, baseAttribute.WithMessage);
                    }
                }

                {
                    MustBeShorterThanAttribute attribute = property.GetCustomAttribute<MustBeShorterThanAttribute>();
                    if (attribute != null)
                    {
                        predicate = new IsShorterThanPredicate(property.PropertyType, attribute.Length);
                        baseAttribute = attribute;
                        AddRule(property.Name, property.PropertyType, baseAttribute.ForElement, predicate, baseAttribute.WidthCode, baseAttribute.WithMessage);
                    }
                }
            }
        }

        protected void AddRule(string name, Type type, bool forElement, IValidationPredicate predicate, int? code, string message)
        {
            ValidationRuleBuilder builder = forElement ? RuleForAll(name) : RuleFor(name);
            builder.Must(predicate);
            if (code != null)
                builder.WithCode((int) code);
            if (message != null)
                builder.WithMessage(message);
        }


        public void When(IValidationPredicate predicate)
        {
            mWhen = predicate;
        }

        public void Unless(IValidationPredicate predicate)
        {
            mUnless = predicate;
        }

        public ValidationRuleBuilder RuleForEntity(string name = null)
        {
            ValidationRule rule = new ValidationRule(ValidateType, ValidateType) {Target = new EntityValidationTarget(ValidateType, name ?? "")};
            mRules.Add(rule);
            return new ValidationRuleBuilder(this, rule);
        }

        public ValidationRuleBuilder RuleFor(string propertyName) => Rule(new PropertyValidationTarget(ValidateType, propertyName));

        public ValidationRuleBuilder RuleForAll(string propertyName) => Rule(new PropertyValidationArrayTarget(ValidateType, propertyName));

        public ValidationRuleBuilder Rule(ValidationTarget target)
        {
            ValidationRule rule = new ValidationRule(ValidateType, target.ValueType) {Target = target};
            mRules.Add(rule);
            return new ValidationRuleBuilder(this, rule);
        }

        public ValidationResult Validate(object entity)
        {
            return ValidateCore(true, entity, null).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<ValidationResult> ValidateAsync(object entity, CancellationToken? token = null)
        {
            return ValidateCore(false, entity, token);
        }

        protected async Task<bool> CalculatePredicateAsync(IValidationPredicate predicate, object value, CancellationToken? token)
        {
            if (predicate == null)
                return false;
            if (predicate is IValidationPredicateAsync asyncPredicate)
                return await asyncPredicate.ValidateAsync(value, token);
            else
                return predicate.Validate(value);

        }

        protected virtual async Task<ValidationResult> ValidateCore(bool sync, object entity, CancellationToken? token)
        {
            ValidationResult result = new ValidationResult();

            if (token != null && token.Value.IsCancellationRequested)
                throw new OperationCanceledException();

            if (SkipIfNull && entity == null)
                return result;

            if (mWhen != null)
            {
                if (sync)
                {
                    if (!mWhen.Validate(entity))
                        return result;
                }
                else
                {
                    if (!(await CalculatePredicateAsync(mWhen, entity, token)))
                        return result;
                }
            }

            if (mUnless != null)
            {
                if (sync)
                {
                    if (mUnless.Validate(entity))
                        return result;
                }
                else
                {
                    if (await CalculatePredicateAsync(mUnless, entity, token))
                        return result;
                }
            }

            foreach (ValidationRule rule in mRules)
            {
                if (token != null && token.Value.IsCancellationRequested)
                    throw new OperationCanceledException();

                if (rule.WhenEntity != null)
                {
                    if (sync)
                    {
                        if (!rule.WhenEntity.Validate(entity))
                            continue;
                    }
                    else
                    {
                        if (!(await CalculatePredicateAsync(rule.WhenEntity, entity, token)))
                            continue;
                    }
                }

                if (rule.UnlessEntity != null)
                {
                    if (sync)
                    {
                        if (rule.UnlessEntity.Validate(entity))
                            continue;
                    }
                    else
                    {
                        if (await CalculatePredicateAsync(rule.UnlessEntity, entity, token))
                            continue;
                    }
                }

                if (rule.Target != null)
                {
                    if (rule.Target.IsSingleValue)
                    {
                        if (sync)
                            ValidateOneValue(true, result, rule, entity, rule.Target.First(entity), rule.Target.ValueType, null).ConfigureAwait(false).GetAwaiter().GetResult();
                        else
                            await ValidateOneValue(false, result, rule, entity, rule.Target.First(entity), rule.Target.ValueType, token);
                    }
                    else
                    {
                        ValidationTarget.ValidationValue[] all = rule.Target.All(entity);
                        if (all != null && all.Length > 0)
                        {
                            foreach (ValidationTarget.ValidationValue v in all)
                            {
                                if (sync)
                                    ValidateOneValue(true, result, rule, entity, v, rule.Target.ValueType, null).ConfigureAwait(false).GetAwaiter().GetResult();
                                else
                                    await ValidateOneValue(false, result, rule, entity, v, rule.Target.ValueType, token);
                            }
                        }
                    }
                }
            }

            return result;
        }

        protected virtual async Task ValidateOneValue(bool sync, ValidationResult result, ValidationRule rule, object entity, ValidationTarget.ValidationValue value, Type valueType, CancellationToken? token)
        {
            if (rule.WhenValue != null)
                if (!rule.WhenValue.Validate(value.Value))
                    return;
            if (rule.UnlessValue != null)
                if (rule.UnlessValue.Validate(value.Value))
                    return;

            if (rule.Validator != null)
            {
                bool success;
                if (sync)
                    success = rule.Validator.Validate(value.Value);
                else
                {
                    if (rule.Validator is IValidationPredicateAsync asyncValidator)
                        success = await asyncValidator.ValidateAsync(value.Value, token);
                    else
                        success = rule.Validator.Validate(value.Value);
                }
                if (!success)
                {
                    result.Failures.Add(new ValidationFailure()
                    {
                        Code = rule.Code,
                        Message = rule.Message,
                        Name = value.Name,
                        Path = value.Name,
                    });
                }
            }

            if (rule.HasAnotherValidator)
            {
                ValidationResult anotherResult;

                if (sync)
                    anotherResult = rule.AnotherValidator.Validate(value.Value);
                else
                {
                    if (rule.AnotherValidator is IAsyncBaseValidator asyncValidator)
                        anotherResult = await asyncValidator.ValidateAsync(value.Value);
                    else
                        anotherResult = rule.AnotherValidator.Validate(value.Value);
                }

                if (!anotherResult.IsValid)
                {
                    foreach (ValidationFailure anotherFailure in anotherResult.Failures)
                    {
                        result.Failures.Add(new ValidationFailure()
                        {
                            Code = anotherFailure.Code,
                            Message = anotherFailure.Message,
                            Name = anotherFailure.Name,
                            Path = string.IsNullOrEmpty(value.Name) ? anotherFailure.Path : $"{value.Name}.{anotherFailure.Path}",
                        });
                    }
                }
            }
        }

        public int RulesCount => mRules.Count;
        public ValidationRule GetRule(int index) => mRules[index];

        public IEnumerator<IValidationRule> GetEnumerator() => mRules.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}