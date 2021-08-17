using System;
using System.Data;

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
}
