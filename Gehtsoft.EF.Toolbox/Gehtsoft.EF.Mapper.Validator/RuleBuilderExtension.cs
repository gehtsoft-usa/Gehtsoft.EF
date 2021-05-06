using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Mapper.Validator
{
    public static class RuleBuilderExtension
    {
        public static ValidationRuleBuilder MustHaveValidDbSize(this ValidationRuleBuilder builder)
        {
            AddValidDbSizeRule(builder);
            return builder;
        }

        public static GenericValidationRuleBuilder<TE, TV> MustHaveValidDbSize<TE, TV>(this GenericValidationRuleBuilder<TE, TV> builder)
        {
            AddValidDbSizeRule(builder);
            return builder;
        }

        public static ValidationRuleBuilder MustBeInValidDbRange(this ValidationRuleBuilder builder)
        {
            AddValidDbValueRange(builder);
            return builder;
        }

        public static GenericValidationRuleBuilder<TE, TV> MustBeInValidDbRange<TE, TV>(this GenericValidationRuleBuilder<TE, TV> builder)
        {
            AddValidDbValueRange(builder);
            return builder;
        }

        public static ValidationRuleBuilder MustBeUnique(this ValidationRuleBuilder builder)
        {
            AddUnique(builder);
            return builder;
        }

        public static GenericValidationRuleBuilder<TE, TE> MustBeUnique<TE, TV>(this GenericValidationRuleBuilder<TE, TV> builder)
        {
            AddUnique(builder);
            GenericValidationRuleBuilder<TE, TE> newBuilder = new GenericValidationRuleBuilder<TE, TE>(builder.Validator, builder.Rule);
            return newBuilder;
        }

        public static ValidationRuleBuilder MustExists(this ValidationRuleBuilder builder)
        {
            AddExists(builder);
            return builder;
        }

        public static GenericValidationRuleBuilder<TE, TV> MustExists<TE, TV>(this GenericValidationRuleBuilder<TE, TV> builder)
        {
            AddExists(builder);
            return builder;
        }

        private static void AddValidDbSizeRule(ValidationRuleBuilder builder)
        {
            Type modelType = builder.Rule.EntityType;
            MapEntityAttribute mapEntitAttribute = modelType.GetTypeInfo().GetCustomAttribute<MapEntityAttribute>();
            if (mapEntitAttribute == null)
                throw new ArgumentException("The model isn't associate with the entity");

            _ = AllEntities.Inst[mapEntitAttribute.EntityType];
            IMap map = MapFactory.GetMap(modelType, mapEntitAttribute.EntityType);

            if (builder.Rule.Target.IsProperty)
            {
                foreach (var property in map.Mappings)
                {
                    if (property.Target is EntityPropertyAccessor accessor && property.Source is ClassPropertyAccessor)
                    {
                        EntityPropertyAccessor propertyTarget = accessor;

                        if (property.Source.Name == builder.Rule.Target.PropertyName)
                        {
                            if ((propertyTarget.ColumnInfo.DbType == DbType.AnsiString ||
                                 propertyTarget.ColumnInfo.DbType == DbType.AnsiStringFixedLength ||
                                 propertyTarget.ColumnInfo.DbType == DbType.String ||
                                 propertyTarget.ColumnInfo.DbType == DbType.StringFixedLength) && propertyTarget.ColumnInfo.Size > 0)
                            {
                                builder.ShorterThan(propertyTarget.ColumnInfo.Size + 1).WhenNotNull();
                            }

                            return;
                        }
                    }
                }
            }

            throw new InvalidOperationException("The model property is not mapped to an entity");
        }

        private static Type GetModelValidatorType(ValidationRuleBuilder builder) => typeof(EfModelValidator<>).MakeGenericType(new Type[] { builder.Rule.EntityType });

        private static SqlDbLanguageSpecifics GetLanguageSpecifics(ValidationRuleBuilder builder)
        {
            Type modelValidatorType = GetModelValidatorType(builder);
            if (modelValidatorType.IsInstanceOfType(builder.Validator))
            {
                PropertyInfo languageSpecificsProperty = builder.Validator.GetType().GetProperty(nameof(EfModelValidator<object>.LanguageSpecifics), BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                return languageSpecificsProperty?.GetValue(builder.Validator) as SqlDbLanguageSpecifics;
            }
            return null;
        }

        private static IValidatorConnectionFactory GetConnectionFactory(ValidationRuleBuilder builder)
        {
            Type modelValidatorType = GetModelValidatorType(builder);
            if (modelValidatorType.IsInstanceOfType(builder.Validator))
            {
                PropertyInfo connectionFactoryProperty = builder.Validator.GetType().GetProperty(nameof(EfModelValidator<object>.ConnectionFactory), BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance);
                return connectionFactoryProperty?.GetValue(builder.Validator) as IValidatorConnectionFactory;
            }
            return null;
        }

        private static void AddValidDbValueRange(ValidationRuleBuilder builder)
        {
            Type modelType = builder.Rule.EntityType;
            MapEntityAttribute mapEntitAttribute = modelType.GetTypeInfo().GetCustomAttribute<MapEntityAttribute>();
            if (mapEntitAttribute == null)
                throw new ArgumentException("The model isn't associate with the entity");

            _ = AllEntities.Inst[mapEntitAttribute.EntityType];
            IMap map = MapFactory.GetMap(modelType, mapEntitAttribute.EntityType);

            if (builder.Rule.Target.IsProperty)
            {
                foreach (var property in map.Mappings)
                {
                    if (property.Target is EntityPropertyAccessor propertyTarget)
                    {
                        if (property.Source.Name == builder.Rule.Target.PropertyName)
                        {
                            if ((propertyTarget.ColumnInfo.DbType == DbType.Double ||
                                 propertyTarget.ColumnInfo.DbType == DbType.Decimal) && propertyTarget.ColumnInfo.Size > 0)
                            {
                                double max = Math.Pow(10, propertyTarget.ColumnInfo.Size - propertyTarget.ColumnInfo.Precision);
                                builder.Must(new ValueIsBetweenPredicate(typeof(double), -max, false, max, false)).WhenNotNull();
                            }
                            else if (propertyTarget.ColumnInfo.DbType == DbType.Date)
                            {
                                SqlDbLanguageSpecifics specifics = GetLanguageSpecifics(builder);
                                if (specifics != null)
                                    builder.Must(new ValueIsBetweenPredicate(typeof(DateTime), specifics.MinDate, true, specifics.MaxDate, true)).WhenNotNull();
                            }
                            else if (propertyTarget.ColumnInfo.DbType == DbType.DateTime)
                            {
                                SqlDbLanguageSpecifics specifics = GetLanguageSpecifics(builder);
                                if (specifics != null)
                                    builder.Must(new ValueIsBetweenPredicate(typeof(DateTime), specifics.MinTimestamp, true, specifics.MaxTimestamp, true)).WhenNotNull();
                            }

                            return;
                        }
                    }
                }
            }
            throw new InvalidOperationException("The model property is not mapped to an entity");
        }

        private static void AddUnique(ValidationRuleBuilder builder)
        {
            Type modelType = builder.Rule.EntityType;
            MapEntityAttribute mapEntitAttribute = modelType.GetTypeInfo().GetCustomAttribute<MapEntityAttribute>();

            if (mapEntitAttribute == null)
                throw new ArgumentException("The model isn't associate with the entity");

            _ = AllEntities.Inst[mapEntitAttribute.EntityType];
            IMap map = MapFactory.GetMap(modelType, mapEntitAttribute.EntityType);

            if (builder.Rule.Target.IsProperty)
            {
                PropertyInfo pk = null;
                foreach (var property in map.Mappings)
                {
                    if (property.Target is EntityPropertyAccessor propertyTarget && property.Source is ClassPropertyAccessor propertySource)
                    {
                        if (propertyTarget.ColumnInfo.PrimaryKey)
                            pk = propertySource.PropertyInfo;
                    }
                }

                foreach (var property in map.Mappings)
                {
                    if (property.Target is EntityPropertyAccessor propertyTarget && property.Source is ClassPropertyAccessor propertySource)
                    {
                        if (propertySource.Name == builder.Rule.Target.PropertyName)
                        {
                            ValidationTarget oldTarget = builder.Rule.Target;
                            builder.ReplaceTarget(new EntityValidationTarget(builder.Rule.EntityType, builder.Rule.Target.TargetName));
                            IValidatorConnectionFactory factory = GetConnectionFactory(builder);
                            if (factory != null)
                                builder.Must(new IsModelUniquePredicate(pk, propertySource.PropertyInfo, factory, mapEntitAttribute.EntityType, propertyTarget.ColumnInfo));

                            if (builder.Rule.WhenValue != null)
                            {
                                IValidationPredicate oldPredicate = builder.Rule.WhenValue;
                                builder.Rule.WhenValue = new FunctionPredicate<object>(o =>
                                {
                                    ValidationTarget.ValidationValue ov = oldTarget.First(o);
                                    bool rc = oldPredicate.Validate(ov?.Value);
                                    return rc;
                                });
                            }
                            else if (builder.Rule.UnlessValue != null)
                            {
                                IValidationPredicate oldPredicate = builder.Rule.UnlessValue;
                                builder.Rule.UnlessValue = new FunctionPredicate<object>(o => oldPredicate.Validate(oldTarget.First(o)));
                            }
                            else if (builder.Rule.WhenEntity == null && builder.Rule.UnlessEntity == null)
                                builder.Rule.WhenValue = new IsNotNullPredicate(typeof(object));
                            return;
                        }
                    }
                }
            }

            throw new InvalidOperationException("The model property is not mapped to an entity");
        }

        private static void AddExists(ValidationRuleBuilder builder)
        {
            Type modelType = builder.Rule.EntityType;
            MapEntityAttribute mapEntitAttribute = modelType.GetTypeInfo().GetCustomAttribute<MapEntityAttribute>();
            if (mapEntitAttribute == null)
                throw new ArgumentException("The model isn't associate with the entity");

            _ = AllEntities.Inst[mapEntitAttribute.EntityType];
            IMap map = MapFactory.GetMap(modelType, mapEntitAttribute.EntityType);

            if (builder.Rule.Target.IsProperty)
            {
                foreach (var property in map.Mappings)
                {
                    if (property.Target is EntityPropertyAccessor propertyTarget)
                    {
                        if (property.Source.Name == builder.Rule.Target.PropertyName)
                        {
                            IValidatorConnectionFactory factory = GetConnectionFactory(builder);
                            if (factory != null)
                                builder.Must(new ReferenceExistsPredicate(factory, mapEntitAttribute.EntityType, propertyTarget.ColumnInfo, true)).WhenNotNull();
                            return;
                        }
                    }
                }
            }

            throw new InvalidOperationException("The model property is not mapped to an entity");
        }

        [Obsolete("The method is replaced with correctly spelled MustBeUnique method")]
        public static ValidationRuleBuilder MustBeUnqiue(this ValidationRuleBuilder builder) => MustBeUnique(builder);

        [Obsolete("The method is replaced with correctly spelled MustBeUnique method")]
        public static GenericValidationRuleBuilder<TE, TE> MustBeUnqiue<TE, TV>(this GenericValidationRuleBuilder<TE, TV> builder) => MustBeUnique(builder);
    }
}
