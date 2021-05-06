using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Bson
{
    public enum BsonExceptionCode
    {
        TypeIsNotEntity,
        TypeIsNotSupported,
        NoPk
    }

    public class BsonException : Exception
    {
        public BsonExceptionCode Code { get; }

        public override string Message
        {
            get
            {
                switch (Code)
                {
                    case BsonExceptionCode.TypeIsNotEntity:
                        return "The type is not an entity type";
                    case BsonExceptionCode.TypeIsNotSupported:
                        return "The property type is not supported";
                    case BsonExceptionCode.NoPk:
                        return "The entity type has no primary key to reference";
                    default:
                        return "Unknown code";
                }
            }
        }

        public BsonException(BsonExceptionCode code) : base()
        {
            Code = code;
        }
    }
}
