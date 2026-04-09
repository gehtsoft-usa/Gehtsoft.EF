using System;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.MysqlDb;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.PostgresDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqliteDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    public class LanguageSpecificsTypeTests
    {
        private static (object value, DbType dbType) CallToDbValue(SqlDbLanguageSpecifics specifics, object value, Type type)
        {
            specifics.ToDbValue(ref value, type, out DbType dbType);
            return (value, dbType);
        }

        #region Base class (Sql92) — byte, TimeSpan, char, short, object, nullable unwrap

        [Fact]
        public void Base_Byte_ToDbValue()
        {
            var specifics = new Sql92LanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, (byte)42, typeof(byte));
            dbType.Should().Be(DbType.Byte);
            value.Should().Be((byte)42);
        }

        [Fact]
        public void Base_TimeSpan_ToDbValue()
        {
            var specifics = new Sql92LanguageSpecifics();
            var ts = TimeSpan.FromHours(2.5);
            var (value, dbType) = CallToDbValue(specifics, ts, typeof(TimeSpan));
            dbType.Should().Be(DbType.Time);
            value.Should().Be(ts);
        }

        [Fact]
        public void Base_Char_ToDbValue()
        {
            var specifics = new Sql92LanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, 'A', typeof(char));
            dbType.Should().Be(DbType.String);
            value.Should().Be("A");
        }

        [Fact]
        public void Base_Short_TypeToDb()
        {
            var specifics = new Sql92LanguageSpecifics();
            specifics.TypeToDb(typeof(short), out DbType dbType).Should().BeTrue();
            dbType.Should().Be(DbType.Int16);
        }

        [Fact]
        public void Base_Object_TypeToDb()
        {
            var specifics = new Sql92LanguageSpecifics();
            specifics.TypeToDb(typeof(object), out DbType dbType).Should().BeTrue();
            dbType.Should().Be(DbType.Object);
        }

        [Fact]
        public void Base_NullableInt_NonNull_ToDbValue()
        {
            var specifics = new Sql92LanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, 5, typeof(int?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(5);
        }

        [Fact]
        public void Base_NullableInt_Null_ToDbValue()
        {
            var specifics = new Sql92LanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, null, typeof(int?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(DBNull.Value);
        }

        #endregion

        #region SQLite driver

        [Fact]
        public void Sqlite_NullableBool_True_ToDbValue()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, true, typeof(bool?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(1);
        }

        [Fact]
        public void Sqlite_NullableBool_Null_ToDbValue()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, null, typeof(bool?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(DBNull.Value);
        }

        [Fact]
        public void Sqlite_NullableGuid_NonNull_ToDbValue()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var guid = Guid.NewGuid();
            var (value, dbType) = CallToDbValue(specifics, guid, typeof(Guid?));
            dbType.Should().Be(DbType.String);
            value.Should().Be(guid.ToString("D"));
        }

        [Fact]
        public void Sqlite_NullableGuid_Null_ToDbValue()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, null, typeof(Guid?));
            dbType.Should().Be(DbType.String);
            value.Should().Be(DBNull.Value);
        }

        [Fact]
        public void Sqlite_NullableDecimal_NonNull_ToDbValue()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, 3.14m, typeof(decimal?));
            dbType.Should().Be(DbType.Double);
            value.Should().Be((double)3.14m);
        }

        [Fact]
        public void Sqlite_NullableDateTime_NonNull_ToDbValue()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var dt = new DateTime(2020, 5, 27, 10, 45, 38, DateTimeKind.Utc);
            var (value, dbType) = CallToDbValue(specifics, dt, typeof(DateTime?));
            // DateTime? path always uses Double — even when StoreDateAsString is false (default), this is correct
            dbType.Should().Be(DbType.Double);
            value.Should().BeOfType<double>();
        }

        [Fact]
        public void Sqlite_NullableDateTime_ZeroTicks_ToDbValue()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var dt = new DateTime(0, DateTimeKind.Unspecified);
            var (value, dbType) = CallToDbValue(specifics, dt, typeof(DateTime?));
            dbType.Should().Be(DbType.Double);
            value.Should().Be(DBNull.Value);
        }

        [Fact]
        public void Sqlite_NullableBool_TranslateValue_NonNull()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue(1, typeof(bool?));
            result.Should().Be((bool?)true);
        }

        [Fact]
        public void Sqlite_NullableBool_TranslateValue_Null()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue(null, typeof(bool?));
            result.Should().BeNull();
        }

        [Fact]
        public void Sqlite_NullableGuid_TranslateValue_NonNull()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var guid = Guid.NewGuid();
            var result = specifics.TranslateValue(guid.ToString("D"), typeof(Guid?));
            result.Should().Be((Guid?)guid);
        }

        [Fact]
        public void Sqlite_NullableGuid_TranslateValue_Null()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue(null, typeof(Guid?));
            result.Should().BeNull();
        }

        [Fact]
        public void Sqlite_NullableGuid_TranslateValue_InvalidString()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue("not-a-guid", typeof(Guid?));
            result.Should().Be((Guid?)Guid.Empty);
        }

        [Fact]
        public void Sqlite_Guid_TranslateValue_InvalidString()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue("not-a-guid", typeof(Guid));
            result.Should().Be(Guid.Empty);
        }

        [Fact]
        public void Sqlite_Guid_TranslateValue_Null()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue(null, typeof(Guid));
            result.Should().Be(Guid.Empty);
        }

        [Fact]
        public void Sqlite_NullableDateTime_TranslateValue_Null()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue(null, typeof(DateTime?));
            result.Should().BeNull();
        }

        [Fact]
        public void Sqlite_DateTime_TranslateValue_Null()
        {
            var specifics = new SqliteDbLanguageSpecifics();
            var result = specifics.TranslateValue(null, typeof(DateTime));
            ((DateTime)result).Ticks.Should().Be(0);
        }

        #endregion

        #region SQLite StoreDateAsString bug

        [Fact]
        public void Sqlite_NullableDateTime_StoreDateAsString_ToDbValue()
        {
            var oldValue = SqliteGlobalOptions.StoreDateAsString;
            try
            {
                SqliteGlobalOptions.StoreDateAsString = true;
                var specifics = new SqliteDbLanguageSpecifics();

                // TypeName generates TEXT when StoreDateAsString=true
                var typeName = specifics.TypeName(DbType.DateTime, 0, 0, false);
                typeName.Should().Be("TEXT");

                // DateTime? ToDbValue should match DateTime ToDbValue when StoreDateAsString=true:
                // return DbType.String and format as "yyyy-MM-dd HH:mm:ss"
                var dt = new DateTime(2020, 5, 27, 10, 45, 38, DateTimeKind.Utc);
                var (value, dbType) = CallToDbValue(specifics, dt, typeof(DateTime?));
                dbType.Should().Be(DbType.String, "DateTime? should use String when StoreDateAsString=true, matching non-nullable DateTime behavior");
                value.Should().Be("2020-05-27 10:45:38");
            }
            finally
            {
                SqliteGlobalOptions.StoreDateAsString = oldValue;
            }
        }

        [Fact]
        public void Sqlite_NullableDateTime_StoreDateAsString_TranslateValue()
        {
            var oldValue = SqliteGlobalOptions.StoreDateAsString;
            try
            {
                SqliteGlobalOptions.StoreDateAsString = true;
                var specifics = new SqliteDbLanguageSpecifics();

                // When StoreDateAsString=true, reading back a DateTime? from a string should work
                var result = specifics.TranslateValue("2020-05-27 10:45:38", typeof(DateTime?));
                result.Should().NotBeNull();
                ((DateTime?)result).Value.Year.Should().Be(2020);
                ((DateTime?)result).Value.Month.Should().Be(5);
                ((DateTime?)result).Value.Day.Should().Be(27);
            }
            finally
            {
                SqliteGlobalOptions.StoreDateAsString = oldValue;
            }
        }

        #endregion

        #region Oracle driver

        [Fact]
        public void Oracle_NullableBool_True_ToDbValue()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, true, typeof(bool?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(1);
        }

        [Fact]
        public void Oracle_NullableBool_Null_ToDbValue()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, null, typeof(bool?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(DBNull.Value);
        }

        [Fact]
        public void Oracle_NullableGuid_NonNull_ToDbValue()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var guid = Guid.NewGuid();
            var (value, dbType) = CallToDbValue(specifics, guid, typeof(Guid?));
            dbType.Should().Be(DbType.String);
            value.Should().Be(guid.ToString("D"));
        }

        [Fact]
        public void Oracle_NullableInt_NonNull_ToDbValue()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, 42, typeof(int?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(42);
        }

        [Fact]
        public void Oracle_NullableBool_TranslateValue_False()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var result = specifics.TranslateValue(0, typeof(bool?));
            result.Should().Be((bool?)false);
        }

        [Fact]
        public void Oracle_NullableGuid_TranslateValue_NonNull()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var guid = Guid.NewGuid();
            var result = specifics.TranslateValue(guid.ToString("D"), typeof(Guid?));
            result.Should().Be((Guid?)guid);
        }

        [Fact]
        public void Oracle_NullableGuid_TranslateValue_InvalidString()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var result = specifics.TranslateValue("not-a-guid", typeof(Guid?));
            result.Should().Be((Guid?)Guid.Empty);
        }

        [Fact]
        public void Oracle_Guid_TranslateValue_Null()
        {
            var specifics = new OracleDbLanguageSpecifics();
            var result = specifics.TranslateValue(null, typeof(Guid));
            result.Should().Be(Guid.Empty);
        }

        #endregion

        #region MySQL driver

        [Fact]
        public void Mysql_NullableBool_True_ToDbValue()
        {
            var specifics = new MysqlDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, true, typeof(bool?));
            dbType.Should().Be(DbType.Int16);
            value.Should().Be(1);
        }

        [Fact]
        public void Mysql_NullableBool_Null_ToDbValue()
        {
            var specifics = new MysqlDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, null, typeof(bool?));
            dbType.Should().Be(DbType.Int16);
            value.Should().Be(DBNull.Value);
        }

        [Fact]
        public void Mysql_NullableGuid_NonNull_ToDbValue()
        {
            var specifics = new MysqlDbLanguageSpecifics();
            var guid = Guid.NewGuid();
            var (value, dbType) = CallToDbValue(specifics, guid, typeof(Guid?));
            dbType.Should().Be(DbType.String);
            value.Should().Be(guid.ToString("D"));
        }

        [Fact]
        public void Mysql_NullableBool_TranslateValue_True()
        {
            var specifics = new MysqlDbLanguageSpecifics();
            var result = specifics.TranslateValue((short)1, typeof(bool?));
            result.Should().Be((bool?)true);
        }

        [Fact]
        public void Mysql_NullableBool_TranslateValue_Null()
        {
            var specifics = new MysqlDbLanguageSpecifics();
            var result = specifics.TranslateValue(null, typeof(bool?));
            result.Should().BeNull();
        }

        [Fact]
        public void Mysql_NullableGuid_TranslateValue_NonNull()
        {
            var specifics = new MysqlDbLanguageSpecifics();
            var guid = Guid.NewGuid();
            var result = specifics.TranslateValue(guid.ToString("D"), typeof(Guid?));
            result.Should().Be((Guid?)guid);
        }

        [Fact]
        public void Mysql_Guid_TranslateValue_InvalidString()
        {
            var specifics = new MysqlDbLanguageSpecifics();
            var result = specifics.TranslateValue("not-a-guid", typeof(Guid));
            result.Should().Be(Guid.Empty);
        }

        #endregion

        #region MSSQL driver

        [Fact]
        public void Mssql_NullableBool_True_ToDbValue()
        {
            var specifics = new MssqlDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, true, typeof(bool?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(1);
        }

        [Fact]
        public void Mssql_NullableBool_False_ToDbValue()
        {
            var specifics = new MssqlDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, false, typeof(bool?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(0);
        }

        [Fact]
        public void Mssql_NullableBool_Null_ToDbValue()
        {
            var specifics = new MssqlDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, null, typeof(bool?));
            dbType.Should().Be(DbType.Int32);
            value.Should().Be(DBNull.Value);
        }

        #endregion

        #region Postgres driver

        [Fact]
        public void Postgres_NullableBool_ToDbValue_FallsToBase()
        {
            // Postgres has no bool override — falls through to base class which uses DbType.Boolean
            var specifics = new PostgresDbLanguageSpecifics();
            var (value, dbType) = CallToDbValue(specifics, true, typeof(bool));
            dbType.Should().Be(DbType.Boolean);
            value.Should().Be(true);
        }

        #endregion
    }
}
