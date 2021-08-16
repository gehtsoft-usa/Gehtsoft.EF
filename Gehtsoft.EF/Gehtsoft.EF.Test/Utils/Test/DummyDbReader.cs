using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Utils
{
    public class DummyDbReaderTest
    {
        [Fact]
        public void TestColumns()
        {
            var reader = new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection
                {
                    {
                        new DummyDbDataReaderResult()
                        {
                            Columns = new DummyDbDataReaderColumnCollection()
                            {
                                { "column1", DbType.Boolean },
                                { "column2", DbType.Int32 },
                                { "column3", DbType.String },
                                { "column4", DbType.Double },
                            },
                            Data = new DummyDbDataReaderColumnDataRows()
                        }
                    }
                }
            };

            reader.FieldCount.Should().Be(4);
            reader.GetName(0).Should().Be("column1");
            reader.GetFieldType(0).Should().Be(typeof(bool));
            reader.GetName(1).Should().Be("column2");
            reader.GetFieldType(1).Should().Be(typeof(int));
            reader.GetName(2).Should().Be("column3");
            reader.GetFieldType(2).Should().Be(typeof(string));
            reader.GetName(3).Should().Be("column4");
            reader.GetFieldType(3).Should().Be(typeof(double));

            reader.GetOrdinal("column1").Should().Be(0);
            reader.GetOrdinal("column2").Should().Be(1);
            reader.GetOrdinal("column3").Should().Be(2);
            reader.GetOrdinal("column4").Should().Be(3);

            reader.GetOrdinal("unknown").Should().BeLessThan(0);

            reader.Read().Should().BeFalse();
        }

        [Fact]
        public void TestRead()
        {
            var reader = new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection
                {
                    {
                        new DummyDbDataReaderResult()
                        {
                            Columns = new DummyDbDataReaderColumnCollection()
                            {
                                { "column1", DbType.Boolean },
                                { "column2", DbType.Int32 },
                                { "column3", DbType.String },
                                { "column4", DbType.Double },
                            },
                            Data = new DummyDbDataReaderColumnDataRows
                            {
                                { false, 123, "abc", 1.234 },
                                { true, 456, "def", null },
                            }
                        }
                    }
                }
            };

            reader.Read().Should().BeTrue();

            reader.IsDBNull(0).Should().BeFalse();
            reader[0].Should().Be(false);
            reader.GetBoolean(0).Should().BeFalse();

            reader.IsDBNull(1).Should().BeFalse();
            reader[1].Should().Be(123);
            reader.GetInt32(1).Should().Be(123);

            reader.IsDBNull(2).Should().BeFalse();
            reader[2].Should().Be("abc");
            reader.GetString(2).Should().Be("abc");

            reader.IsDBNull(3).Should().BeFalse();
            reader[3].Should().Be(1.234);
            reader.GetDouble(3).Should().Be(1.234);

            reader.Read().Should().BeTrue();

            reader.IsDBNull(0).Should().BeFalse();
            reader[0].Should().Be(true);
            reader.GetBoolean(0).Should().BeTrue();

            reader.IsDBNull(1).Should().BeFalse();
            reader[1].Should().Be(456);
            reader.GetInt32(1).Should().Be(456);

            reader.IsDBNull(2).Should().BeFalse();
            reader[2].Should().Be("def");
            reader.GetString(2).Should().Be("def");

            reader.IsDBNull(3).Should().BeTrue();

            reader.Read().Should().BeFalse();
        }

        [Fact]
        public void TestNextResult()
        {
            var reader = new DummyDbDataReader()
            {
                Results = new DummyDbDataReaderResultCollection
                {
                        new DummyDbDataReaderResult()
                        {
                            Columns = new DummyDbDataReaderColumnCollection()
                            {
                                { "column11", DbType.Boolean },
                                { "column12", DbType.Int32 },
                                { "column13", DbType.String },
                                { "column14", DbType.Double },
                            },
                            Data = new DummyDbDataReaderColumnDataRows()
                        },
                        new DummyDbDataReaderResult()
                        {
                            Columns = new DummyDbDataReaderColumnCollection()
                            {
                                { "column21", DbType.Int16 },
                                { "column22", DbType.DateTime },
                                { "column23", DbType.Binary },
                                { "column24", DbType.Decimal },
                                { "column25", DbType.Time },
                            },
                            Data = new DummyDbDataReaderColumnDataRows()
                        }
                    }
            };

            reader.FieldCount.Should().Be(4);
            reader.GetName(0).Should().Be("column11");
            reader.GetFieldType(0).Should().Be(typeof(bool));
            reader.GetName(1).Should().Be("column12");
            reader.GetFieldType(1).Should().Be(typeof(int));
            reader.GetName(2).Should().Be("column13");
            reader.GetFieldType(2).Should().Be(typeof(string));
            reader.GetName(3).Should().Be("column14");
            reader.GetFieldType(3).Should().Be(typeof(double));

            reader.NextResult().Should().BeTrue();

            reader.FieldCount.Should().Be(5);
            reader.GetName(0).Should().Be("column21");
            reader.GetFieldType(0).Should().Be(typeof(short));
            reader.GetName(1).Should().Be("column22");
            reader.GetFieldType(1).Should().Be(typeof(DateTime));
            reader.GetName(2).Should().Be("column23");
            reader.GetFieldType(2).Should().Be(typeof(byte[]));
            reader.GetName(3).Should().Be("column24");
            reader.GetFieldType(3).Should().Be(typeof(decimal));
            reader.GetName(4).Should().Be("column25");
            reader.GetFieldType(4).Should().Be(typeof(TimeSpan));

            reader.NextResult().Should().BeFalse();
        }

    }
}
