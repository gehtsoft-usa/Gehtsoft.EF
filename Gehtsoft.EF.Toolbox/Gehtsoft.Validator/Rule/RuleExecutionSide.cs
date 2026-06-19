namespace Gehtsoft.Validator
{
    /// <summary>
    /// Defines on which side a validation rule is executed.
    /// </summary>
    public enum RuleExecutionSide
    {
        /// <summary>
        /// The rule is executed on the server side only and is never translated to a client-side script.
        /// </summary>
        Server = 1,

        /// <summary>
        /// The rule is executed on the server side and is also translated to a client-side script.
        /// </summary>
        Both = 3,
    }
}
