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
        string RemoteScript(Type expressionCompilerType);
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
