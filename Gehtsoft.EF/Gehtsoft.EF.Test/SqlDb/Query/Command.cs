using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Query
{
    public class Command
    {
        [Fact]
        public void DiposesDependencies()
        {
            using var connection = new DummySqlConnection(new DummyDbConnection());

            var reader1 = new DummyDbDataReader();
            var reader2 = new DummyDbDataReader();

            DummyDbCommand dbcommand = null;
            using (var query = connection.GetQuery("command"))
            {
                dbcommand = query.Command as DummyDbCommand;

                dbcommand.ReturnReader = reader1;

                query.ExecuteReader();

                dbcommand.ReturnReader = reader2;

                query.ExecuteReader();

                reader1.DisposeCalled.Should().BeTrue();
                reader2.DisposeCalled.Should().BeFalse();
            }

            reader2.DisposeCalled.Should().BeTrue();
            dbcommand.DisposedCalled.Should().BeTrue();
        }

        [Theory]
        [InlineData("command", "command;")]
        [InlineData("command;", "command;")]
        [InlineData("command;   ", "command;   ")]
        [InlineData("command   ", "command   ;")]
        public void TerminateCommandWithSemicolon(string command, string expectedCommand)
        {
            using var connection = new DummySqlConnection(new DummyDbConnection());
            using var query = connection.GetQuery(command);
            query.ExecuteNoData();
            query.Command.CommandText.Should().Be(expectedCommand);
        }
    }
}
