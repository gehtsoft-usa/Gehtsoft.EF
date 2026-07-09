using System;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>The exception thrown when WKT or WKB input cannot be parsed into a geometry.</summary>
    public class GeoFormatException : FormatException
    {
        /// <summary>Initializes a new instance of the <see cref="GeoFormatException"/> class.</summary>
        public GeoFormatException()
        {
        }

        /// <summary>Initializes a new instance with the specified message.</summary>
        /// <param name="message">The message that describes the parse error.</param>
        public GeoFormatException(string message) : base(message)
        {
        }

        /// <summary>Initializes a new instance with the specified message and inner exception.</summary>
        /// <param name="message">The message that describes the parse error.</param>
        /// <param name="innerException">The underlying exception that caused this error.</param>
        public GeoFormatException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
