using System;
using System.Collections;
using System.Collections.Generic;
using Gehtsoft.Validator;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    /// <summary>
    /// Targeted coverage for Gehtsoft.Validator paths the scenario suites do not reach:
    /// the fluent builder methods <c>Null/DoesNotMatch/PhoneNumber/CreditCardNumber</c> and their
    /// predicates, the <c>Otherwise()</c> branches, the entity-level <c>When</c> overloads, the
    /// unused-but-shipped Always/Never predicates and rule/predicate collections, the extra
    /// <see cref="ValidationFailure"/> constructors, the attribute-driven "not empty" rule and the
    /// <see cref="BaseValidator"/> enumeration surface.
    /// </summary>
    public class TestValidatorCoverage
    {
        public class CovEntity
        {
            public int Num { get; set; }
            public string Text { get; set; }
            public string Phone { get; set; }
            public string Card { get; set; }
            public object Ref { get; set; }
        }

        public class CovValidator : AbstractValidator<CovEntity>
        {
            public CovValidator()
            {
                RuleFor(e => e.Ref).Null().WithCode(1);
                RuleFor(e => e.Text).DoesNotMatch("^bad").WithCode(2);
                RuleFor(e => e.Phone).PhoneNumber().WithCode(3);
                RuleFor(e => e.Card).CreditCardNumber().WithCode(4);
            }
        }

        [Fact]
        public void Builder_Predicates_Flag_Invalid_Values()
        {
            var validator = new CovValidator();

            var bad = new CovEntity { Ref = new object(), Text = "badword", Phone = "abc", Card = "1234" };
            var result = validator.Validate(bad);
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(CovEntity.Ref), 1).Should().BeTrue();
            result.Failures.Contains(nameof(CovEntity.Text), 2).Should().BeTrue();
            result.Failures.Contains(nameof(CovEntity.Phone), 3).Should().BeTrue();
            result.Failures.Contains(nameof(CovEntity.Card), 4).Should().BeTrue();
        }

        [Fact]
        public void Builder_Predicates_Accept_Valid_Values()
        {
            var validator = new CovValidator();

            var good = new CovEntity
            {
                Ref = null,
                Text = "good",
                Phone = "5551234567",
                Card = "4111111111111111", // passes the Luhn check
            };
            validator.Validate(good).IsValid.Should().BeTrue();
        }

        // =====================================================================
        // Otherwise()
        // =====================================================================

        public class OtherwiseValidator : AbstractValidator<CovEntity>
        {
            public OtherwiseValidator()
            {
                RuleFor(e => e.Num).WhenValue(v => v > 10).Must(v => v < 100).WithCode(10)
                    .Otherwise().Must(v => v == 0).WithCode(11);
                RuleFor(e => e.Text).UnlessValue(v => v == null).Must(v => v.Length > 2).WithCode(20)
                    .Otherwise().Must(v => true).WithCode(21);
                RuleFor(e => e.Num).WhenEntity(en => en.Num >= 0).Must(v => v < 1000).WithCode(30)
                    .Otherwise().Must(v => true).WithCode(31);
                RuleFor(e => e.Num).UnlessEntity(en => en.Num < 0).Must(v => v < 1000).WithCode(40)
                    .Otherwise().Must(v => true).WithCode(41);
            }
        }

        [Fact]
        public void Otherwise_Builds_The_Complementary_Rule()
        {
            var validator = new OtherwiseValidator();
            // Num == 5 is not > 10, so the WhenValue rule is skipped and its Otherwise (Num == 0) runs
            var result = validator.Validate(new CovEntity { Num = 5, Text = "hi" });
            result.Failures.Contains(nameof(CovEntity.Num), 11).Should().BeTrue();
        }

        [Fact]
        public void Otherwise_Without_Condition_Throws()
        {
            var validator = new AbstractValidator<CovEntity>();
            validator.Invoking(v => v.RuleFor(e => e.Num).Must(val => val > 0).Otherwise())
                .Should().Throw<InvalidOperationException>();
        }

        // =====================================================================
        // Entity-level When / Unless with predicate objects
        // =====================================================================

        [Fact]
        public void Entity_When_Predicate_Gates_All_Rules()
        {
            var withAlways = new AbstractValidator<CovEntity>();
            withAlways.RuleFor(e => e.Num).Must(x => x > 0).WithCode(1);
            ((BaseValidator)withAlways).When(new AlwaysPredicate(typeof(CovEntity)));
            withAlways.Validate(new CovEntity { Num = 0 }).Failures.Contains(nameof(CovEntity.Num), 1).Should().BeTrue();

            var withNever = new AbstractValidator<CovEntity>();
            withNever.RuleFor(e => e.Num).Must(x => x > 0).WithCode(1);
            ((BaseValidator)withNever).When(new NeverPredicate(typeof(CovEntity)));
            withNever.Validate(new CovEntity { Num = 0 }).IsValid.Should().BeTrue(); // gated out
        }

        [Fact]
        public void Entity_When_Function_Overload()
        {
            var validator = new AbstractValidator<CovEntity>();
            validator.RuleFor(e => e.Num).Must(x => x > 0).WithCode(1);
            validator.When(e => e.Num >= 0);
            validator.Validate(new CovEntity { Num = 0 }).Failures.Contains(nameof(CovEntity.Num), 1).Should().BeTrue();
        }

        // =====================================================================
        // BaseValidator enumeration surface
        // =====================================================================

        [Fact]
        public void BaseValidator_Rule_Enumeration()
        {
            var validator = new CovValidator();
            BaseValidator baseValidator = validator;

            baseValidator.RulesCount.Should().BeGreaterThan(0);
            baseValidator.GetRule(0).Should().NotBeNull();

            int enumerated = 0;
            foreach (var rule in baseValidator)
            {
                rule.Should().NotBeNull();
                enumerated++;
            }
            enumerated.Should().Be(baseValidator.RulesCount);

            ((IEnumerable)baseValidator).GetEnumerator().Should().NotBeNull();
        }

        // =====================================================================
        // Attribute-driven "not empty" rule (BaseValidator ctor branch + predicate)
        // =====================================================================

        public class NotEmptyEntity
        {
            [MustBeNotEmpty(WithMessage = "must not be empty", WithCode = 42)]
            public string Value { get; set; }

            // no WithCode assigned -> HasCode stays false, rule keeps the default code
            [MustBeNotEmpty(WithMessage = "also required")]
            public string Other { get; set; }
        }

        [Fact]
        public void MustBeNotEmpty_Attribute_Is_Enforced()
        {
            var validator = new AbstractValidator<NotEmptyEntity>();

            var failed = validator.Validate(new NotEmptyEntity { Value = "", Other = "" });
            failed.IsValid.Should().BeFalse();
            // WithCode now flows through the attribute to the rule
            failed.Failures.Contains(nameof(NotEmptyEntity.Value), 42).Should().BeTrue();
            failed.Failures.Find(nameof(NotEmptyEntity.Value), 42).Message.Should().Be("must not be empty");
            // the property without an explicit code keeps the default (0)
            failed.Failures.Contains(nameof(NotEmptyEntity.Other), 0).Should().BeTrue();

            validator.Validate(new NotEmptyEntity { Value = "x", Other = "y" }).IsValid.Should().BeTrue();
        }

        [Fact]
        public void ValidatorAttribute_WithCode_Sets_HasCode()
        {
            var withoutCode = new MustBeNotEmptyAttribute();
            withoutCode.HasCode.Should().BeFalse();
            withoutCode.WithCode.Should().Be(0);

            var withCode = new MustBeNotEmptyAttribute { WithCode = 7 };
            withCode.HasCode.Should().BeTrue();
            withCode.WithCode.Should().Be(7);
        }

        // =====================================================================
        // Predicates tested directly (incl. shipped-but-unused Always/Never)
        // =====================================================================

        [Fact]
        public void CreditCardNumberPredicate_Covers_All_Branches()
        {
            var predicate = new CreditCardNumberPredicate(typeof(string));

            predicate.Validate(null).Should().BeFalse();          // null
            predicate.Validate(12345).Should().BeFalse();         // not a string
            predicate.Validate("4111-1111-XXXX-1111").Should().BeFalse(); // non-digit char
            predicate.Validate("4111 1111 1111 1111").Should().BeTrue();  // valid Luhn, separators stripped
            predicate.Validate("4111111111111112").Should().BeFalse();    // Luhn checksum fails
            predicate.RemoteScript(typeof(object)).Should().NotBeNull();
        }

        [Fact]
        public void Always_And_Never_Predicates()
        {
            var always = new AlwaysPredicate(typeof(int));
            always.Validate("anything").Should().BeTrue();
            always.ParameterType.Should().Be(typeof(int));
            always.RemoteScript(typeof(object)).Should().Be("true");

            var never = new NeverPredicate(typeof(int));
            never.Validate("anything").Should().BeFalse();
            never.ParameterType.Should().Be(typeof(int));
            never.RemoteScript(typeof(object)).Should().Be("false");
        }

        private sealed class LazySequence : IEnumerable
        {
            private readonly int mCount;
            public LazySequence(int count) => mCount = count;

            public IEnumerator GetEnumerator()
            {
                for (int i = 0; i < mCount; i++)
                    yield return i;
            }
        }

        [Fact]
        public void IsNotNullOrEmpty_Handles_Plain_Enumerable()
        {
            var predicate = new IsNotNullOrEmptyPredicate(typeof(IEnumerable));
            predicate.Validate(new LazySequence(0)).Should().BeFalse(); // empty enumerable
            predicate.Validate(new LazySequence(3)).Should().BeTrue();  // non-empty enumerable
        }

        // =====================================================================
        // ValidationFailure constructors and collection helpers
        // =====================================================================

        [Fact]
        public void ValidationFailure_Constructors()
        {
            var full = new ValidationFailure("name", "path", 7, "msg");
            full.Name.Should().Be("name");
            full.Path.Should().Be("path");
            full.Code.Should().Be(7);
            full.Message.Should().Be("msg");
            full.ToString().Should().Contain("path");

            var byName = new ValidationFailure("name", 8, "msg2");
            byName.Path.Should().Be("name"); // path defaults to name
            byName.Code.Should().Be(8);
            byName.Message.Should().Be("msg2");

            var codeOnly = new ValidationFailure("name", 9);
            codeOnly.Path.Should().Be("name");
            codeOnly.Code.Should().Be(9);
            codeOnly.Message.Should().BeNull();
        }

        [Fact]
        public void ValidationFailureCollection_Find_And_Contains_Edges()
        {
            var collection = new ValidationFailureCollection();
            collection.Add(new ValidationFailure("name", "path", 1, "message"));

            collection.Find("path", 1).Should().NotBeNull();
            collection.Find("path", 999).Should().BeNull();     // wrong code
            collection.Find("missing", 1).Should().BeNull();    // wrong path

            collection.Contains("path", "message").Should().BeTrue();
            collection.Contains("path", "other").Should().BeFalse();

            // an empty collection enumerates cleanly
            var empty = new ValidationFailureCollection();
            empty.Count.Should().Be(0);
            ((IEnumerable)empty).GetEnumerator().Should().NotBeNull();
            foreach (var _ in empty) { }
        }

        // =====================================================================
        // Shipped-but-unused collections: exercise their public read surface
        // =====================================================================

        [Fact]
        public void Attribute_And_Rule_Constructors()
        {
            new MustBeShorterThanAttribute().Length.Should().Be(0);
            new MustBeShorterThanAttribute(5).Length.Should().Be(5);

            new MustMatchAttribute().Pattern.Should().BeNull();
            new MustMatchAttribute("a.+").Pattern.Should().Be("a.+");

            var rule = new GenericValidationRule<CovEntity, int>();
            rule.RuleValueType.Should().Be(typeof(int));
        }

        [Fact]
        public void Unused_Collections_Public_Surface()
        {
            var rules = new ValidationRuleCollection();
            rules.Count.Should().Be(0);
            rules.Invoking(c => { var _ = c[0]; }).Should().Throw<InvalidOperationException>();
            ((IEnumerable)rules).GetEnumerator().Should().NotBeNull();
            foreach (var _ in rules) { }

            var predicates = new ValidationPredicateCollection();
            predicates.Count.Should().Be(0);
            predicates.Invoking(c => { var _ = c[0]; }).Should().Throw<InvalidOperationException>();
            ((IEnumerable)predicates).GetEnumerator().Should().NotBeNull();
            foreach (var _ in predicates) { }
        }
    }
}
