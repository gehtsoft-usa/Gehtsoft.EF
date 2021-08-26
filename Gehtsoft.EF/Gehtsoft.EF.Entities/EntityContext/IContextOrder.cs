namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The order definition.
    /// </summary>
    public interface IContextOrder
    {
        /// <summary>
        /// Adds an column to the order definition.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="sortDir"></param>
        /// <returns></returns>
        IContextOrder Add(string name, SortDir sortDir = SortDir.Asc);
    }
}