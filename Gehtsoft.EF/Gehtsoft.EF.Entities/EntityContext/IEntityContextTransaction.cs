using System;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The transaction with entity context.
    ///
    /// The transaction is a group operation that will be either
    /// executed all or not executed at all.
    ///
    /// Please note that not all context (especially non-SQL) will
    /// support transactions.
    /// </summary>
    public interface IEntityContextTransaction : IDisposable
    {
        /// <summary>
        /// Commits the transaction.
        ///
        /// The transaction will be considered failed unless it is explicitly commited.
        /// </summary>
        void Commit();

        /// <summary>
        /// Rollsback transaction.
        /// </summary>
        void Rollback();
    }
}