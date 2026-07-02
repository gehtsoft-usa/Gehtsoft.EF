using System;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Mapper;
using Gehtsoft.EF.Mapper.Validator;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    /// <summary>
    /// Targeted coverage for the <c>Gehtsoft.EF.Mapper.Validator</c> bridge paths the scenario
    /// suite (<see cref="TestModelValidator"/>) does not reach: the attribute <c>WithCode</c>
    /// branches of the <see cref="EfModelValidator{T}"/> constructor, the ASP.NET / null
    /// message-provider branches of <see cref="EfModelValidator{T}.ValidateModel"/>, the guard
    /// throws of every <c>RuleBuilderExtension</c> rule (no <c>[MapEntity]</c> → ArgumentException,
    /// unmapped property → InvalidOperationException), the <c>UnlessValue</c> branch of
    /// <c>AddUnique</c>, and the two obsolete misspelled <c>MustBeUnqiue</c> overloads.
    /// Reuses the entities / models / specifics declared in <see cref="TestModelValidator"/>.
    /// </summary>
    public class TestEfMapperValidatorCoverage
    {
        private static TestModelValidator.DummySqlSpecifics Specifics() => new TestModelValidator.DummySqlSpecifics();

        // ------------------------------------------------------- models under test

        // no [MapEntity] at all -> every bridge rule throws ArgumentException
        public class NoMapEntityModel
        {
            public int? ID { get; set; }
            public string Name { get; set; }
        }

        // mapped, but "Extra" is not a mapped property -> bridge rules throw InvalidOperationException
        [MapEntity(EntityType = typeof(TestModelValidator.Dictionary))]
        public class UnmappedPropModel
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty]
            public string Name { get; set; }

            public string Extra { get; set; }
        }

        [MapEntity(EntityType = typeof(TestModelValidator.Dictionary))]
        public class DictCodesModel
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty]
            [MustHaveValidDbSize(WithCode = 101)]
            [MustBeUnique(WithCode = 102)]
            public string Name { get; set; }
        }

        [MapEntity(EntityType = typeof(TestModelValidator.Entity))]
        public class EntityCodesModel
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty]
            [MustExist(WithCode = 201)]
            public int? Reference { get; set; }

            [MapProperty]
            [MustBeInDbValueRange(WithCode = 202)]
            public double? NumericValue { get; set; }
        }

        // --------------------------------------------------- EfModelValidator ctor WithCode

        [Fact]
        public void Constructor_Applies_Attribute_WithCode()
        {
            // building these validators runs the HasCode -> WithCode branch for all four attributes
            // (existing suites only set WithMessage, never WithCode).
            var dictValidator = new EfModelValidator<DictCodesModel>(Specifics());
            var entityValidator = new EfModelValidator<EntityCodesModel>(Specifics());

            dictValidator.Should().NotBeNull();
            entityValidator.Should().NotBeNull();

            // the size rule for Name fires with the attribute-supplied code
            ValidationResult result = dictValidator.Validate(new DictCodesModel { ID = 0, Name = new string('x', 256) });
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(DictCodesModel.Name), 101).Should().BeTrue();
        }

        // ------------------------------------------------- ValidateModel branches

        [Fact]
        public void ValidateModel_Throws_When_Model_Has_No_MapEntity()
        {
            var validator = new EfModelValidator<NoMapEntityModel>();
            ((Action)(() => validator.ValidateModel()))
                .Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ValidateModel_AspNet_And_Null_MessageProvider_Build_Rules()
        {
            using SqlDbConnection connection = InitConnection();
            var factory = new ValidatorSingletonConnectionFactory(connection);

            // aspNetValidation:true -> non-nullable string uses NotNullOrEmpty (line 96); a null
            // message provider exercises the null branch of every `messageProvider?.GetMessage`.
            var dictValidator = new EfModelValidator<TestModelValidator.DictionaryModel1>(Specifics(), factory);
            dictValidator.ValidateModel(null, aspNetValidation: true);

            var entityValidator = new EfModelValidator<TestModelValidator.EntityModel1>(Specifics(), factory);
            entityValidator.ValidateModel(null, aspNetValidation: true);

            // a null-property object trips the NotNull(OrEmpty) rules built above
            dictValidator.Validate(new TestModelValidator.DictionaryModel1 { ID = null, Name = null })
                .IsValid.Should().BeFalse();

            // a fully valid object passes every rule, incl. the unique / foreign-key DB rules
            var validDict = new TestModelValidator.DictionaryModel1 { ID = 0, Name = "brand_new" };
            dictValidator.Validate(validDict).IsValid.Should().BeTrue();

            var validEntity = new TestModelValidator.EntityModel1
            {
                ID = 1,
                Reference = 1,          // exists (inserted by InitConnection)
                SecondReference = null,
                NumericValue = 1.0,
                DateTimeValue = new DateTime(2020, 1, 1),
                NullableNumericValue = null,
                NullableDateTimeValue = null,
            };
            entityValidator.Validate(validEntity).IsValid.Should().BeTrue();
        }

        private static SqlDbConnection InitConnection()
        {
            SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var query = connection.GetCreateEntityQuery<TestModelValidator.Dictionary>())
                query.Execute();
            using (var query = connection.GetCreateEntityQuery<TestModelValidator.Entity>())
                query.Execute();
            using (var query = connection.GetInsertEntityQuery<TestModelValidator.Dictionary>())
                query.Execute(new TestModelValidator.Dictionary { Name = "Record1" }); // -> ID 1
            return connection;
        }

        // ----------------------------------------- RuleBuilderExtension guard throws

        [Fact]
        public void Rules_Throw_When_Model_Has_No_MapEntity()
        {
            var validator = new EfModelValidator<NoMapEntityModel>();

            ((Action)(() => validator.RuleFor(nameof(NoMapEntityModel.Name)).MustHaveValidDbSize()))
                .Should().Throw<ArgumentException>();
            ((Action)(() => validator.RuleFor(nameof(NoMapEntityModel.Name)).MustBeInValidDbRange()))
                .Should().Throw<ArgumentException>();
            ((Action)(() => validator.RuleFor(nameof(NoMapEntityModel.Name)).MustBeUnique()))
                .Should().Throw<ArgumentException>();
            ((Action)(() => validator.RuleFor(nameof(NoMapEntityModel.Name)).MustExists()))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Rules_Throw_When_Property_Is_Not_Mapped()
        {
            var validator = new EfModelValidator<UnmappedPropModel>(Specifics());

            ((Action)(() => validator.RuleFor(nameof(UnmappedPropModel.Extra)).MustHaveValidDbSize()))
                .Should().Throw<InvalidOperationException>();
            ((Action)(() => validator.RuleFor(nameof(UnmappedPropModel.Extra)).MustBeInValidDbRange()))
                .Should().Throw<InvalidOperationException>();
            ((Action)(() => validator.RuleFor(nameof(UnmappedPropModel.Extra)).MustBeUnique()))
                .Should().Throw<InvalidOperationException>();
            ((Action)(() => validator.RuleFor(nameof(UnmappedPropModel.Extra)).MustExists()))
                .Should().Throw<InvalidOperationException>();
        }

        // -------------------------------------------------- AddUnique UnlessValue branch

        [Fact]
        public void MustBeUnique_Preserves_Existing_UnlessValue_Condition()
        {
            var validator = new EfModelValidator<TestModelValidator.DictionaryModel1>(Specifics());
            // an UnlessValue condition set before MustBeUnique takes the else-if branch of AddUnique
            validator.RuleFor(nameof(TestModelValidator.DictionaryModel1.Name))
                .UnlessValue(new IsNullPredicate(typeof(string)))
                .MustBeUnique();
        }

        // ----------------------------- bridge rules on a non-EfModelValidator validator

        public class PlainValidator<T> : AbstractValidator<T>
        {
        }

        [Fact]
        public void Rules_On_NonEfModelValidator_Resolve_Null_Specifics_And_Factory()
        {
            // the validator is a plain AbstractValidator, not an EfModelValidator<EntityType>, so
            // GetLanguageSpecifics / GetConnectionFactory take their "return null" branches.
            var validator = new PlainValidator<TestModelValidator.EntityModel1>();

            validator.RuleFor(x => x.DateTimeValue).MustBeInValidDbRange(); // GetLanguageSpecifics -> null
            validator.RuleFor(x => x.Reference).MustExists();               // GetConnectionFactory -> null
            validator.RuleFor(x => x.ID).MustBeUnique();                    // GetConnectionFactory -> null
        }

        // ------------------------------------------- obsolete MustBeUnqiue overloads

        [Fact]
        public void Obsolete_MustBeUnqiue_Overloads_Delegate_To_MustBeUnique()
        {
            var validator = new EfModelValidator<TestModelValidator.DictionaryModel1>(Specifics());
#pragma warning disable CS0618 // testing the obsolete spelling on purpose
            validator.RuleFor(nameof(TestModelValidator.DictionaryModel1.Name)).MustBeUnqiue();
            validator.RuleFor(x => x.Name).MustBeUnqiue();
#pragma warning restore CS0618
        }
    }
}
