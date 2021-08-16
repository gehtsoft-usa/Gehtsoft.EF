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

namespace Gehtsoft.EF.Db.SqlDb
{
    public interface IDbQuery : IDisposable
    {
        bool IsInsert { get; }

        bool CanRead { get; }

        SqlDbLanguageSpecifics LanguageSpecifics { get; }

        void BindNull(string name, DbType type);
        void BindOutputParam(string name, DbType type);
        void BindParam(string name, ParameterDirection direction, object value, Type valueType);
        void BindParam<T>(string name, T value);
        object GetParamValue(string name, Type type);
        T GetParamValue<T>(string name);

        int ExecuteNoData();
        void ExecuteReader();
        bool ReadNext();

        bool IsNull(int column);
        bool IsNull(string column);

        T GetValue<T>(int column);
        T GetValue<T>(string column);

        object GetValue(int column, Type type);

        object GetValue(string column, Type type);

        int FindField(string column);
    }

    public class SqlDbQuery : IDbQuery
    {
        protected DbCommand mCommand;
        protected DbDataReader mReader;

        public IDbCommand Command => mCommand;

        public IDataReader Reader => mReader;

        protected SqlDbLanguageSpecifics mSpecifics;
        public bool CanRead { get; protected set; }

        public SqlDbConnection Connection { get; }

        public StringBuilder CommandTextBuilder { get; private set; } = new StringBuilder();

        public SqlDbLanguageSpecifics LanguageSpecifics => mSpecifics;

        public bool IsInsert => CommandText.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase);

        private readonly IResiliencyPolicy mResiliency;

        public SqlDbQuery(SqlDbConnection connection, DbCommand command, SqlDbLanguageSpecifics specifics)
        {
            Connection = connection;
            mSpecifics = specifics;
            mCommand = command;
            mReader = null;
            CanRead = false;
            mResiliency = ResiliencyPolicyDictionary.Instance.GetPolicy(connection.Connection.ConnectionString);
        }

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

        public int ParametersCount => Parameters.Count;

        public void BindParam<T>(string name, T value)
        {
            object _value = value;
            Type t = typeof(T);

            if (_value.GetType().GetTypeInfo().IsClass)
            {
                EntityDescriptor ed = AllEntities.Inst[_value.GetType(), false];
                if (ed != null && ed.TableDescriptor.PrimaryKey != null)
                {
                    t = ed.TableDescriptor.PrimaryKey.PropertyAccessor.PropertyType;
                    _value = ed.TableDescriptor.PrimaryKey.PropertyAccessor.GetValue(_value) ?? DBNull.Value;
                }
            }
            mSpecifics.ToDbValue(ref _value, t, out DbType type);
            BindParam(name, type, ParameterDirection.Input, _value);
        }

        internal void CopyParametersFrom(SqlDbQuery query)
        {
            foreach (Param param in query.Parameters)
                BindParam(param.Name, param.DbType, param.Direction, param.Value);
        }

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

        public virtual void BindOutputParam(string name, DbType type)
        {
            BindParam(name, type, ParameterDirection.Output, null);
        }

        public virtual void BindParam(string name, ParameterDirection direction, object value, Type valueType)
        {
            object _value = value;
            mSpecifics.ToDbValue(ref _value, valueType, out DbType type);
            BindParam(name, type, direction, _value);
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

        public virtual object GetParamValue(string name, Type type)
        {
            object value = GetParamValue(name);
            return mSpecifics.TranslateValue(value, type);
        }

        public T GetParamValue<T>(string name)
        {
            return (T)GetParamValue(name, typeof(T));
        }

        public virtual void BindNull(string name, DbType type)
        {
            BindParam(name, type, DBNull.Value);
        }

        public virtual void BindOutput(string name, DbType type)
        {
            BindParam(name, type, ParameterDirection.Output, null);
        }

        protected Dictionary<string, FieldInfo> mFields;

        protected virtual string FinalizeCommand()
        {
            if (mSpecifics.TerminateWithSemicolon)
            {
                if (!EndsWithSemicolon(CommandTextBuilder))
                    CommandTextBuilder.Append(';');
            }
            return CommandTextBuilder.ToString();
        }

        protected virtual void BindParameterToQuery(SqlDbQuery.Param p)
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

        public virtual Task<int> ExecuteNoDataAsync() => ExecuteNoDataAsync(null);

        public virtual Task<int> ExecuteNoDataAsync(CancellationToken? token)
        {
            PrepareExecute();

            if (string.IsNullOrEmpty(mCommand.CommandText))
                return Task.FromResult(0);

            if (mResiliency == null)
                return mCommand.ExecuteNonQueryAsync(token ?? CancellationToken.None);
            else
                return mResiliency.ExecuteAsync(token1 => mCommand.ExecuteNonQueryAsync(token1), token ?? CancellationToken.None);
        }

        public bool ReadBlobAsStream { get; set; } = false;

        public virtual void ExecuteReader()
        {
            PrepareExecute();
            if (mResiliency == null)
                mReader = mCommand.ExecuteReader(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default);
            else
                mReader = mResiliency.Execute(() => mCommand.ExecuteReader(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default));
        }

        public Task ExecuteReaderAsync() => ExecuteReaderAsync(null);
        public async Task ExecuteReaderAsync(CancellationToken? token)
        {
            PrepareExecute();
            if (mResiliency == null)
                mReader = await mCommand.ExecuteReaderAsync(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default, token ?? CancellationToken.None);
            else
                mReader = await mResiliency.ExecuteAsync(token1 => mCommand.ExecuteReaderAsync(ReadBlobAsStream ? CommandBehavior.SequentialAccess : CommandBehavior.Default, token1), token ?? CancellationToken.None);
        }

        protected const string READER_IS_NOT_INIT = "Reader is not initialized";
        protected const string FIELD_NOT_FOUND = "Field is not found";

        public class FieldInfo
        {
            public string Name { get; }
            public Type DataType { get; }
            public int Index { get; }

            public FieldInfo(string name, Type type, int index)
            {
                Name = name;
                DataType = type;
                Index = index;
            }
        }

        public int FieldCount => mReader.FieldCount;

        protected virtual void HandleFieldName(ref string name)
        {
        }

        public virtual FieldInfo Field(int column)
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            return Field(mReader.GetName(column));
        }

        public virtual FieldInfo Field(string name)
        {
            HandleFieldName(ref name);

            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);

            if (mFields == null)
                mFields = new Dictionary<string, FieldInfo>();

            if (mFields.Count != mReader.FieldCount)
            {
                if (mFields.Count > 0)
                    mFields.Clear();
                for (int i = 0; i < mReader.FieldCount; i++)
                {
                    string rname = mReader.GetName(i);
                    mFields[rname] = new FieldInfo(rname, mReader.GetFieldType(i), i);
                }
            }

            if (mFields.TryGetValue(name, out FieldInfo rc))
                return rc;
            else
                return null;
        }

        public bool NextReaderResult()
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

        public Task<bool> NextReaderResultAsync() => NextReaderResultAsync(null);

        public Task<bool> NextReaderResultAsync(CancellationToken? token)
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

        public Task<bool> ReadNextAsync() => ReadNextAsync(null);

        public async Task<bool> ReadNextAsync(CancellationToken? token)
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            if (mResiliency == null)
                return CanRead = await mReader.ReadAsync(token ?? CancellationToken.None);
            else
                return CanRead = await mResiliency.ExecuteAsync(token1 => mReader.ReadAsync(token1), token ?? CancellationToken.None);
        }

        public virtual bool IsNull(int column)
        {
            if (mReader == null)
                throw new InvalidOperationException(READER_IS_NOT_INIT);
            return mReader.IsDBNull(column);
        }

        public virtual object GetValue(int column)
        {
            return mReader.GetValue(column);
        }

        public virtual object GetValue(int column, Type type)
        {
            return mSpecifics.TranslateValue(IsNull(column) ? null : GetValue(column), type);
        }

        public virtual Stream GetStream(int column)
        {
            return mReader.GetStream(column);
        }

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

        public bool IsNull(string column)
        {
            return IsNull(GetColumn(column));
        }

        public object GetValue(string field)
        {
            return GetValue(GetColumn(field));
        }
        public Stream GetStream(string field)
        {
            return GetStream(GetColumn(field));
        }

        public object GetValue(string column, Type type)
        {
            return GetValue(GetColumn(column), type);
        }

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

        public int FindField(string column)
        {
            FieldInfo fi = Field(column);
            if (fi == null)
                return -1;
            else
                return fi.Index;
        }

        public void Cancel()
        {
            mCommand.Cancel();
        }
    }
}
