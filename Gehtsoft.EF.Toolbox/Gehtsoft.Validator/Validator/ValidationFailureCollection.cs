using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.Validator
{
    public class ValidationFailureCollection : IEnumerable<ValidationFailure>
    {
        private List<ValidationFailure> mFailures = null;

        public ValidationFailureCollection()
        {

        }

        public int Count => mFailures?.Count ?? 0;

        public ValidationFailure this[int index]
        {
            get
            {
                if (mFailures == null) 
                    throw new IndexOutOfRangeException();
                return mFailures[index]; 
            }
        }

        public void Add(ValidationFailure failure) => (mFailures ?? (mFailures = new List<ValidationFailure>())).Add(failure);

        public IEnumerator<ValidationFailure> GetEnumerator() => mFailures?.GetEnumerator() ?? new List<ValidationFailure>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class ValidationFailureCollectionExtension
    {
        public static ValidationFailure Find(this ValidationFailureCollection collection, string path, int code)
        {
            foreach (ValidationFailure failure in collection)
                if (failure.Path == path && failure.Code == code)
                    return failure;
            return null;
        }

        public static bool Contains(this ValidationFailureCollection collection, string path, int code)
        {
            foreach (ValidationFailure failure in collection)
                if (failure.Path == path && failure.Code == code)
                    return true;
            return false;
        }

        public static bool Contains(this ValidationFailureCollection collection, string path, string message)
        {
            foreach (ValidationFailure failure in collection)
                if (failure.Path == path && failure.Message == message)
                    return true;
            return false;
        }

    }
}