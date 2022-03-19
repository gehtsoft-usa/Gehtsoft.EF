using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.OData
{
    public enum EfODataExceptionCode
    {
        UnknownOperator,
        BadPath,
        QueryOptionsFault,
        SkiptokenWithoutPagingLimit,
        SkiptokenWithoutId,
        UnknownEdmType,
        UnknownOrderByExpression,
        UnknownSingleValueNode,
        NoEntityInBuildQuery,
        ResourceNotFound,
        UnsupportedFormat,
        UnknownField,
    }

    internal class EfODataExceptionMessages
    {
        public static EfODataExceptionMessages Inst { get; } = new EfODataExceptionMessages();

        public string this[EfODataExceptionCode code]
        {
            get
            {
                switch (code)
                {
                    case EfODataExceptionCode.UnknownField:
                        return "Unknown Field {0}";

                    case EfODataExceptionCode.UnsupportedFormat:
                        return "Unsupported media type requested";

                    case EfODataExceptionCode.ResourceNotFound:
                        return "Resource not found for the segment {0}";

                    case EfODataExceptionCode.NoEntityInBuildQuery:
                        return "Not found entity in ODataToQuery.BuildQuery";

                    case EfODataExceptionCode.UnknownSingleValueNode:
                        return "Unknown SingleValueNode node";

                    case EfODataExceptionCode.UnknownOrderByExpression:
                        return "Unknown OrderBy expression type";

                    case EfODataExceptionCode.UnknownEdmType:
                        return "Unknown IEdmType type in OrderBy expression";

                    case EfODataExceptionCode.UnknownOperator:
                        return "The operator is unknown or unsupported by the target platform";

                    case EfODataExceptionCode.BadPath:
                        return "The operator is unknown or unsupported by the target platform";

                    case EfODataExceptionCode.QueryOptionsFault:
                        return "Query options $orderby, $inlinecount, $skip and $top cannot be applied to the requested resource";

                    case EfODataExceptionCode.SkiptokenWithoutPagingLimit:
                        return "A skip token can only be provided in a query request against an entity set when the entity set has a paging limit set";

                    case EfODataExceptionCode.SkiptokenWithoutId:
                        return "A skip token with the value only is used for the entity which is not a PK";

                    default:
                        return $"Unknown exception {code}";
                }
            }
        }
    }

    [Serializable]
    public class EfODataException : Exception
    {
        public EfODataExceptionCode ErrorCode { get; }

        public EfODataException(EfODataExceptionCode code, params object[] args) : base(string.Format(EfODataExceptionMessages.Inst[code], args))
        {
            ErrorCode = code;
        }

        protected EfODataException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
