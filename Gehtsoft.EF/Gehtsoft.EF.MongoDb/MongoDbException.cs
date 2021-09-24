using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The exception code for mongo exceptions.
    /// </summary>
    public enum EfMongoDbExceptionCode
    {
        LogOpNotSupported,
        CmpOpNotSupported,
        PropertyNotFound,
        NotAnEntity,
        LogOpNotSame,
        FilterGroupIsEmpty,
        FilterIsIncomplete,
        NoRow,
        TypeDoesNotMatchQuery,
    }

    /// <summary>
    /// The exception related to Mongo.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class EfMongoDbException : Exception
    {
        /// <summary>
        /// The code of the exception
        /// </summary>
        public EfMongoDbExceptionCode Code { get; }

        /// <summary>
        /// The message.
        /// </summary>
        public override string Message
        {
            get
            {
                switch (Code)
                {
                    case EfMongoDbExceptionCode.LogOpNotSupported:
                        return "Logical operation isn't supported";
                    case EfMongoDbExceptionCode.CmpOpNotSupported:
                        return "Comparison operation isn't supported";
                    case EfMongoDbExceptionCode.NotAnEntity:
                        return "The object references is not an entity";
                    case EfMongoDbExceptionCode.PropertyNotFound:
                        return "The object references is not a property";
                    case EfMongoDbExceptionCode.LogOpNotSame:
                        return "All logical operations within the group must be the same";
                    case EfMongoDbExceptionCode.FilterGroupIsEmpty:
                        return "A filter group is empty";
                    case EfMongoDbExceptionCode.FilterIsIncomplete:
                        return "A filter is incomplete";
                    case EfMongoDbExceptionCode.NoRow:
                        return "No row has been read";
                    case EfMongoDbExceptionCode.TypeDoesNotMatchQuery:
                        return "Requested type does not match query type";
                    default:
                        return "Unknown code";
                }
            }
        }

        internal EfMongoDbException(EfMongoDbExceptionCode code) : base()
        {
            Code = code;
        }
    }
}

