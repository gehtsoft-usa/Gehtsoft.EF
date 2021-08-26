using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The database query.
    ///
    /// Use <see cref="SqlDbConnection"/> to create a query.
    ///
    /// Do not forget to dispose the query after use. Some DBs requires the previous
    /// query to be disposed before the next query is executed.
    /// </summary>
    public class SqlDbQuery : IDbQuery
    {
        protected DbCommand mCommand;
        protected DbDataReader mReader;

        /// <summary>
        /// Returns underlying ADO.NET command object.
        /// </summary>
        public IDbCommand Command => mCommand;

        /// <summary>
        /// Returns underlying ADO.NET reader.
        /// </summary>
        public IDataReader Reader => mReader;

        protected SqlDbLanguageSpecifics mSpecifics;

        /// <summary>
        /// Returns the flag indicating whether the query can read a row.
        /// </summary>
        public bool CanRead { get; protected set; }

        /// <summary>
        /// Returns the connection object associated with the query.
        /// </summary>
        public SqlDbConnection Connection { get; }

        /// <summary>
        /// Returns the command text builder.
        /// </summary>
        public StringBuilder CommandTextBuilder { get; private set; } = new StringBuilder();

        /// <summary>
        /// Returns the DB-specific rules.
        /// </summary>
        public SqlDbLanguageSpecifics LanguageSpecifics => mSpecifics;

        /// <summary>
        /// Returns the flag indicating whether the command is insert.
        /// </summary>
        public bool IsInsert => CommandText.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase);

        private readonly IResiliencyPolicy mResiliency;

        internal protected SqlDbQuery(SqlDbConnection connection, DbCommand command, SqlDbLanguageSpecifics specifics)
        {
            Connection = connection;
            mSpecifics = specifics;
            mCommand = command;
            mReader = null;
            CanRead = false;
            mResiliency = ResiliencyPolicyDictionary.Instance.GetPolicy(connection.Connection.ConnectionString);
        }

        /// <summary>
        /// The text of the command.
        /// </summary>
        public string CommandText
        {
            get { return CommandTextBuilder.ToString(); }
            set { CommandTextBuilder = new StringBuilder(value); }
        }

        protected virtual void Dispose(bool disposing)
        {
            CanRead = false;

            if (mCommand != null)
            {
                mCommand.Dispose();
                mCommand = null;
            }

            if (mReader != null)
            {
                mReader.Dispose();
                mReader = null;
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SqlDbQuery()
        {
            Dispose(false);
        }

        protected class Param
        {
            public string Name { get; set; }
            public DbType DbType { get; set; }
            public object Value { get; set; }
            public ParameterDirection Direction { get; set; }
            public IDbDataParameter DbParameter { get; set; } = null;

            public Param Clone()
            {
                return new Param()
                {
                    Name = this.Name,
                    DbType = this.DbType,
                    Value = this.Value,
                    Direction = this.Direction,
                };
            }
        }

        protected List<Param> Parameters { get; } = new List<Param>();
        protected Dictionary<string, Param> ParametersDictionary = new Dictionary<string, Param>();

        /// <summary>
        /// Returns the number of the parameters.
        /// </summary>
        public int ParametersCount => Parameters.Count;

        /// <summary>
        /// Checks whether the parameter with the name specified exist.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool HasParam(string name) => Parameters.Find(p => p.Name == name) != null;

        /// <summary>
        /// Binds the input parameter of the type specified with the value specified.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="t"></param>
        /// <param name="value"></param>
        public void BindParam(string name, Type t, object value)
        {
            if (t == typeof(object) && value != null)
                t = value.GetType();

            if (!mSpecifics.TypeToDb(t, out var dbt))
            {
                if (t.IsClass)
                {
                    var ed = AllEntities.Inst[t, false];
                    if (ed != null && ed.TableDescriptor.PrimaryKey != null)
                    {
                        t = ed.TableDescriptor.PrimaryKey.PropertyAccessor.PropertyType;
                        mSpecifics.TypeToDb(t, out dbt);
                        value = ed.TableDescriptor.PrimaryKey.PropertyAccessor.GetValue(value) ?? DBNull.Value;
                    }
                    else
                        throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
                }
            }

            if (value == null)
                BindNull(name, dbt);
            else
            {
                mSpecifics.ToDbValue(ref value, t, out dbt);
                BindParam(name, dbt, ParameterDirection.Input, value);
            }
        }

        /// <summary>
        /// Binds the input parameter of the type specified with the value specified (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void BindParam<T>(string name, T value) => BindParam(name, typeof(T), value);

        internal void CopyParametersFrom(SqlDbQuery query)
        {
            foreach (Param param in query.Parameters)
                BindParam(param.Name, param.DbType, param.Direction, param.Value);
        }

        /// <summary>
        /// Binds the input parameter of the specified DB type and with the value specified.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        public virtual void BindParam(string name, DbType type, object value)
        {
            if (value == null)
                value = DBNull.Value;

            if (value != DBNull.Value)
            {
                if (value.GetType().GetTypeInfo().IsClass)
                {
                    EntityDescriptor ed = AllEntities.Inst[value.GetType(), false];
                    if (ed != null && ed.TableDescriptor.PrimaryKey != null)
                    {
                        value = ed.TableDescriptor.PrimaryKey.PropertyAccessor.GetValue(value) ?? DBNull.Value;
                    }
                }

                mSpecifics.ToDbValue(ref value, value.GetType(), out type);
            }

            BindParam(name, type, ParameterDirection.Input, value);
        }

        /// <summary>
        /// Binds the output parameter of the specified DB type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        public virtual void BindOutputParam(string name, DbType type)
        {
            BindParam(name, type, ParameterDirection.Output, null);
        }

        /// <summary>
        /// Binds the output parameter of the specified run-time type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        public virtual void BindOutputParam(string name, Type type)
        {
            if (!mSpecifics.TypeToDb(type, out var dbt))
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
            BindOutputParam(name, dbt);
        }

        /// <summary>
        /// Binds the output parameter of the specified run-time type (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        public virtual void BindOutputParam<T>(string name) => BindOutputParam(name, typeof(T));

        /// <summary>
        /// Binds the output parameter of the specified direction and type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="direction"></param>
        /// <param name="value"></param>
        /// <param name="valueType"></param>
        public virtual void BindParam(string name, ParameterDirection direction, object value, Type valueType)
        {
            valueType = Nullable.GetUnderlyingType(valueType) ?? valueType;
            if (value != null && valueType.IsClass && valueType != typeof(string) && valueType != typeof(byte[]))
            {
                var ef = AllEntities.Get(valueType, false);
                if (ef != null && ef.PrimaryKey != null)
                {
                    valueType = ef.PrimaryKey.PropertyAccessor.PropertyType;
                    value = ef.PrimaryKey.PropertyAccessor.GetValue(value);
                }
            }
            mSpecifics.ToDbValue(ref value, valueType, out DbType type);
            BindParam(name, type, direction, value);
        }

        protected virtual void BindParam(string name, DbType type, ParameterDirection direction, object value = null)
        {
            if (ParametersDictionary.TryGetValue(name, out Param param))
            {
                param.DbType = type;
                param.Direction = direction;
                param.Value = value ?? DBNull.Value;
            }
            else
            {
                param = new Param() { Name = name, DbType = type, Value = value ?? DBNull.Value, Direction = direction };
                Parameters.Add(param);
                ParametersDictionary[name] = param;
            }
        }

        /// <summary>
        /// Gets the parameter value.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual object GetParamValue(string name)
        {
            if (ParametersDictionary.TryGetValue(name, out Param param))
            {
                if (param.DbParameter != null)
                {
                    if (param.DbParameter.Value == DBNull.Value)
                        return null;
                    else
                        return param.DbParameter.Value;
                }
                else
                {
                    if (param.Value == DBNull.Value)
                        return null;
                    else
                        return param.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the parameter value of the type desired.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public virtual object GetParamValue(string name, Type type)
        {
            object value = GetParamValue(name);
            return mSpecifics.TranslateValue(value, type);
        }

        /// <summary>
        /// Gets the parameter value of the type desired (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public T GetParamValue<T>(string name)
        {
            return (T)GetParamValue(name, typeof(T));
        }

        /// <summary>
        /// Binds null value of the specified type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        public virtual void BindNull(string name, DbType type)
        {
            BindParam(name, type, DBNull.Value);
        }

        /// <summary>
        /// Binds the output parameter of the specified DB type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        public virtual void BindOutput(string name, DbType type)
        {
            BindParam(name, type, ParameterDirection.Output, null);
        }

        protected Dictionary<string, FieldInfo> mFields;
        protected Dictionary<string, FieldInfo> mFieldsNoCase;

        protected virtual string FinalizeCommand()
        {
            if (mSpecifics.TerminateWithSemicolon)
            {
                if (!EndsWithSemicolon(CommandTextBuilder))
                    CommandTextBuilder.Append(';');
            }
            return CommandTextBuilder.ToString();
        }

        protected virtual void BindParameterToQuery(Param p)
        {
            if (p.DbParameter == null)
            {
                IDbDataParameter param = mCommand.CreateParameter();
                string name = p.Name;
                if (!string.IsNullOrEmpty(mSpecifics.ParameterPrefix) && !name.StartsWith(mSpecifics.ParameterPrefix))
                    name = mSpecifics.ParameterPrefix + name;
                param.ParameterName = name;
                param.Direction = p.Direction;
                mCommand.Parameters.Add(param);
                p.DbParameter = param;
            }
            p.DbParameter.DbType = p.DbType;
            p.DbParameter.Value = p.Value;
        }

        protected virtual void Prepare()
        {
            mCommand.CommandText = FinalizeCommand();

            if (Parameters.Count > 0)
            {
                foreach (Param p in Parameters)
                {
                    BindParameterToQuery(p);
                }
            }
        }

        private void PrepareExecute()
        {
            CanRead = false;

            if (mReader != null)
            {
                mReader.Dispose();
                mReader = null;
            }

            if (mFields != null)
                mFields.Clear();

            Prepare();
        }

        /// <summary>
        /// Executes the query that reads no data.
        /// </summary>
        /// <returns>The number of the rows affected.</returns>
        public virtual int ExecuteNoData()
        {
            PrepareExecute();

            if (string.IsNullOrEmpty(mCommand.CommandText))
                return 0;

            if (mResiliency == null)
                return mCommand.ExecuteNonQuery();
            else
                return mResiliency.Execute(() => mCommand.ExecuteNonQuery());
        }

        /// <summary>
        /// Executes the query that reads no data asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>The number of the rows affected.</returns>
        public virtual Task<int> ExecuteNoDataAsync(CancellationToken? token = null)
        {
            PrepareExecute();

            if (string.IsNullOrEmpty(mCommand.CommandText))
                return Task.FromResult(0);

            if (mResiliency == null)
                return mCommand.ExecuteNonQueryAsync(token ?? CancellationToken.None);
            else
                return mResiliency.ExecuteAsync(token1 => mCommand.ExecuteNonQueryAsync(token1), token ?? CancellationToken.None);
        }

        /// <summary>
        /// Sets or gets the flag indicating whether BLOB fields must be read as stream.
        ///
        /// If the value is `true` blob fields will be returned as a stream.
        /// If the value is `false` (default) blob fields will be returned as an array.
        /// </summary>
        public bool ReadBlobAsStream { get; set; } = false;

        /// <summary>
        /// Executes the query that reads a resultset.
        /// </summary>
        public virtual void ExecuteReader()
        {
            PrepareExecute();
            if (mResiliency == null)
                mReader = mCommand.ExecuteReader(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default);
            else
                mReader = mResiliency.Execute(() => mCommand.ExecuteReader(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default));
        }

        /// <summary>
        /// Executes the query that reads a resultset asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual async Task ExecuteReaderAsync(CancellationToken? token = null)
        {
            PrepareExecute();
            if (mResiliency == null)
                mReader = await mCommand.ExecuteReaderAsync(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default, token ?? CancellationToken.None);
            else
                mReader = await mResiliency.ExecuteAsync(token1 => mCommand.ExecuteReaderAsync(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default, token1), token ?? CancellationToken.None);
        }

        protected const string READER_IS_NOT_INIT = "Reader is not initialized";
        protected const string FIELD_NOT_FOUND = "Field is not found";

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

        /// <summary>
        /// Returns the number of columns in the resultset.
        /// </summary>
        public int FieldCount => mReader.FieldCount;

        protected virtual void HandleFieldName(ref string name)
        {
        }

        /// <summary>
        /// Returns the column information by its index.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public virtual FieldInfo Field(int column)
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            return Field(mReader.GetName(column));
        }

        /// <summary>
        /// Returns the column information by its name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        public virtual FieldInfo Field(string name, bool ignoreCase = false)
        {
            HandleFieldName(ref name);

            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);

            if (ignoreCase && mFieldsNoCase == null)
                mFieldsNoCase = new Dictionary<string, FieldInfo>();

            if (!ignoreCase && mFields == null)
                mFields = new Dictionary<string, FieldInfo>();

            var dict = ignoreCase ? mFieldsNoCase : mFields;

            if (dict.Count != mReader.FieldCount)
            {
                if (dict.Count > 0)
                    dict.Clear();

                for (int i = 0; i < mReader.FieldCount; i++)
                {
                    var rname = mReader.GetName(i);
                    var key = ignoreCase ? rname.ToUpperInvariant() : rname;
                    dict[key] = new FieldInfo(rname, mReader.GetFieldType(i), i);
                }
            }

            if (dict.TryGetValue(ignoreCase ? name.ToUpperInvariant() : name, out FieldInfo rc))
                return rc;
            else
                return null;
        }

        /// <summary>
        /// Gets the next resultset.
        ///
        /// The method switches to the next resultset. Use this method
        /// when multiple `SELECT` queries are executed.
        /// </summary>
        /// <returns></returns>
        public virtual bool NextReaderResult()
        {
            CanRead = false;

            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);

            if (mFields != null)
                mFields.Clear();

            if (mResiliency == null)
                return mReader.NextResult();
            else
                return mResiliency.Execute(() => mReader.NextResult());
        }

        /// <summary>
        /// Gets the next resultset asynchronously.
        ///
        /// The method switches to the next resultset. Use this method
        /// when multiple `SELECT` queries are executed.
        /// </summary>
        /// <returns></returns>
        public virtual Task<bool> NextReaderResultAsync(CancellationToken? token = null)
        {
            CanRead = false;

            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);

            if (mFields != null)
                mFields.Clear();

            if (mResiliency == null)
            {
                if (token == null)
                    return mReader.NextResultAsync();
                else
                    return mReader.NextResultAsync(token.Value);
            }
            else
                return mResiliency.ExecuteAsync(token1 => mReader.NextResultAsync(token1), token ?? CancellationToken.None);
        }

        /// <summary>
        /// Reads the next row of the resultset.
        /// </summary>
        /// <returns>The method returns `false` if there are no more rows.</returns>
        public virtual bool ReadNext()
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            if (mResiliency == null)
                CanRead = mReader.Read();
            else
                CanRead = mResiliency.Execute(() => mReader.Read());
            return CanRead;
        }

        /// <summary>
        /// Reads the next row of the resultset asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual async Task<bool> ReadNextAsync(CancellationToken? token = null)
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            if (mResiliency == null)
                return CanRead = await mReader.ReadAsync(token ?? CancellationToken.None);
            else
                return CanRead = await mResiliency.ExecuteAsync(token1 => mReader.ReadAsync(token1), token ?? CancellationToken.None);
        }

        /// <summary>
        /// Checks whether the column has null value by the column index.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public virtual bool IsNull(int column)
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            return mReader.IsDBNull(column);
        }

        /// <summary>
        /// Gets the value by the column index as it is returned by the database.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public virtual object GetValue(int column)
        {
            return mReader.GetValue(column);
        }

        /// <summary>
        /// Get the value by the column index and try to convert it to the desired type.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public virtual object GetValue(int column, Type type)
        {
            return mSpecifics.TranslateValue(IsNull(column) ? null : GetValue(column), type);
        }

        /// <summary>
        /// Gets the value stream by the column index.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public virtual Stream GetStream(int column)
        {
            return mReader.GetStream(column);
        }

        /// <summary>
        /// Get the value by the column index and try to convert it to the desired type (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="column"></param>
        /// <returns></returns>
        public T GetValue<T>(int column)
        {
            return (T)GetValue(column, typeof(T));
        }

        protected int GetColumn(string column)
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            if (column == null)
                throw new ArgumentNullException(nameof(column));
            FieldInfo fi = Field(column);
            if (fi == null)
                throw new ArgumentException(FIELD_NOT_FOUND, nameof(column));
            return fi.Index;
        }

        /// <summary>
        /// Checks whether the column has null value by the column name.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public bool IsNull(string column)
        {
            return IsNull(GetColumn(column));
        }

        /// <summary>
        /// Gets the value by the column name as it is returned by the database.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public object GetValue(string field)
        {
            return GetValue(GetColumn(field));
        }

        /// <summary>
        /// Gets the value stream by the column name.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public Stream GetStream(string field)
        {
            return GetStream(GetColumn(field));
        }

        /// <summary>
        /// Get the value by the column index and try to convert it to the desired type.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetValue(string column, Type type)
        {
            return GetValue(GetColumn(column), type);
        }

        /// <summary>
        /// Get the value by the column index and try to convert it to the desired type (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="column"></param>
        /// <returns></returns>
        public T GetValue<T>(string column)
        {
            return GetValue<T>(GetColumn(column));
        }

        protected internal static bool EndsWithSemicolon(StringBuilder s)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (Char.IsWhiteSpace(s[i]))
                    continue;
                return s[i] == ';';
            }
            return false;
        }

        /// <summary>
        /// Finds column index by its name.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        public int FindField(string column, bool ignoreCase = false)
        {
            FieldInfo fi = Field(column, ignoreCase);
            if (fi == null)
                return -1;
            else
                return fi.Index;
        }

        /// <summary>
        /// Cancels the query.
        /// </summary>
        public void Cancel()
        {
            mCommand.Cancel();
        }
    }
}
