using System;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The aggregation function identifiers.
    /// </summary>
    public enum AggFn
    {
        /// <summary>
        /// No function
        /// </summary>
        None,
        /// <summary>
        /// Count
        /// </summary>
        Count,
        /// <summary>
        /// Sum
        /// </summary>
        Sum,
        /// <summary>
        /// Average
        /// </summary>
        Avg,
        /// <summary>
        /// Find minimum value
        /// </summary>
        Min,
        /// <summary>
        /// Find maximum value.
        /// </summary>
        Max,
    }

    /// <summary>
    /// The logical operator identifiers.
    /// </summary>
    [Flags]
    public enum LogOp
    {
        /// <summary>
        /// No operation
        /// </summary>
        None = 0,
        /// <summary>
        /// Logical not
        /// </summary>
        Not = 1,
        /// <summary>
        /// Logical And
        /// </summary>
        And = 2,
        /// <summary>
        /// Logical Or
        /// </summary>
        Or = 4,
    }

    /// <summary>
    /// Comparison operations
    /// </summary>
    public enum CmpOp
    {
        /// <summary>
        /// Equals
        /// </summary>
        Eq,
        /// <summary>
        /// Not equals
        /// </summary>
        Neq,
        /// <summary>
        /// Less
        /// </summary>
        Ls,
        /// <summary>
        /// Less or equal
        /// </summary>
        Le,
        /// <summary>
        /// Greater
        /// </summary>
        Gt,
        /// <summary>
        /// Greater or equal
        /// </summary>
        Ge,
        /// <summary>
        /// Like
        /// </summary>
        Like,
        /// <summary>
        /// In (query or set)
        /// </summary>
        In,
        /// <summary>
        /// Not In (query or set)
        /// </summary>
        NotIn,
        /// <summary>
        /// Value is null
        /// </summary>
        IsNull,
        /// <summary>
        /// Value is not
        /// </summary>
        NotNull,
        /// <summary>
        /// Sub-query returns non-empty result
        /// </summary>
        Exists,
        /// <summary>
        /// Sub-query returns empty result
        /// </summary>
        NotExists,
    }

    /// <summary>
    /// Kinds of table joins
    /// </summary>
    public enum TableJoinType
    {
        /// <summary>
        /// None
        ///
        /// No-join means returning of all possible combinations of the records
        /// from left and right tables.
        /// </summary>
        None,
        /// <summary>
        /// Inner
        ///
        /// In inner join only records that exactly matches are selected
        /// </summary>
        Inner,
        /// <summary>
        /// Left Outer
        ///
        /// In left outer join all records that exactly matches, plus records
        /// from the first table that have no matches in right table are selected.
        /// </summary>
        Left,
        /// <summary>
        /// Right Outer
        ///
        /// In right outer join all records that exactly matches, plus
        /// records from the second table that have no matches in left table are select.
        /// </summary>
        Right,
        /// <summary>
        /// Full Outer
        ///
        /// In full outer join all matches, and all records without match from left
        /// and right tables are returned.
        /// </summary>
        Outer,
    }

    /// <summary>
    /// Internal interface. Do not use it directly.
    /// </summary>
    [DocgenIgnore]
    public interface IOpBracketAcceptor
    {
        void BracketClosed(OpBracket op);
    }

    /// <summary>
    /// Internal class, Do not use it directly.
    /// </summary>
    public sealed class OpBracket : IDisposable
    {
        private IOpBracketAcceptor mAcceptor;

        public LogOp LogOp { get; }

        public event EventHandler OnClose;

        public OpBracket(IOpBracketAcceptor acceptor, LogOp op)
        {
            mAcceptor = acceptor;
            LogOp = op;
        }

        public void Dispose()
        {
            OnClose?.Invoke(this, EventArgs.Empty);

            if (mAcceptor != null)
                mAcceptor.BracketClosed(this);
            mAcceptor = null;
        }
    }

    /// <summary>
    /// Sorting direction.
    /// </summary>
    public enum SortDir
    {
        Asc,
        Desc,
    }
}
