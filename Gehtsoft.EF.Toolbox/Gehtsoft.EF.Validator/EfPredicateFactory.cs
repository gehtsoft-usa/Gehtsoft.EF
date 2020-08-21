using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Validator
{
    public static class EfPredicateFactory
    {
        public static void AddDbValidation<T>(this AbstractValidator<T> validator, SqlDbLanguageSpecifics specifics = null, IValidatorConnectionFactory connectionFactory = null, IEfValidatorMessageProvider messageProvider = null) => AddDbValidation(validator, typeof(T), specifics, connectionFactory, messageProvider);

        public static void AddDbValidation<T>(this AbstractValidator<T> validator, Type entityType, SqlDbLanguageSpecifics specifics = null, IValidatorConnectionFactory connectionFactory = null, IEfValidatorMessageProvider messageProvider = null)
        {
            EntityDescriptor descriptor = AllEntities.Inst[entityType];
            List<Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>> list = new List<Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>>();

            foreach (TableDescriptor.ColumnInfo column in descriptor.TableDescriptor)
            {
                if (!column.Nullable)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new IsNotNullPredicate(column.PropertyAccessor.PropertyType)).WithCode((int) EfValidationErrorCode.NullValue)));

                if ((column.DbType == DbType.String || column.DbType == DbType.AnsiString || column.DbType == DbType.StringFixedLength || column.DbType == DbType.AnsiStringFixedLength) && column.Size > 0)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new IsShorterThanPredicate(typeof(string), column.Size + 1)).WithCode((int) EfValidationErrorCode.StringIsTooLong)));

                if (column.DbType == DbType.Double && column.Size > 0)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new NumberPropertyRangePredicate(column.Size, column.Precision)).WithCode((int) EfValidationErrorCode.NumberIsOutOfRange)));

                if (column.DbType == DbType.Decimal && column.Size > 0)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new DecimalPropertyRangePredicate(column.Size, column.Precision)).WithCode((int) EfValidationErrorCode.NumberIsOutOfRange)));

                if (column.DbType == DbType.Date && specifics != null)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new ValueIsBetweenPredicate(typeof(DateTime), specifics.MinDate, true, specifics.MaxDate, true)).WithCode((int) EfValidationErrorCode.DateIsOutRange).UnlessValue(new IsNullPredicate(column.PropertyAccessor.PropertyType))));

                if (column.DbType == DbType.DateTime && specifics != null)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new ValueIsBetweenPredicate(typeof(DateTime), specifics.MinTimestamp, true, specifics.MaxTimestamp, true)).WithCode((int) EfValidationErrorCode.TimestampIsOutOfRange).UnlessValue(new IsNullPredicate(column.PropertyAccessor.PropertyType))));

                if (column.PropertyAccessor.PropertyType.GetTypeInfo().IsEnum)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new IsEnumValueCorrectPredicate(column.PropertyAccessor.PropertyType)).WithCode((int) EfValidationErrorCode.EnumerationValueIsInvalid)));
                else if (Nullable.GetUnderlyingType(column.PropertyAccessor.PropertyType)?.GetTypeInfo()?.IsEnum == true)
                    list.Add(new Tuple<TableDescriptor.ColumnInfo, ValidationRuleBuilder>(column, validator.RuleFor(column.PropertyAccessor.Name).Must(new IsEnumValueCorrectPredicate(Nullable.GetUnderlyingType(column.PropertyAccessor.PropertyType))).WithCode((int) EfValidationErrorCode.EnumerationValueIsInvalid).UnlessValue(new IsNullPredicate(column.PropertyAccessor.PropertyType))));

                if (connectionFactory != null)
                {
                    if (column.Unique)
                        validator.RuleForEntity(column.PropertyAccessor.Name).Must(new IsUniquePredicate(connectionFactory, validator.ValidateType, column)).WithCode((int) EfValidationErrorCode.ValueIsNotUnique);
                    if (column.ForeignKey)
                        validator.RuleFor(column.PropertyAccessor.Name).Must(new ReferenceExistsPredicate(connectionFactory, validator.ValidateType, column)).WithCode((int) EfValidationErrorCode.ReferenceDoesNotExists).UnlessValue(new IsNullPredicate(column.PropertyAccessor.PropertyType));
                }
            }

            if (messageProvider != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    string message = messageProvider.GetMessage(descriptor, list[i].Item1, list[i].Item2.Rule.Code);
                    if (message != null)
                        list[i].Item2.WithMessage(message);
                }
            }
        }



        public static IValidationPredicate[] GetPredicates(TableDescriptor.ColumnInfo column, SqlDbLanguageSpecifics specifics = null, Func<SqlDbConnection> connectionFactory = null)
        {
            List<IValidationPredicate> predicates = new List<IValidationPredicate>();;

            if (!column.Nullable)
                predicates.Add(new IsNotNullOrEmptyPredicate(column.PropertyAccessor.PropertyType));
            
            if ((column.DbType == DbType.String || column.DbType == DbType.AnsiString || column.DbType == DbType.StringFixedLength || column.DbType == DbType.AnsiStringFixedLength) && column.Size > 0)
                predicates.Add(new IsShorterThanPredicate(typeof(string), column.Size + 1));

            if (column.DbType == DbType.Double && column.Size > 0)
                predicates.Add(new NumberPropertyRangePredicate(column.Size, column.Precision));
            
            if (column.DbType == DbType.Decimal && column.Size > 0)
                predicates.Add(new DecimalPropertyRangePredicate(column.Size, column.Precision));

            if (column.DbType == DbType.Date && specifics != null)
                predicates.Add(new ValueIsBetweenPredicate(typeof(DateTime), specifics.MinDate, true, specifics.MaxDate, true));
            
            if (column.DbType == DbType.DateTime && specifics != null)
                predicates.Add(new ValueIsBetweenPredicate(typeof(DateTime), specifics.MinDate, true, specifics.MaxDate, true));

            if (column.PropertyAccessor.PropertyType.GetTypeInfo().IsEnum)
                predicates.Add(new IsEnumValueCorrectPredicate(column.PropertyAccessor.PropertyType));
            else if (Nullable.GetUnderlyingType(column.PropertyAccessor.PropertyType)?.GetTypeInfo()?.IsEnum == true)
                predicates.Add(new IsEnumValueCorrectPredicate(Nullable.GetUnderlyingType(column.PropertyAccessor.PropertyType)));

            return predicates.ToArray();
        }
    }
}
