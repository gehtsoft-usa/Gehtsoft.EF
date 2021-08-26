using System;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The filter for all queries with the condition.
    ///
    /// The filter is the sequence of the <see cref="IContextFilterCondition">conditions</see>
    /// joined by Logical And or Logical Or operators.
    ///
    /// Use <see cref="EntityFilterBuilderExtension"/> for more readable definition
    /// of the filters.
    /// </summary>
    public interface IContextFilter
    {
        /// <summary>
        /// Adds a group of the conditions (conditions enclosed into brackets)
        /// connected to the previously set conditions by the operator specified.
        ///
        /// The condition considered finished when the returns object is disposed.
        /// </summary>
        /// <param name="logOp"></param>
        /// <returns></returns>
        IDisposable AddGroup(LogOp logOp = LogOp.And);

        /// <summary>
        /// Adds a condition.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        IContextFilterCondition Add(LogOp op = LogOp.And);
    }
}