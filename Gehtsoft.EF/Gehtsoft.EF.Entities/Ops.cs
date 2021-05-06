using System;

namespace Gehtsoft.EF.Entities
{
    public enum AggFn
    {
        None,
        Count,
        Sum,
        Avg,
        Min,
        Max,
    }

    [Flags]
    public enum LogOp
    {
        None = 0,
        Not = 1,
        And = 2,
        Or = 4,
    }

    public enum CmpOp
    {
        Eq,
        Neq,
        Ls,
        Le,
        Gt,
        Ge,
        Like,
        In,
        NotIn,
        IsNull,
        NotNull,
        Exists,
        NotExists,
    }

    public enum TableJoinType
    {
        None,
        Inner,
        Left,
        Right,
        Outer,
    }

    public interface IOpBracketAcceptor
    {
        void BracketClosed(OpBracket op);
    }

    public sealed class OpBracket : IDisposable
    {
        private IOpBracketAcceptor mAcceptor;
        public LogOp LogOp { get; }

        public OpBracket(IOpBracketAcceptor acceptor, LogOp op)
        {
            mAcceptor = acceptor;
            LogOp = op;
        }

        public void Dispose()
        {
            if (mAcceptor != null)
                mAcceptor.BracketClosed(this);
            mAcceptor = null;
        }
    }

    public enum SortDir
    {
        Asc,
        Desc,
    }
}
