using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class HashCreator
    {
        [Theory]
        [InlineData("test", "098f6bcd4621d373cade4e832627b4f6")]
        [InlineData("brown fox jumped over the lazy dog", "bfcca1286add7f2530631c4ddd3b9914")]
        [InlineData("съешь еще этих мягких булок да выпей чаю", "ed117faf4c3c8e819d9e013c3664c53b")]
        public void MD5Test(string data, string hash)
        {
            MD5HashCreator.GetString(data).Should().Be(hash);
        }
    }
}
