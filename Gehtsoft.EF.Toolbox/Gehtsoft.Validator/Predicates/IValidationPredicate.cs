using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.Validator
{
    public interface IValidationPredicate
    {
        Type ParameterType { get; }
        bool Validate(object value);

        /// <summary>
        /// Returns the JavaScript expression that evaluates this predicate on the client side.
        /// A predicate that declares <see cref="Side"/> as <see cref="RuleExecutionSide.Both"/> must
        /// return a non-null script; for a rule executed on both sides a null script is treated by
        /// the JS converter as an error rather than as a silent exclusion.
        /// </summary>
        string RemoteScript(Type expressionCompilerType);

        /// <summary>
        /// The side on which the predicate can be executed. Predicates that by their nature cannot
        /// be expressed as a client-side script (opaque delegates, database-backed checks,
        /// reflection-based checks) declare <see cref="RuleExecutionSide.Server"/>.
        /// </summary>
        RuleExecutionSide Side => RuleExecutionSide.Both;

        /// <summary>
        /// When true, <see cref="Validate"/> must be invoked with the whole entity
        /// rather than with the value of the rule's target.
        /// </summary>
        bool ParameterIsEntity => false;
    }

    public interface IValidationPredicateAsync
    {
        Task<bool> ValidateAsync(object value, CancellationToken? token = null);
    }

    public class ValidationPredicateCollection : IEnumerable<IValidationPredicate>
    {
        private List<IValidationPredicate> mPredicates = null;

        public int Count => mPredicates?.Count ?? 0;

        public IValidationPredicate this[int index]
        {
            get
            {
                if (mPredicates == null)
                    throw new InvalidOperationException("There is no predicates set yet");
                return mPredicates[index];
            }
        }

        internal void Add(IValidationPredicate predicate)
        {
            (mPredicates ?? (mPredicates = new List<IValidationPredicate>())).Add(predicate);
        }

        public IEnumerator<IValidationPredicate> GetEnumerator() => mPredicates != null ? mPredicates.GetEnumerator() : (new List<IValidationPredicate>()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
