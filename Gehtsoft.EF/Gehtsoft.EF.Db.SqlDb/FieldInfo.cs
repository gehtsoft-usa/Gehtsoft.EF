using System;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The information about the resultset column.
    /// </summary>
    public class FieldInfo
    {
        /// <summary>
        /// The column name.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The column data type
        /// </summary>
        public Type DataType { get; }
        /// <summary>
        /// The column index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="index"></param>
        public FieldInfo(string name, Type type, int index)
        {
            Name = name;
            DataType = type;
            Index = index;
        }
    }
}
