using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Tools
{
    public class HashCreatorTest
    {
        [Theory]
        [InlineData("test", "098f6bcd4621d373cade4e832627b4f6")]
        [InlineData("brown fox jumped over the lazy dog", "bfcca1286add7f2530631c4ddd3b9914")]
        [InlineData("съешь еще этих мягких булок да выпей чаю", "ed117faf4c3c8e819d9e013c3664c53b")]
        public void MD5Test(string data, string hash)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            MD5HashCreator.GetString(data).Should().Be(hash);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Theory]
        [InlineData("test", "ee26b0dd4af7e749aa1a8ee3c10ae9923f618980772e473f8819a5d4940e0db27ac185f8a0e1d5f84f88bc887fd67b143732c304cc5fa9ad8e6f57f50028a8ff")]
        [InlineData("brown fox jumped over the lazy dog", "57843415c590bdaf5982b4c5d69f3028e242d11c7aeb70a1a2daf229e18304c8b3b3f7598c16dbce851840a5900f289a0bbb49de7e5c981ecc409be33fb0cde8")]
        public void SHA512Test(string data, string hash)
        {
            HashCreator.GetHexString<SHA512>(data).Should().Be(hash);
        }

        [Theory]
        [InlineData("test", "7iaw3Ur350mqGo7jwQrpkj9hiYB3Lkc/iBml1JQODbJ6wYX4oOHV+E+IvIh/1nsUNzLDBMxfqa2Ob1f1ACio/w==")]
        [InlineData("brown fox jumped over the lazy dog", "V4Q0FcWQva9ZgrTF1p8wKOJC0Rx663ChotryKeGDBMizs/dZjBbbzoUYQKWQDyiaC7tJ3n5cmB7MQJvjP7DN6A==")]
        public void SHA512TestBase64(string data, string hash)
        {
            HashCreator.GetBase64String<SHA512>(data).Should().Be(hash);
        }

    }
}
