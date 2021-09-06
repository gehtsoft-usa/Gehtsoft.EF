using System;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Event arguments for `OnAction` event of `CreateEntityController`
    ///
    /// See also <see cref="CreateEntityController.OnAction"/>
    /// </summary>
    public class CreateEntityControllerEventArgs : EventArgs
    {
        /// <summary>
        /// The action type.
        /// </summary>
        public enum Action
        {
            Create,
            Drop,
            Update,
            Processing,
        }

        /// <summary>
        /// The action.
        /// </summary>
        public Action EventAction { get; set; }

        /// <summary>
        /// The table name.
        /// </summary>
        public string Table { get; set; }

        [DocgenIgnore]
        public CreateEntityControllerEventArgs(Action action, string table)
        {
            EventAction = action;
            Table = table;
        }
    }
}
