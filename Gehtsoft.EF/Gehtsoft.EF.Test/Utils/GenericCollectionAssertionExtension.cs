using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.EF.Test.Entity.Utils
{
    public static class GenericCollectionAssertionExtension
    {
        public static AndConstraint<GenericCollectionAssertions<T>> HaveElementMatching<T>(this GenericCollectionAssertions<T> collection, Expression<Func<T, bool>> predicate, string because = null, params object[] args)
        {
            var p = predicate.Compile();
            Execute.Assertion
               .BecauseOf(because, args)
               .Given(() => collection.Subject)
               .ForCondition(e => e.Any(i => p(i)))
               .FailWith("Expected {context:the collection} have element matching {0} but it does not", predicate);

            return new AndConstraint<GenericCollectionAssertions<T>>(collection);
        }

        public static AndConstraint<GenericCollectionAssertions<T>> HaveAllElementsMatching<T>(this GenericCollectionAssertions<T> collection, Expression<Func<T, bool>> predicate, string because = null, params object[] args)
        {
            var p = predicate.Compile();
            Execute.Assertion
               .BecauseOf(because, args)
               .Given(() => collection.Subject)
               .ForCondition(e => e.All(i => p(i)))
               .FailWith("Expected {context:the collection} have element matching {0} but it does not", predicate);

            return new AndConstraint<GenericCollectionAssertions<T>>(collection);
        }

        public static AndConstraint<GenericCollectionAssertions<T>> HaveOneElementAfterTheOther<T>(this GenericCollectionAssertions<T> collection, T one, T two, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => collection.Subject)
                .ForCondition(e =>
                {
                    bool f1 = false, f2 = false;
                    e.ForEach(t =>
                    {
                        if (ReferenceEquals(t, one) || t.Equals(one))
                            f1 = true;
                        if ((ReferenceEquals(t, two) || t.Equals(two)) && f1)
                            f2 = true;
                    });
                    return f2;
                })
                .FailWith("Expected {context:the collection} contain {0} and then {1} but it does not", one, two);

            return new AndConstraint<GenericCollectionAssertions<T>>(collection);
        }

        public static AndConstraint<GenericCollectionAssertions<T>> HaveOneElementAfterTheOther<T>(this GenericCollectionAssertions<T> collection, Expression<Func<T, bool>> one, Expression<Func<T, bool>> two, string because = null, params object[] args)
        {
            var _one = one.Compile();
            var _two = two.Compile();

            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => collection.Subject)
                .ForCondition(e =>
                {
                    bool f1 = false, f2 = false;
                    e.ForEach(t =>
                    {
                        if (_one(t))
                            f1 = true;
                        if (_two(t) && f1)
                            f2 = true;
                    });
                    return f2;
                })
                .FailWith("Expected {context:the collection} contain {0} and then {1} but it does not", one, two);

            return new AndConstraint<GenericCollectionAssertions<T>>(collection);
        }
    }
}
