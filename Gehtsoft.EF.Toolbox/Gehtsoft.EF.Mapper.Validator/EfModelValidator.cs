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
    public class EfModelValidator<T> : AbstractValidator<T>
    {
        internal SqlDbLanguageSpecifics LanguageSpecifics { get; }
        internal IValidatorConnectionFactory ConnectionFactory { get; }

        public EfModelValidator(SqlDbLanguageSpecifics specifics = null, IValidatorConnectionFactory connectionFactory = null)
        {
            LanguageSpecifics = specifics;
            ConnectionFactory = connectionFactory;

            PropertyInfo[] properties = typeof(T).GetTypeInfo().GetProperties();

            foreach (PropertyInfo property in properties)
            {
                {
                    MustHaveValidDbSizeAttribute attribute = property.GetCustomAttribute<MustHaveValidDbSizeAttribute>();
                    if (attribute != null)
                    {
                        ValidationRuleBuilder builder = RuleFor(property.Name).MustHaveValidDbSize().WhenNotNull();
                        if (attribute.WidthCode != null)
                            builder.WithCode((int)attribute.WidthCode);
                        if (attribute.WithMessage != null)
                            builder.WithMessage(attribute.WithMessage);
                    }
                }

                {
                    MustBeInDbValueRangeAttribute attribute = property.GetCustomAttribute<MustBeInDbValueRangeAttribute>();
                    if (attribute != null)
                    {
                        ValidationRuleBuilder builder = RuleFor(property.Name).MustBeInValidDbRange().WhenNotNull();
                        if (attribute.WidthCode != null)
                            builder.WithCode((int)attribute.WidthCode);
                        if (attribute.WithMessage != null)
                            builder.WithMessage(attribute.WithMessage);
                    }
                }

                {
                    MustBeUniqueAttribute attribute = property.GetCustomAttribute<MustBeUniqueAttribute>();
                    if (attribute != null)
                    {
                        ValidationRuleBuilder builder = RuleFor(property.Name).MustBeUnique().WhenNotNull();
                        if (attribute.WidthCode != null)
                            builder.WithCode((int)attribute.WidthCode);
                        if (attribute.WithMessage != null)
                            builder.WithMessage(attribute.WithMessage);
                    }
                }

                {
                    MustExistAttribute attribute = property.GetCustomAttribute<MustExistAttribute>();
                    if (attribute != null)
                    {
                        ValidationRuleBuilder builder = RuleFor(property.Name).MustExists().WhenNotNull();
                        if (attribute.WidthCode != null)
                            builder.WithCode((int)attribute.WidthCode);
                        if (attribute.WithMessage != null)
                            builder.WithMessage(attribute.WithMessage);
                    }
                }
            }
        }

        public void ValidateModel(IEfValidatorMessageProvider messageProvider = null, bool aspNetValidation = false)
        {
            Type modelType = typeof(T);
            MapEntityAttribute mapEntitAttribute = modelType.GetTypeInfo().GetCustomAttribute<MapEntityAttribute>();
            if (mapEntitAttribute == null)
                throw new InvalidOperationException("The model isn't associate with the entity");

            EntityDescriptor entity = AllEntities.Inst[mapEntitAttribute.EntityType];
            IMap map = MapFactory.GetMap(modelType, mapEntitAttribute.EntityType);

            foreach (var property in map.Mappings)
            {
                if (property.Target is EntityPropertyAccessor entityProperty)
                {
                    if (!entityProperty.ColumnInfo.Nullable)
                    {
                        if (entityProperty.ColumnInfo.PropertyAccessor.PropertyType == typeof(string) && aspNetValidation)
                            RuleFor(property.Source.Name).NotNullOrEmpty().WithCode((int)EfValidationErrorCode.NullValue).WithMessage(messageProvider?.GetMessage(entity, entityProperty.ColumnInfo, (int)EfValidationErrorCode.NullValue));
                        else
                            RuleFor(property.Source.Name).NotNull().WithCode((int)EfValidationErrorCode.NullValue).WithMessage(messageProvider?.GetMessage(entity, entityProperty.ColumnInfo, (int)EfValidationErrorCode.NullValue));
                    }

                    if ((entityProperty.ColumnInfo.DbType == DbType.AnsiString || entityProperty.ColumnInfo.DbType == DbType.AnsiStringFixedLength || entityProperty.ColumnInfo.DbType == DbType.String || entityProperty.ColumnInfo.DbType == DbType.StringFixedLength) && entityProperty.ColumnInfo.Size > 0)
                        RuleFor(property.Source.Name).MustHaveValidDbSize().WhenNotNull()
                            .WithCode((int)EfValidationErrorCode.StringIsTooLong)
                            .WithMessage(messageProvider?.GetMessage(entity, entityProperty.ColumnInfo, (int)EfValidationErrorCode.StringIsTooLong));

                    if (entityProperty.ColumnInfo.DbType == DbType.Double || entityProperty.ColumnInfo.DbType == DbType.Decimal)
                        RuleFor(property.Source.Name).MustBeInValidDbRange().WhenNotNull()
                            .WithCode((int)EfValidationErrorCode.NumberIsOutOfRange)
                            .WithMessage(messageProvider?.GetMessage(entity, entityProperty.ColumnInfo, (int)EfValidationErrorCode.NumberIsOutOfRange));

                    if ((entityProperty.ColumnInfo.DbType == DbType.Date || entityProperty.ColumnInfo.DbType == DbType.DateTime) && LanguageSpecifics != null)
                        RuleFor(property.Source.Name).MustBeInValidDbRange().WhenNotNull()
                            .WithCode((int)EfValidationErrorCode.DateIsOutRange)
                            .WithMessage(messageProvider?.GetMessage(entity, entityProperty.ColumnInfo, (int)EfValidationErrorCode.DateIsOutRange));

                    if ((entityProperty.ColumnInfo.Unique) && ConnectionFactory != null)
                        RuleFor(property.Source.Name).MustBeUnique().WhenNotNull()
                            .WithCode((int)EfValidationErrorCode.ValueIsNotUnique)
                            .WithMessage(messageProvider?.GetMessage(entity, entityProperty.ColumnInfo, (int)EfValidationErrorCode.ValueIsNotUnique));

                    if ((entityProperty.ColumnInfo.ForeignKey) && ConnectionFactory != null)
                        RuleFor(property.Source.Name).MustExists()
                            .WithCode((int)EfValidationErrorCode.ReferenceDoesNotExists)
                            .WhenNotNull().WithMessage(messageProvider?.GetMessage(entity, entityProperty.ColumnInfo, (int)EfValidationErrorCode.ReferenceDoesNotExists));
                }
            }
        }
    }
}
