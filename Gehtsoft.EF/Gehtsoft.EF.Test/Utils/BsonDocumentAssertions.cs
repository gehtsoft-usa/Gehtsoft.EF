using System;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using FluentAssertions.Primitives;
using FluentAssertions.Execution;

namespace Gehtsoft.EF.Test.Utils
{
    public class BsonDocumentAssertions : ReferenceTypeAssertions<BsonDocument, BsonDocumentAssertions>
    {
        public BsonDocumentAssertions(BsonDocument subject) : base(subject)
        {
        }

        protected override string Identifier => "bson";

        public AndConstraint<BsonDocumentAssertions> HaveProperty(string name, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.Contains(name))
                .FailWith("Expected {context:bson} contains the property {0} but it does not", name);

            return new AndConstraint<BsonDocumentAssertions>(this);
        }

        public AndConstraint<BsonDocumentAssertions> HavePropertiesCount(int count, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.Elements.Count() == count)
                .FailWith("Expected {context:bson} contains {0} elements it does not", count);

            return new AndConstraint<BsonDocumentAssertions>(this);
        }

        public AndConstraint<BsonDocumentAssertions> HaveProperty(string name, Func<BsonValue, bool> predicate, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.Contains(name))
                .FailWith("Expected {context:bson} contains the property {0} but it does not", name)
                .Then
                .ForCondition(s => predicate(s[name]))
                .FailWith("Expected {context:bson} contains the property {0} matching the predicate but it does not match", name);

            return new AndConstraint<BsonDocumentAssertions>(this);
        }

        public AndConstraint<BsonDocumentAssertions> HaveProperty(string name, object value, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.Contains(name))
                .FailWith("Expected {context:bson} contains the property {0} but it does not", name)
                .Then
                .ForCondition(s =>
                {
                    var subject = s[name].ValueOf();
                    if (subject == null && value == null)
                        return true;
                    else if (subject == null || value == null)
                        return false;
                    else
                        return subject.Equals(value);
                })
                .FailWith("Expected {context:bson} contains the property {0} that has value {1} but the action value is {2}", name, value, Subject[name]);

            return new AndConstraint<BsonDocumentAssertions>(this);
        }
    }
}
