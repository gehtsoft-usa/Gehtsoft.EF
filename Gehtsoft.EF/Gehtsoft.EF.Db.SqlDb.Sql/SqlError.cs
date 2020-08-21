using System;
using System.Runtime.Serialization;


namespace Gehtsoft.EF.Db.SqlDb.Sql
{

    /// <summary>
    /// Error occurred during parsing the lua
    /// </summary>
    [Serializable]
    public class SqlError : ISerializable
    {
        /// <summary>
        /// The name of the source
        /// </summary>
        public string SourceName { get; private set; }
        /// <summary>
        /// The line number inside the source
        /// </summary>
        public int SourceLine { get; private set; }
        /// <summary>
        /// The position within the line
        /// </summary>
        public int SourcePosition { get; private set; }
        /// <summary>
        /// The error message
        /// </summary>
        public string ErrorMessage { get; private set; }

        internal SqlError(string sourceName, int sourceLine, int sourcePosition, string errorMessage)
        {
            SourceName = sourceName;
            SourceLine = sourceLine;
            SourcePosition = sourcePosition;
            ErrorMessage = errorMessage;
        }

        protected SqlError(SerializationInfo info, StreamingContext context)
        {
            SourceName = (string)info.GetValue("source", typeof(string));
            ErrorMessage = (string)info.GetValue("error", typeof(string));
            SourceLine = (int)info.GetValue("line", typeof(int));
            SourcePosition = (int)info.GetValue("position", typeof(int));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("source", SourceName);
            info.AddValue("error", ErrorMessage);
            info.AddValue("line", SourceLine);
            info.AddValue("position", SourcePosition);
        }

        public override string ToString()
        {
            return $"{SourceName} {SourceLine}:{SourcePosition} - {ErrorMessage}";
        }
    }
}
