using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;

namespace Gehtsoft.EF.Test.Entity.Utils
{
    public static class GenericDictionaryAssertionExtension
    {
        public static AndConstraint<GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV>> HaveNoElement<TK, TV>(this GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV> assertions, TK key, string because = null, params object[] args)
        {
            assertions.CurrentAssertionChain
                .BecauseOf(because, args)
                .Given(() => assertions.Subject)
                .ForCondition(d => !d.ContainsKey(key))
                .FailWith("Expected dictionary contain no key {0} but it does", key);

            return new AndConstraint<GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV>>(assertions);
        }

        public static AndConstraint<GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV>> HaveElement<TK, TV>(this GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV> assertions, TK key, TV value, string because = null, params object[] args)
        {
            assertions.CurrentAssertionChain
                .BecauseOf(because, args)
                .Given(() => assertions.Subject)
                .ForCondition(d => d.ContainsKey(key))
                .FailWith("Expected dictionary contain key {0} but it does not", key)
                .Then
                .ForCondition(d =>
                {
                    var subject = d[key];
                    if (subject == null && value == null)
                        return true;
                    else if (subject == null || value == null)
                        return false;
                    else
                    {
                        object value1;
                        if (subject.GetType() != value.GetType() && subject.GetType().IsValueType)
                            value1 = Convert.ChangeType(value, subject.GetType());
                        else
                            value1 = value;
                        return subject.Equals(value1);
                    }
                })
                .FailWith("Expected value with the key {0} to be {1} but it is {2}", key, value, assertions.Subject[key]);

            return new AndConstraint<GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV>>(assertions);
        }

        public static AndConstraint<GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV>> HaveElementMatching<TK, TV>(this GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV> assertions, TK key, Func<TV, bool> predicate, string because = null, params object[] args)
        {
            assertions.CurrentAssertionChain
                .BecauseOf(because, args)
                .Given(() => assertions.Subject)
                .ForCondition(d => d.ContainsKey(key))
                .FailWith("Expected dictionary contain key {0} but it does not", key)
                .Then
                .ForCondition(d => predicate(d[key]))
                .FailWith("Expected value with the key {0} to match the predicate, but its value does not", key);

            return new AndConstraint<GenericDictionaryAssertions<IDictionary<TK, TV>, TK, TV>>(assertions);
        }
    }
}
