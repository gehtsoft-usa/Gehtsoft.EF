using System;
using FluentAssertions;
using MongoDB.Bson;
using FluentAssertions.Primitives;
using FluentAssertions.Execution;

namespace Gehtsoft.EF.Test.Utils
{
    public class BsonValueAssertions : ReferenceTypeAssertions<BsonValue, BsonValueAssertions>
    {
        public BsonValueAssertions(BsonValue subject) : base(subject)
        {
        }

        protected override string Identifier => "value";

        public AndConstraint<BsonValueAssertions> BeOfBsonType(BsonType type, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.BsonType == type)
                .FailWith("Expected {context:value} to be a {0} but it is {1}", type, Subject.BsonType);

            return new AndConstraint<BsonValueAssertions>(this);
        }

        public AndConstraint<BsonValueAssertions> BeInt32(string because = null, params object[] args) => BeOfBsonType(BsonType.Int32, because, args);
        public AndConstraint<BsonValueAssertions> BeInt64(string because = null, params object[] args) => BeOfBsonType(BsonType.Int64, because, args);
        public AndConstraint<BsonValueAssertions> BeDouble(string because = null, params object[] args) => BeOfBsonType(BsonType.Double, because, args);
        public AndConstraint<BsonValueAssertions> BeDecimal(string because = null, params object[] args) => BeOfBsonType(BsonType.Decimal128, because, args);
        public AndConstraint<BsonValueAssertions> BeArray(string because = null, params object[] args) => BeOfBsonType(BsonType.Array, because, args);
        public AndConstraint<BsonValueAssertions> BeBoolean(string because = null, params object[] args) => BeOfBsonType(BsonType.Boolean, because, args);
        public AndConstraint<BsonValueAssertions> BeDatetime(string because = null, params object[] args) => BeOfBsonType(BsonType.DateTime, because, args);
        public AndConstraint<BsonValueAssertions> BeString(string because = null, params object[] args) => BeOfBsonType(BsonType.String, because, args);
        public AndConstraint<BsonValueAssertions> BeDocument(string because = null, params object[] args) => BeOfBsonType(BsonType.Document, because, args);

        public AndConstraint<BsonValueAssertions> HaveProperty(string name, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.IsBsonDocument)
                .FailWith("Expected {context:value} to be a document but it is not")
                .Then
                .ForCondition(s => s.AsBsonDocument.Contains(name))
                .FailWith("Expected {context:value} contains the property {0} but it does not", name);

            return new AndConstraint<BsonValueAssertions>(this);
        }

        public AndConstraint<BsonValueAssertions> HavePropertiesCount(int count, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.IsBsonDocument)
                .FailWith("Expected {context:value} to be a document but it is not")
                .Then
                .ForCondition(s => s.AsBsonDocument.ElementCount == count)
                .FailWith("Expected {context:value} contains the {0} properties but it does not", count);

            return new AndConstraint<BsonValueAssertions>(this);
        }

        public AndConstraint<BsonValueAssertions> HaveCount(int count, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.IsBsonArray)
                .FailWith("Expected {context:value} to be an array but it is not")
                .Then
                .ForCondition(s => s.AsBsonArray.Count == count)
                .FailWith("Expected {context:value} have {0} elements but it does not", count);

            return new AndConstraint<BsonValueAssertions>(this);
        }

        public AndConstraint<BsonValueAssertions> HavePropertyMatching(string name, Func<BsonValue, bool> predicate, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.IsBsonDocument)
                .FailWith("Expected {context:value} to be a document but it is not")
                .Then
                .ForCondition(s => s.AsBsonDocument.Contains(name))
                .FailWith("Expected {context:value} contains the property {0} but it does not", name)
                .Then
                .ForCondition(s => predicate(s.AsBsonDocument[name]))
                .FailWith("Expected {context:value} contains the property {0} that matches the predicate but it does not", name);

            return new AndConstraint<BsonValueAssertions>(this);
        }

        public AndConstraint<BsonValueAssertions> HaveProperty(string name, object value, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.IsBsonDocument)
                .FailWith("Expected {context:value} to be a document but it is not")
                .Then
                .ForCondition(s => s.AsBsonDocument.Contains(name))
                .FailWith("Expected {context:value} contains the property {0} but it does not", name)
                .Then
                .ForCondition(s =>
                {
                    var subject = s.AsBsonDocument[name].ValueOf();

                    bool eq;
                    if (subject == null && value == null)
                        eq = true;
                    else if (subject == null || value == null)
                        eq = false;
                    else
                        eq = subject.Equals(value);

                    return eq;
                })
                .FailWith("Expected {context:value} contains the property {0} that has value {1} but the actual value is {2}", name, value, Subject.AsBsonDocument[name]);

            return new AndConstraint<BsonValueAssertions>(this);
        }

        public AndConstraint<BsonValueAssertions> HaveElement(int index, object value, string because = null, params object[] args)
        {
            Execute.Assertion
                .BecauseOf(because, args)
                .Given(() => Subject)
                .ForCondition(s => s.IsBsonArray)
                .FailWith("Expected {context:value} to be an array but it is not")
                .Then
                .ForCondition(s => s.AsBsonArray.Count > index)
                .FailWith("Expected {context:value} contains the at {0} least elements  but it does not", index + 1)
                .Then
                .ForCondition(s =>
                {
                    var subject = s.AsBsonArray[index].ValueOf();

                    bool eq;
                    if (subject == null && value == null)
                        eq = true;
                    else if (subject == null || value == null)
                        eq = false;
                    else
                        eq = subject.Equals(value);

                    return eq;
                })
                .FailWith("Expected {context:value} contains the element {0} that has value {1} but the actual value is {2}", index, value, Subject.AsBsonArray[index]);

            return new AndConstraint<BsonValueAssertions>(this);
        }

        public AndConstraint<BsonValueAssertions> HaveValue(object value, string because = null, params object[] args)
        {
            var subject = Subject.ValueOf();

            bool eq;
            if (subject == null && value == null)
                eq = true;
            else if (subject == null || value == null)
                eq = false;
            else
                eq = subject.Equals(value);

            Execute.Assertion
                .BecauseOf(because, args)
                .ForCondition(eq)
                .FailWith("Expected {context:value} to be {0} but it is {1}", value, subject);

            return new AndConstraint<BsonValueAssertions>(this);
        }
    }
}
