using System;
using System.Runtime.Serialization;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// Codes for `EfSqlException`
    /// </summary>
    public enum EfExceptionCode
    {
        NotEntity,
        NoPrimaryKeyInTable,
        NoTableToConnect,
        NoColumnToConnect,
        NoTableInQuery,
        InvalidOperation,
        PropertyNotFound,
        ColumnNotFound,
        IncorrectJoin,
        NestingTransactionsNotSupported,
        FeatureNotSupported,
        WhereBracketIsEmpty,
        WrongOperator,
        UnknownOperator,
        TypeIsUnsupported,
    }

    internal class EfExceptionMessages
    {
        public static EfExceptionMessages Inst { get; } = new EfExceptionMessages();

        public string this[EfExceptionCode code]
        {
            get
            {
                switch (code)
                {
                    case EfExceptionCode.NoPrimaryKeyInTable:
                        return "The table {0} has no primary key";

                    case EfExceptionCode.NoTableToConnect:
                        return "Cannot find a table in the query to connect the table specified";

                    case EfExceptionCode.NoColumnToConnect:
                        return "Cannot find a column to connect the table specified";

                    case EfExceptionCode.NoTableInQuery:
                        return "The table requested is not found in the query";

                    case EfExceptionCode.NotEntity:
                        return "Type {0} is not an entity";

                    case EfExceptionCode.ColumnNotFound:
                        return "The column or property {0} is not found";

                    case EfExceptionCode.PropertyNotFound:
                        return "The property {0} is not found";

                    case EfExceptionCode.IncorrectJoin:
                        return "The join operation requested is incorrect";

                    case EfExceptionCode.InvalidOperation:
                        return "Operation requested is not supported";

                    case EfExceptionCode.NestingTransactionsNotSupported:
                        return "Nesting transactions aren't supported";

                    case EfExceptionCode.FeatureNotSupported:
                        return "Requested feature isn't supported";

                    case EfExceptionCode.WhereBracketIsEmpty:
                        return "Bracket group in where is empty";

                    case EfExceptionCode.WrongOperator:
                        return "The incorrect operator is chosen for this argument";

                    case EfExceptionCode.UnknownOperator:
                        return "The operator is unknown or unsupported by the target platform";

                    case EfExceptionCode.TypeIsUnsupported:
                        return "The data type {0} isn't supported";

                    default:
                        return $"Unknown exception {code}";
                }
            }
        }
    }

    /// <summary>
    /// EF exception
    /// </summary>
    [Serializable]
    public class EfSqlException : Exception
    {
        /// <summary>
        /// The code of the error
        /// </summary>
        public EfExceptionCode ErrorCode { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="code"></param>
        /// <param name="args"></param>
        public EfSqlException(EfExceptionCode code, params object[] args) : base(string.Format(EfExceptionMessages.Inst[code], args))
        {
            ErrorCode = code;
        }

        protected EfSqlException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}