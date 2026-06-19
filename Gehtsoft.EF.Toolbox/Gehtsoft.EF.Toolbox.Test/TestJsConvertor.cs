using System;
using System.Collections.Generic;
using System.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using Gehtsoft.EF.Mapper.Validator;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;
using Gehtsoft.Validator.JSConvertor;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    public class TestJsConvertor
    {
        static TestJsConvertor()
        {
            ValidationExpressionCompiler.AddCustomCall((expression, compiler) =>
            {
                if (expression.Object == null && expression.Method.Name == nameof(ModelValidator.IsXXCentury) && expression.Method.DeclaringType == typeof(ModelValidator))
                    return $"(function (e) {{ return e >= 1900 && e < 2000; }})({compiler(expression.Arguments[0])})";
                return null;
            });

            ValidationExpressionCompiler.AddCustomMemberAccess((expression, compiler) =>
            {
                if (expression.Expression?.Type == typeof(ModelValidator) && expression.Member.Name == nameof(ModelValidator.ConstantProperty))
                    return "true";
                return null;
            });
        }

        private static void AssertClientMatchesServer<T>(AbstractValidator<T> validator, T model, params (string Path, string Message)[] expected)
        {
            List<(string Path, string Message)> server = validator.Validate(model).Failures
                .Select(f => (f.Path, f.Message)).ToList();
            List<(string Path, string Message)> client = JsRuleExecutor.Validate(validator.GetJsRules(), model);

            client.Should().BeEquivalentTo(server, "the client-side validation verdict must match the server-side one");
            client.Should().BeEquivalentTo(expected);
        }

        public class Address
        {
            public string City { get; set; }
            public string Zip { get; set; }
        }

        public class Person
        {
            public int? Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string Comment { get; set; }
            public double Age { get; set; }
            public int[] Scores { get; set; }
            public Address Address { get; set; }
            public Address[] Addresses { get; set; }
        }

        private static Person ValidPerson() => new Person()
        {
            Id = 1,
            Name = "John",
            Email = "john@example.com",
            Comment = "plain text",
            Age = 30,
            Scores = new int[] { 15, 20 },
            Address = new Address() { City = "Boston", Zip = "02101" },
            Addresses = new Address[] { new Address() { City = "New York", Zip = "10001" } },
        };

        public class StaticPredicatesValidator : AbstractValidator<Person>
        {
            public StaticPredicatesValidator()
            {
                RuleFor(e => e.Name).NotNullOrWhitespace().WithMessage("name is required");
                RuleFor(e => e.Name).ShorterThan(11).WithMessage("name is too long");
                RuleFor(e => e.Email).EmailAddress().UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("email is malformed");
                RuleFor(e => e.Comment).NotSQLInjection().UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("no sql in comment");
                RuleFor(e => e.Comment).NotHTML().UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("no html in comment");
                RuleFor(e => e.Age).Between(18.0, true, 65.0, false).WithMessage("age is out of range");
            }
        }

        [Fact]
        public void StaticPredicates_ClientMatchesServer()
        {
            var validator = new StaticPredicatesValidator();

            AssertClientMatchesServer(validator, ValidPerson());

            var person = ValidPerson();
            person.Name = "   ";
            AssertClientMatchesServer(validator, person, ("Name", "name is required"));

            person = ValidPerson();
            person.Name = "Bartholomew Goldsmith";
            AssertClientMatchesServer(validator, person, ("Name", "name is too long"));

            person = ValidPerson();
            person.Email = "not-an-email";
            AssertClientMatchesServer(validator, person, ("Email", "email is malformed"));

            person = ValidPerson();
            person.Comment = "Robert'); DROP TABLE Students;--";
            AssertClientMatchesServer(validator, person, ("Comment", "no sql in comment"));

            person = ValidPerson();
            person.Comment = "<script>alert(1)</script>";
            AssertClientMatchesServer(validator, person, ("Comment", "no html in comment"));

            person = ValidPerson();
            person.Age = 65;
            AssertClientMatchesServer(validator, person, ("Age", "age is out of range"));

            person = ValidPerson();
            person.Age = 18;
            AssertClientMatchesServer(validator, person);
        }

        public class ExpressionPredicatesValidator : AbstractValidator<Person>
        {
            public ExpressionPredicatesValidator()
            {
                RuleFor(e => e.Id).Must(v => v.HasValue).WithMessage("id is required");
                RuleFor(e => e.Age).Must(v => v > 16).WithMessage("too young");
                RuleFor(e => e.Name).Must(v => v.Length > 2).WhenValue(v => v != null).WithMessage("name is too short");
                RuleFor(e => e.Name).Must(value => value.Substring(1).All(c => c != value[0])).WhenValue(v => !string.IsNullOrEmpty(v)).WithMessage("name repeats its first character");
                RuleFor(e => e.Scores).Must(v => v.All(c => c > 0)).WhenValue(v => v != null).WithMessage("scores must be positive");
                RuleFor(e => e.Age).EntityMust(e => e.Age >= e.Scores[0]).WhenEntity(e => e.Scores != null).WithMessage("age must be not less than the first score");
            }
        }

        [Fact]
        public void ExpressionPredicates_ClientMatchesServer()
        {
            var validator = new ExpressionPredicatesValidator();

            AssertClientMatchesServer(validator, ValidPerson());

            var person = ValidPerson();
            person.Id = null;
            AssertClientMatchesServer(validator, person, ("Id", "id is required"));

            person = ValidPerson();
            person.Name = "ab";
            AssertClientMatchesServer(validator, person, ("Name", "name is too short"));

            person = ValidPerson();
            person.Name = "aba";
            AssertClientMatchesServer(validator, person, ("Name", "name repeats its first character"));

            person = ValidPerson();
            person.Scores = new int[] { 3, -1 };
            AssertClientMatchesServer(validator, person, ("Scores", "scores must be positive"));

            person = ValidPerson();
            person.Age = 2;
            person.Scores = new int[] { 5, 10 };
            AssertClientMatchesServer(validator, person,
                ("Age", "too young"),
                ("Age", "age must be not less than the first score"));

            person = ValidPerson();
            person.Scores = null;
            AssertClientMatchesServer(validator, person);
        }

        public class ConditionsValidator : AbstractValidator<Person>
        {
            public ConditionsValidator()
            {
                RuleFor(e => e.Email).NotNullOrEmpty().WhenEntity(e => e.Id != null).WithMessage("email is required for registered users");
                RuleFor(e => e.Name).NotNullOrWhitespace().UnlessEntity(e => e.Id == null).WithMessage("name is required for registered users");
                RuleFor(e => e.Age).Must(v => v >= 18).WhenValue(v => v > 0).WithMessage("specified age must be adult");
            }
        }

        [Fact]
        public void Conditions_GateRulesIdenticallyOnBothSides()
        {
            var validator = new ConditionsValidator();

            var person = new Person() { Id = null, Email = null, Name = null, Age = 0 };
            AssertClientMatchesServer(validator, person);

            person = new Person() { Id = 5, Email = "", Name = " ", Age = 20 };
            AssertClientMatchesServer(validator, person,
                ("Email", "email is required for registered users"),
                ("Name", "name is required for registered users"));

            person = new Person() { Id = null, Age = 10 };
            AssertClientMatchesServer(validator, person, ("Age", "specified age must be adult"));

            person = new Person() { Id = 5, Email = "x@y.zz", Name = "Bob", Age = 33 };
            AssertClientMatchesServer(validator, person);
        }

        public class ChainValidator : AbstractValidator<Person>
        {
            public ChainValidator()
            {
                RuleFor(e => e.Age)
                    .WhenEntity(e => string.IsNullOrEmpty(e.Name))
                    .Must(v => v > 21).WithMessage("anonymous user must be over 21")
                    .Otherwise().EntityMust(e => e.Id.HasValue).WithMessage("named user must have an id")
                    .Also().EntityMust(e => e.Age > 10).WithMessage("named user must be over 10");
            }
        }

        [Fact]
        public void OtherwiseAndAlsoChains_ClientMatchesServer()
        {
            var validator = new ChainValidator();

            AssertClientMatchesServer(validator, new Person() { Name = null, Age = 25 });

            AssertClientMatchesServer(validator, new Person() { Name = null, Age = 18 },
                ("Age", "anonymous user must be over 21"));

            AssertClientMatchesServer(validator, new Person() { Name = "Bob", Id = null, Age = 8 },
                ("Age", "named user must have an id"),
                ("Age", "named user must be over 10"));

            AssertClientMatchesServer(validator, new Person() { Name = "Bob", Id = 7, Age = 30 });
        }

        public class AddressValidator : AbstractValidator<Address>
        {
            public AddressValidator()
            {
                RuleFor(e => e.City).NotNullOrWhitespace().WithMessage("city is required");
                RuleFor(e => e.Zip).DoesMatch("^[0-9]{5}$").UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("zip must be 5 digits");
            }
        }

        public class NestedPersonValidator : AbstractValidator<Person>
        {
            public NestedPersonValidator()
            {
                RuleFor(e => e.Address).ValidateUsing<AddressValidator>();
                RuleForAll(e => e.Addresses).ValidateUsing<AddressValidator>();
                RuleForAll(e => e.Scores).Must(v => v > 10).WithMessage("score must be above 10");
            }
        }

        [Fact]
        public void NestedAndArrayRules_TargetsAndFlags()
        {
            JsValidatorRule[] rules = new NestedPersonValidator().GetJsRules();

            rules.Select(r => (r.JsTargetName, r.ArrayValidator)).Should().BeEquivalentTo(new[]
            {
                ("Address.City", false),
                ("Address.Zip", false),
                ("Addresses[index].City", true),
                ("Addresses[index].Zip", true),
                ("Scores", true),
            });
        }

        [Fact]
        public void NestedAndArrayRules_ClientMatchesServer()
        {
            var validator = new NestedPersonValidator();

            AssertClientMatchesServer(validator, ValidPerson());

            var person = ValidPerson();
            person.Address = new Address() { City = "" };
            AssertClientMatchesServer(validator, person, ("Address.City", "city is required"));

            person = ValidPerson();
            person.Addresses = new Address[]
            {
                new Address() { City = "Boston", Zip = "02101" },
                new Address() { City = "LA", Zip = "abc" },
            };
            AssertClientMatchesServer(validator, person, ("Addresses[1].Zip", "zip must be 5 digits"));

            person = ValidPerson();
            person.Scores = new int[] { 20, 5, 30 };
            AssertClientMatchesServer(validator, person, ("Scores[1]", "score must be above 10"));
        }

        public class ExclusionValidator : AbstractValidator<Person>
        {
            public ExclusionValidator()
            {
                RuleFor(e => e.Name).NotNull().WithMessage("client rule");
                RuleForEntity().Must(e => e.Address != null).WithMessage("server-only entity rule").ServerOnly();
                RuleFor(e => e.Comment).Must(new FunctionPredicate<string>(v => v == null || v.Length < 20)).WithMessage("delegate rule");
                RuleFor(e => e.Age).Must(v => v >= 0).WithMessage("explicit server rule").SetSide(RuleExecutionSide.Server);
            }
        }

        [Fact]
        public void ServerSideRules_AreExcludedFromJsRules()
        {
            var validator = new ExclusionValidator();

            JsValidatorRule[] rules = validator.GetJsRules();
            rules.Should().ContainSingle().Which.JsTargetName.Should().Be("Name");

            // the excluded rules still work on the server
            var person = new Person() { Name = "Bob", Address = null, Comment = new string('x', 30), Age = -1 };
            validator.Validate(person).Failures.Select(f => f.Message).Should().BeEquivalentTo(
                "server-only entity rule", "delegate rule", "explicit server rule");
        }

        private sealed class OpaquePredicate : IValidationPredicate
        {
            public Type ParameterType => typeof(string);
            public bool Validate(object value) => true;
            public string RemoteScript(Type compilerType) => null;
            // Side intentionally stays at the interface default (Both): the predicate
            // claims client support but produces no script
        }

        public class UntranslatableValidatorValidator : AbstractValidator<Person>
        {
            public UntranslatableValidatorValidator()
            {
                RuleFor(e => e.Name).Must(new OpaquePredicate()).WithMessage("opaque");
            }
        }

        [Fact]
        public void UntranslatableValidationPredicate_Throws()
        {
            Action act = () => new UntranslatableValidatorValidator().GetJsRules();
            act.Should().Throw<InvalidOperationException>().WithMessage("*validation predicate*Name*");
        }

        public class UntranslatableConditionValidator : AbstractValidator<Person>
        {
            public UntranslatableConditionValidator()
            {
                RuleFor(e => e.Name).NotNull().WhenValue(new OpaquePredicate()).WithMessage("guarded");
            }
        }

        [Fact]
        public void UntranslatableConditionPredicate_Throws()
        {
            Action act = () => new UntranslatableConditionValidator().GetJsRules();
            act.Should().Throw<InvalidOperationException>().WithMessage("*condition predicate*Name*");
        }

        public class EmptyPersonValidator : AbstractValidator<Person>
        {
        }

        [Fact]
        public void SetSideBoth_OnServerOnlyPredicate_ThrowsAtBuildTime()
        {
            var validator = new EmptyPersonValidator();

            Action act = () => validator.RuleFor(e => e.Comment)
                .Must(new FunctionPredicate<string>(v => v != null))
                .SetSide(RuleExecutionSide.Both);
            act.Should().Throw<InvalidOperationException>();

            Action act2 = () => validator.RuleFor(e => e.Comment)
                .SetSide(RuleExecutionSide.Both)
                .Must(new FunctionPredicate<string>(v => v != null));
            act2.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ServerOnly_IsEquivalentToSetSideServer()
        {
            var validator = new EmptyPersonValidator();
            var builder = validator.RuleFor(e => e.Name).NotNull().ServerOnly();

            builder.Rule.Side.Should().Be(RuleExecutionSide.Server);
            builder.Rule.IgnoreOnClient.Should().BeTrue();
        }

        public static bool LooksGood(string value) => true;

        public class UnsupportedCallValidator : AbstractValidator<Person>
        {
            public UnsupportedCallValidator()
            {
                RuleFor(e => e.Name).Must(v => LooksGood(v)).WithMessage("unsupported");
            }
        }

        [Fact]
        public void UnsupportedExpressionConstruct_Throws()
        {
            Action act = () => new UnsupportedCallValidator().GetJsRules();
            act.Should().Throw<Exception>();
        }

        // --- integration: EfModelValidator with EF metadata rules, nested validators,
        // array rules and custom compiler hooks ---

        [Entity]
        public class Entity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }

            [EntityProperty(Size = 4, Precision = 1)]
            public double Age { get; set; }
        }

        public class SubModel
        {
            public string Email { get; set; }
            public bool AllowEmptyName { get; set; }
            public bool AllowAllowEmptyName { get; set; }
        }

        [MapEntity(typeof(Entity))]
        public class Model
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty]
            public string Name { get; set; }

            [MapProperty]
            public double Age { get; set; }

            public int[] Arr { get; set; }

            public DateTime? Dt { get; set; }

            public SubModel SubModel { get; set; }

            public int[] Array1 { get; set; }

            public SubModel[] Array2 { get; set; }
        }

        public class SubModelValidator : AbstractValidator<SubModel>
        {
            public SubModelValidator()
            {
                RuleFor(e => e.Email).EmailAddress().UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("Must be a correct email");
                RuleFor(e => e.Email).NotSQLInjection().UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("Incorrect symbols 1");
                RuleFor(e => e.Email).NotHTML().UnlessValue(v => string.IsNullOrEmpty(v)).WithMessage("Incorrect symbols 2");
                RuleFor(e => e.AllowEmptyName).Must(v => !v).UnlessEntity(e => e.AllowAllowEmptyName).WithMessage("Allowing an empty name isn't allowed");
            }
        }

        public class ModelValidator : EfModelValidator<Model>
        {
            public ModelValidator() : base()
            {
                ValidateModel(new DefaultEfValidatorMessageProvider());
                RuleForEntity().Must(e => e.SubModel != null).WithMessage("SubModel is required").ServerOnly();
                RuleFor(e => e.ID).Must(v => v.HasValue).WithMessage("ID is required");
                RuleFor(e => e.Name)
                    .NotNullOrWhitespace()
                    .UnlessEntity(e => e.SubModel != null && e.SubModel.AllowAllowEmptyName)
                    .WithMessage("Name must not be empty");
                RuleFor(e => e.Name)
                    .Must(value => value.Substring(1).All(c => c != value[0]))
                    .WhenValue(v => !string.IsNullOrEmpty(v))
                    .WithMessage("Name must not repeat its first character");
                RuleFor(e => e.Age).Must(v => v > 16).WithMessage("Age must be more than 16");
                RuleFor(e => e.Age)
                    .Must(new ValueIsBetweenPredicate(typeof(double), 1.0, true, 100.0, false))
                    .WithMessage("Age must be between 1 and 100");
                RuleFor(e => e.SubModel).ValidateUsing<SubModelValidator>();
                RuleFor(e => e.Dt)
                    .Must(v => IsXXCentury(v.Value.Year) && ConstantProperty)
                    .WhenValue(v => v.HasValue)
                    .WithMessage("Date must be in the XX century");
                RuleFor(e => e.Arr)
                    .Must(v => v.All(c => c >= v[0]))
                    .WhenValue(v => v != null)
                    .WithMessage("All elements must be not less than the first one");
                RuleForAll(e => e.Array1).Must(v => v > 10).WithMessage("Must be above 10");
                RuleForAll(e => e.Array2).ValidateUsing<SubModelValidator>();
            }

            public static bool IsXXCentury(int year) => year >= 1900 && year < 2000;

            public bool ConstantProperty => true;
        }

        private static Model ValidModel() => new Model()
        {
            ID = 1,
            Name = "John",
            Age = 25,
            SubModel = new SubModel() { Email = "john@example.com" },
            Arr = new int[] { 5, 7, 9 },
            Dt = new DateTime(1955, 5, 5),
            Array1 = new int[] { 11, 12 },
            Array2 = new SubModel[] { new SubModel() { Email = "a@b.com" }, new SubModel() { Email = "" } },
        };

        [Fact]
        public void EfModelValidator_ValidModelPassesOnBothSides()
        {
            AssertClientMatchesServer(new ModelValidator(), ValidModel());
        }

        [Fact]
        public void EfModelValidator_ClientMatchesServer()
        {
            var validator = new ModelValidator();

            var model = ValidModel();
            model.Age = 10;
            AssertClientMatchesServer(validator, model, ("Age", "Age must be more than 16"));

            model = ValidModel();
            model.Age = 0;
            AssertClientMatchesServer(validator, model,
                ("Age", "Age must be more than 16"),
                ("Age", "Age must be between 1 and 100"));

            model = ValidModel();
            model.ID = null;
            AssertClientMatchesServer(validator, model,
                ("ID", "The value must not be empty"),
                ("ID", "ID is required"));

            model = ValidModel();
            model.Name = "aba";
            AssertClientMatchesServer(validator, model, ("Name", "Name must not repeat its first character"));

            model = ValidModel();
            model.Name = "";
            model.SubModel.AllowAllowEmptyName = true;
            AssertClientMatchesServer(validator, model);

            model = ValidModel();
            model.Dt = new DateTime(2005, 1, 1);
            AssertClientMatchesServer(validator, model, ("Dt", "Date must be in the XX century"));

            model = ValidModel();
            model.Dt = null;
            AssertClientMatchesServer(validator, model);

            model = ValidModel();
            model.Arr = new int[] { 5, 3, 9 };
            AssertClientMatchesServer(validator, model, ("Arr", "All elements must be not less than the first one"));

            model = ValidModel();
            model.SubModel.Email = "<b>x</b>@bad";
            AssertClientMatchesServer(validator, model,
                ("SubModel.Email", "Must be a correct email"),
                ("SubModel.Email", "Incorrect symbols 2"));

            model = ValidModel();
            model.SubModel.AllowEmptyName = true;
            AssertClientMatchesServer(validator, model, ("SubModel.AllowEmptyName", "Allowing an empty name isn't allowed"));

            model = ValidModel();
            model.Array1 = new int[] { 11, 5, 12 };
            AssertClientMatchesServer(validator, model, ("Array1[1]", "Must be above 10"));

            model = ValidModel();
            model.Array2[1].Email = "not-an-email";
            AssertClientMatchesServer(validator, model, ("Array2[1].Email", "Must be a correct email"));
        }

        [Fact]
        public void EfModelValidator_EntityMetadataRules_ClientMatchesServer()
        {
            var validator = new ModelValidator();

            // Name longer than the entity property size (32); the message text comes from
            // DefaultEfValidatorMessageProvider, so assert parity and shape, not the text
            var model = ValidModel();
            model.Name = "B" + new string('x', 2) + "abcdefghijklmnopqrstuvwxyz0123456789";
            model.Name.Length.Should().BeGreaterThan(32);

            var server = validator.Validate(model).Failures.Select(f => (f.Path, f.Message)).ToList();
            var client = JsRuleExecutor.Validate(validator.GetJsRules(), model);

            server.Should().NotBeEmpty("the entity metadata limits the name to 32 characters");
            server.Should().OnlyContain(f => f.Path == "Name");
            client.Should().BeEquivalentTo(server);
        }

        [Fact]
        public void CustomCompilerHooks_AreEmbeddedInGeneratedScript()
        {
            JsValidatorRule[] rules = new ModelValidator().GetJsRules();

            JsValidatorRule dtRule = rules.Single(r => r.JsTargetName == "Dt");
            dtRule.JsValidationExpression.Should().Contain("getFullYear");
            dtRule.JsValidationExpression.Should().Contain("e >= 1900 && e < 2000");
            dtRule.JsWhenExpression.Should().NotBeNull();
        }

        [Fact]
        public void ServerOnlyEntityRule_IsExcludedFromJsRules()
        {
            JsValidatorRule[] rules = new ModelValidator().GetJsRules();
            rules.Should().NotContain(r => r.ErrorMessage == "SubModel is required");
        }
    }
}
