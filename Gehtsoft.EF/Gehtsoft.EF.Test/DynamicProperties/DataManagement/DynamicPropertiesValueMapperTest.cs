using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    public class DynamicPropertiesValueMapperTest
    {
        private static readonly DateTime SampleUtc = new DateTime(2021, 12, 27, 12, 55, 17, DateTimeKind.Utc);

        // ---- encode: type code + target column + encoded value ----

        [Fact]
        public void Encode_String()
        {
            var (type, column, value) = DynamicPropertiesValueMapper.Encode("hello");
            type.Should().Be(DynamicPropertyValueType.String);
            column.Should().Be(DynamicPropertiesTableBuilder.StringValueColumn);
            value.Should().Be("hello");
        }

        [Fact]
        public void Encode_Int_GoesToVIntAsLong()
        {
            var (type, column, value) = DynamicPropertiesValueMapper.Encode(42);
            type.Should().Be(DynamicPropertyValueType.Integer);
            column.Should().Be(DynamicPropertiesTableBuilder.IntValueColumn);
            value.Should().BeOfType<long>().And.Be(42L);
        }

        [Fact]
        public void Encode_Long_GoesToVInt()
        {
            var (type, column, value) = DynamicPropertiesValueMapper.Encode(42L);
            type.Should().Be(DynamicPropertyValueType.Long);
            column.Should().Be(DynamicPropertiesTableBuilder.IntValueColumn);
            value.Should().Be(42L);
        }

        [Fact]
        public void Encode_Double_GoesToVReal()
        {
            var (type, column, value) = DynamicPropertiesValueMapper.Encode(4.5);
            type.Should().Be(DynamicPropertyValueType.Real);
            column.Should().Be(DynamicPropertiesTableBuilder.RealValueColumn);
            value.Should().Be(4.5);
        }

        [Fact]
        public void Encode_Bool_GoesToVIntAs0Or1()
        {
            DynamicPropertiesValueMapper.Encode(true).Value.Should().Be(1L);
            DynamicPropertiesValueMapper.Encode(false).Value.Should().Be(0L);
            DynamicPropertiesValueMapper.Encode(true).Column.Should().Be(DynamicPropertiesTableBuilder.IntValueColumn);
            DynamicPropertiesValueMapper.Encode(true).Type.Should().Be(DynamicPropertyValueType.Boolean);
        }

        [Fact]
        public void Encode_DateTime_GoesToVIntAsUtcTicks()
        {
            var (type, column, value) = DynamicPropertiesValueMapper.Encode(SampleUtc);
            type.Should().Be(DynamicPropertyValueType.DateTime);
            column.Should().Be(DynamicPropertiesTableBuilder.IntValueColumn);
            value.Should().Be(SampleUtc.Ticks);
        }

        [Fact]
        public void Encode_UnsupportedType_Throws()
        {
            ((Action)(() => DynamicPropertiesValueMapper.Encode(1.23m))).Should().Throw<ArgumentException>();
        }

        // ---- round-trip: object -> sql -> object ----

        [Fact]
        public void RoundTrip_PreservesValueAndClrType()
        {
            AssertRoundTrip("hello");
            AssertRoundTrip(42);          // int stays int
            AssertRoundTrip(9000000000L); // long stays long
            AssertRoundTrip(4.5);
            AssertRoundTrip(true);
            AssertRoundTrip(false);

            var back = RoundTrip(SampleUtc);
            back.Should().BeOfType<DateTime>();
            ((DateTime)back).Should().Be(SampleUtc);
            ((DateTime)back).Kind.Should().Be(DateTimeKind.Utc);
        }

        private static object RoundTrip(object value)
        {
            var (type, _, encoded) = DynamicPropertiesValueMapper.Encode(value);
            return DynamicPropertiesValueMapper.Decode(type, encoded);
        }

        private static void AssertRoundTrip(object value)
        {
            object back = RoundTrip(value);
            back.Should().Be(value);
            back.GetType().Should().Be(value.GetType(), "the CLR type must round-trip");
        }
    }
}
