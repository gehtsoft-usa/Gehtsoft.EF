namespace Gehtsoft.EF.Validator
{
    public enum EfValidationErrorCode
    {
        StringIsTooLong = 1000,
        NumberIsOutOfRange = 1001,
        DateIsOutRange = 1002,
        TimestampIsOutOfRange = 1003,
        EnumerationValueIsInvalid = 1004,
        NullValue = 1005,
        ValueIsNotUnique = 1006,
        ReferenceDoesNotExists = 1007,
    }
}