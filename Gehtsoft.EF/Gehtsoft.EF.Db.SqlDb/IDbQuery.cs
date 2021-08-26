using System;
using System.Data;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The interface to a query object
    /// </summary>
    public interface IDbQuery : IDisposable
    {
        /// <summary>
        /// The flag indicating whether the query is an insert query.
        /// </summary>
        bool IsInsert { get; }

        /// <summary>
        /// The flag indicating whether the query has a resultset to read.
        /// </summary>
        bool CanRead { get; }

        /// <summary>
        /// The specific rules for the connection.
        /// </summary>
        SqlDbLanguageSpecifics LanguageSpecifics { get; }

        /// <summary>
        /// Binds a null value.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        void BindNull(string name, DbType type);
        /// <summary>
        /// Binds an output parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        void BindOutputParam(string name, DbType type);
        /// <summary>
        /// Binds a parameters.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="direction"></param>
        /// <param name="value"></param>
        /// <param name="valueType"></param>
        void BindParam(string name, ParameterDirection direction, object value, Type valueType);
        /// <summary>
        /// Binds an input parameter (generic method). 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void BindParam<T>(string name, T value);
        /// <summary>
        /// Gets a parameter value.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object GetParamValue(string name, Type type);
        /// <summary>
        /// Gets a parameter value (generic method).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        T GetParamValue<T>(string name);

        /// <summary>
        /// Executes the query that returns no data.
        /// </summary>
        /// <returns>The method returns the number of the rows affect by the query.</returns>
        int ExecuteNoData();

        /// <summary>
        /// Executes query that return data.
        /// </summary>
        void ExecuteReader();
        
        /// <summary>
        /// Reads next row of the result set.
        /// </summary>
        /// <returns>The method returns `false` if there is no more rows to read.</returns>
        bool ReadNext();

        /// <summary>
        /// Checks whether the column is null (by column index).
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        bool IsNull(int column);
        
        /// <summary>
        /// Checks whether the column is null (by column name).
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        bool IsNull(string column);

        /// <summary>
        /// Gets the value (by column index, generic method).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="column"></param>
        /// <returns></returns>
        T GetValue<T>(int column);
        
        /// <summary>
        /// Gets the value (by column name, generic method).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="column"></param>
        /// <returns></returns>
        T GetValue<T>(string column);

        /// <summary>
        /// Gets the value by column index.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object GetValue(int column, Type type);

        /// <summary>
        /// Gets the value by column name.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        object GetValue(string column, Type type);

        /// <summary>
        /// Finds field index by its name. 
        /// </summary>
        /// <param name="column"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        int FindField(string column, bool ignoreCase = false);
    }
}
