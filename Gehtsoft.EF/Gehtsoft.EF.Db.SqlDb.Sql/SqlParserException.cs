using System;
using System.Runtime.Serialization;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    /// <summary>
    /// Exception thrown when the parsing is failed
    /// </summary>
    [Serializable]
    public class SqlParserException : Exception
    {
        /// <summary>
        /// The collection of errors
        /// </summary>
        public SqlErrorCollection Errors { get; private set; }

        /// <summary>
        /// The aggregate error message
        /// </summary>
        private readonly string mMessage;

        public override string Message => mMessage;

        internal SqlParserException(SqlError error) : this(new SqlErrorCollection() { error })
        {
        }


        internal SqlParserException(SqlErrorCollection errors)
        {
            Errors = errors;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Sql compilation failed:");
            foreach (var e in errors)
                builder.AppendLine(e.ToString());
            mMessage = builder.ToString();
        }

        protected SqlParserException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            mMessage = (string)info.GetValue("aggmessage", typeof(string));
            Errors = (SqlErrorCollection)info.GetValue("errors", typeof(SqlErrorCollection));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("aggmessage", mMessage);
            info.AddValue("errors", Errors);
        }
    }

}
