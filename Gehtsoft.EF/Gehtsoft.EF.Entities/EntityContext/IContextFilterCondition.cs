namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// A single condition inside the filter expression.
    ///
    ///
    /// Example:
    /// ```cs
    /// condition.Property("a").Is(CmpOp.Eq).Value(1);
    /// condition.Property("a").Is(CmpOp.Le).Property(b);
    /// ```
    ///
    /// See also <see cref="IContextFilter"/>.
    ///
    /// Use <see cref="EntityFilterConditionExtension"/> for more readable
    /// definition of the conditions.
    /// </summary>
    public interface IContextFilterCondition
    {
        /// <summary>
        /// Adds a property to the condition.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IContextFilterCondition Property(string name);

        /// <summary>
        /// Sets the operation.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        IContextFilterCondition Is(CmpOp op);

        /// <summary>
        /// Adds the value of the condition.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        IContextFilterCondition Value(object value);
    }
}