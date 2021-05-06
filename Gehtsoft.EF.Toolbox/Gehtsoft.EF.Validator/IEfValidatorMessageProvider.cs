using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Validator
{
    public interface IEfValidatorMessageProvider
    {
        string GetMessage(EntityDescriptor entityDescriptor, TableDescriptor.ColumnInfo column, int validationErrorCode);
    }

    public class DefaultEfValidatorMessageProvider : IEfValidatorMessageProvider
    {
        private static readonly Dictionary<string, Dictionary<int, string>> gDefaultCodes = new Dictionary<string, Dictionary<int, string>>()
        {
            {
                "en",
                new Dictionary<int, string>()
                {
                    {-1, "Unknown Error"},
                    {(int) EfValidationErrorCode.NullValue, "The value must not be empty"},
                    {(int) EfValidationErrorCode.DateIsOutRange, "The date is out of the supported range"},
                    {(int) EfValidationErrorCode.EnumerationValueIsInvalid, "The value selected is not supported"},
                    {(int) EfValidationErrorCode.NumberIsOutOfRange, "The number is out of the supported range"},
                    {(int) EfValidationErrorCode.ReferenceDoesNotExists, "The value selected does not exists"},
                    {(int) EfValidationErrorCode.StringIsTooLong, "The string is too long"},
                    {(int) EfValidationErrorCode.TimestampIsOutOfRange, "The is out of the supported range"},
                    {(int) EfValidationErrorCode.ValueIsNotUnique, "The value is not unique"},
                }
            },
            {
                "ru",
                new Dictionary<int, string>()
                {
                    {-1, "Неизвестная ошибка"},
                    {(int) EfValidationErrorCode.NullValue, "Значение не должно быть пустым"},
                    {(int) EfValidationErrorCode.DateIsOutRange, "Дата выходит за пределы допустимого диапазона"},
                    {(int) EfValidationErrorCode.EnumerationValueIsInvalid, "Выбранное значение не поддерживается"},
                    {(int) EfValidationErrorCode.NumberIsOutOfRange, "Число выходит за пределы допустимого диапазона"},
                    {(int) EfValidationErrorCode.ReferenceDoesNotExists, "Выбранное значение не существует"},
                    {(int) EfValidationErrorCode.StringIsTooLong, "Строка слишком длинная"},
                    {(int) EfValidationErrorCode.TimestampIsOutOfRange, "Дата выходит за пределы допустимого диапазона"},
                    {(int) EfValidationErrorCode.ValueIsNotUnique, "Значение не уникально"},
                }
            }
        };

        private readonly Dictionary<int, string> mDictionary;

        public DefaultEfValidatorMessageProvider(string language = "en")
        {
            if (!gDefaultCodes.TryGetValue(language, out mDictionary))
                mDictionary = gDefaultCodes["en"];
        }

        public string GetMessage(EntityDescriptor entityDescriptor, TableDescriptor.ColumnInfo column, int validationErrorCode)
        {
            if (!mDictionary.TryGetValue(validationErrorCode, out string s))
                s = mDictionary[-1];
            return s;
        }
    }
}
