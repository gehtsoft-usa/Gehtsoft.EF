using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class TypeConvertorTest
    {
        [Theory]
        [InlineData(typeof(object), null, typeof(string), null)]
        [InlineData(typeof(object), null, typeof(int), 0)]
        [InlineData(typeof(object), null, typeof(DateTime), 0)]
        [InlineData(typeof(object), null, typeof(int?), null)]
        [InlineData(typeof(object), null, typeof(DateTime?), null)]
        [InlineData(typeof(object), null, typeof(DayOfWeek), DayOfWeek.Sunday)]
        [InlineData(typeof(object), null, typeof(DayOfWeek?), null)]
        [InlineData(typeof(DayOfWeek?), null, typeof(int?), null)]

        [InlineData(typeof(int), 6, typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(uint), 6, typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(byte), 6, typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(short), 6, typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(ushort), 6, typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(long), 6, typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(ulong), 6, typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(int), 6)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(uint), 6)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(byte), 6)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(short), 6)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(ushort), 6)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(long), 6)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(ulong), 6)]
        [InlineData(typeof(string), "Saturday", typeof(DayOfWeek), DayOfWeek.Saturday)]
        [InlineData(typeof(DayOfWeek), DayOfWeek.Saturday, typeof(string), "Saturday")]

        [InlineData(typeof(int), 6, typeof(string), "6")]
        [InlineData(typeof(string), "6", typeof(int), 6)]
        [InlineData(typeof(double), 1.23, typeof(string), "1.23")]
        [InlineData(typeof(double), 1.23, typeof(string), "1,23", "ru")]
        [InlineData(typeof(string), "1,23", typeof(double), 1.23, "ru")]
        [InlineData(typeof(decimal), 1.23, typeof(string), "1.23")]
        [InlineData(typeof(string), "1.23", typeof(decimal), 1.23)]

        [InlineData(typeof(bool), true, typeof(string), "True")]
        [InlineData(typeof(bool), "True", typeof(bool), true)]
        [InlineData(typeof(bool), false, typeof(string), "False")]
        [InlineData(typeof(bool), "False", typeof(bool), false)]

        [InlineData(typeof(DateTime), "2010-11-25", typeof(string), "11/25/2010 00:00:00")]
        [InlineData(typeof(DateTime), "2010-11-25", typeof(string), "25.11.2010 00:00:00", "ru")]
        [InlineData(typeof(string), "2010-11-25", typeof(DateTime), "2010-11-25")]
        [InlineData(typeof(string), "11/25/2010", typeof(DateTime), "2010-11-25")]

        [InlineData(typeof(decimal), 1.23, typeof(double), 1.23)]
        [InlineData(typeof(double), 1.23, typeof(decimal), 1.23)]

        [InlineData(typeof(int), 1, typeof(bool), true)]
        [InlineData(typeof(int), 0, typeof(bool), false)]
        [InlineData(typeof(bool), false, typeof(int), 0)]

        [InlineData(typeof(int), 6, typeof(short), 6)]
        [InlineData(typeof(short), 6, typeof(int), 6)]
        [InlineData(typeof(int), 6, typeof(uint), 6)]
        [InlineData(typeof(uint), 6, typeof(int), 6)]
        [InlineData(typeof(int), 6, typeof(double), 6.0)]
        [InlineData(typeof(int), 6, typeof(decimal), 6.0)]
        [InlineData(typeof(double), 6, typeof(int), 6)]
        [InlineData(typeof(decimal), 6, typeof(int), 6)]

        [InlineData(typeof(DateTime), "2010-11-22 00:00:00Z", typeof(double), 40504.0)]
        [InlineData(typeof(DateTime), "2010-11-22 08:15:55Z", typeof(long), 634260105550000000)]
        [InlineData(typeof(double), 40504.0, typeof(DateTime), "2010-11-22 00:00:00Z")]
        [InlineData(typeof(long), 634260105550000000, typeof(DateTime), "2010-11-22 08:15:55Z")]

        public void Convert(Type srcType, object src, Type dstType, object dst, string cultureName = null)
        {
            var culture = cultureName == null ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(cultureName);

            src = TestValue.Translate(srcType, src);
            dst = TestValue.Translate(dstType, dst);

            TypeConverter.Convert(src, dstType, culture)
                .Should().Be(dst);
        }

        [Fact]
        public void CallForClass()
        {
            using var s = new MemoryStream();
            TypeConverter.Convert(s, typeof(MemoryStream), CultureInfo.InvariantCulture)
                .Should().BeSameAs(s);
            TypeConverter.Convert(s, typeof(Stream), CultureInfo.InvariantCulture)
                .Should().BeSameAs(s);
            TypeConverter.Convert(s, typeof(IDisposable), CultureInfo.InvariantCulture)
                .Should().BeSameAs(s);
        }
    }
}

