namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The query that has a condition (filter).
    /// </summary>
    public interface IContextQueryWithCondition : IEntityQuery
    {
        /// <summary>
        /// The condition.
        /// </summary>
        IContextFilter Where { get; }
    }
}