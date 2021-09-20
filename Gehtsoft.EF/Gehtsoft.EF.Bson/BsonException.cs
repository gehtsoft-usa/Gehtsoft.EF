using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Bson
{
    /// <summary>
    /// The code of the BsonException.
    ///
    /// See also <see cref="BsonException"/>
    /// </summary>
    public enum BsonExceptionCode
    {
        TypeIsNotEntity,
        TypeIsNotSupported,
        NoPk
    }

    /// <summary>
    /// The entity/BSON conversion exception
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class BsonException : Exception
    {
        /// <summary>
        /// The exception code
        /// </summary>
        public BsonExceptionCode Code { get; }

        /// <summary>
        /// The message
        /// </summary>
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

        [DocgenIgnore]
        public BsonException(BsonExceptionCode code) : base()
        {
            Code = code;
        }
    }
}
