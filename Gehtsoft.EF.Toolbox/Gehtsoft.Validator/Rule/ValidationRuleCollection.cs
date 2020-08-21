using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.Validator
{
    public class ValidationRuleCollection : IEnumerable<ValidationRule>
    {
        private List<ValidationRule> mFailures = null;

        public ValidationRuleCollection()
        {

        }

        public int Count => mFailures?.Count ?? 0;

        public ValidationRule this[int index]
        {
            get
            {
                if (mFailures == null) 
                    throw new IndexOutOfRangeException();
                return mFailures[index]; 
            }
        }

        internal void Add(ValidationRule failure) => (mFailures ?? (mFailures = new List<ValidationRule>())).Add(failure);

        public IEnumerator<ValidationRule> GetEnumerator() => mFailures?.GetEnumerator() ?? new List<ValidationRule>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}