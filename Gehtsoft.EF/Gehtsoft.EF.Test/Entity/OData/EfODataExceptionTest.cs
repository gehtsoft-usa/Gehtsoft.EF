using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.OData;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.OData
{
    /// <summary>
    /// Every <see cref="EfODataExceptionCode"/> maps to a message, and an unrecognized code falls back to a
    /// generic message. Constructing the exception for each code exercises the full message table.
    /// </summary>
    public class EfODataExceptionTest
    {
        [Fact]
        public void EveryCode_ProducesAMessageAndKeepsTheCode()
        {
            foreach (EfODataExceptionCode code in Enum.GetValues(typeof(EfODataExceptionCode)))
            {
                EfODataException ex = new EfODataException(code, "arg");
                ex.ErrorCode.Should().Be(code);
                ex.Message.Should().NotBeNullOrEmpty();
            }
        }

        [Fact]
        public void UnknownCode_FallsBackToGenericMessage()
        {
            EfODataException ex = new EfODataException((EfODataExceptionCode)9999);
            ex.Message.Should().Contain("Unknown exception");
        }
    }
}
