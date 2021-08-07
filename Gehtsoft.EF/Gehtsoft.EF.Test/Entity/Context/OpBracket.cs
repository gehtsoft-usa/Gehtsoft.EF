using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Context
{
    public class TestOpBracket
    {
        [Fact]
        public void CallsAcceptor()
        {
            OpBracket bracket = null;

            var acceptor = new Mock<IOpBracketAcceptor>();
            Expression<Action<IOpBracketAcceptor>> acceptorAction = a => a.BracketClosed(It.Is<OpBracket>(b => ReferenceEquals(bracket, b)));
            acceptor.Setup(acceptorAction).Verifiable();

            bracket = new OpBracket(acceptor.Object, LogOp.Or);
            bracket.Dispose();

            acceptor.Verify(acceptorAction, Times.Once());
        }
    }
}
