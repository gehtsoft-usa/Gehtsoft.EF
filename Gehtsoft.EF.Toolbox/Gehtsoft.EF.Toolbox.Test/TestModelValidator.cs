using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using Gehtsoft.EF.Mapper.Validator;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    public class TestModelValidator
    {
        public class DummySqlSpecifics : SqlDbLanguageSpecifics
        {
            public override DateTime? MinDate => new DateTime(1761, 1, 1);
            public override DateTime? MaxDate => new DateTime(2999, 12, 31);
            public override DateTime? MinTimestamp => new DateTime(1970, 1, 1);
            public override DateTime? MaxTimestamp => new DateTime(2030, 12, 31);
        }

        [Entity(Table = "dictionary")]
        public class Dictionary
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Unique = true, Size = 64, Sorted = true, Nullable = false)]
            public string Name { get; set; }

            public Dictionary()
            {
            }

            public Dictionary(int id)
            {
                ID = id;
            }
        }

        [Entity(Table = "entity")]
        public class Entity
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public Dictionary Reference { get; set; }

            [EntityProperty(ForeignKey = true, Nullable = true)]
            public Dictionary SecondReference { get; set; }

            [EntityProperty(Size = 8, Precision = 4)]
            public double NumericValue { get; set; }

            [EntityProperty(DbType = DbType.Date, Nullable = false)]
            public DateTime DateTimeValue { get; set; }

            [EntityProperty(Size = 9, Precision = 4, Nullable = true)]
            public double? NullableNumericValue { get; set; }

            [EntityProperty(DbType = DbType.DateTime, Nullable = true)]
            public DateTime? NullableDateTimeValue { get; set; }

            public Entity()
            {
            }

            public Entity(int id)
            {
                ID = id;
            }
        }

        public interface IDictionaryModel
        {
            int? ID { get; set; }
            string Name { get; set; }
        }

        [MapEntity(EntityType = typeof(Dictionary))]
        public class DictionaryModel1 : IDictionaryModel
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty]
            public string Name { get; set; }
        }

        [MapEntity(EntityType = typeof(Dictionary))]
        public class DictionaryModel2 : IDictionaryModel
        {
            [MustBeNotNull(WithMessage = "isnull")]
            [MapProperty]
            public int? ID { get; set; }

            [MustBeNotNull(WithMessage = "isnull")]
            [MustHaveValidDbSize(WithMessage = "toolong")]
            [MustBeUnique(WithMessage = "notunique")]
            [MapProperty]
            public string Name { get; set; }
        }

        public interface IEntityModel
        {
            int? ID { get; set; }
            int? Reference { get; set; }
            int? SecondReference { get; set; }
            double? NumericValue { get; set; }
            DateTime? DateTimeValue { get; set; }
            double? NullableNumericValue { get; set; }
            DateTime? NullableDateTimeValue { get; set; }
        }

        [MapEntity(EntityType = typeof(Entity))]
        public class EntityModel1 : IEntityModel
        {
            [MapProperty]
            public int? ID { get; set; }

            [MapProperty()]
            public int? Reference { get; set; }

            [MapProperty()]
            public int? SecondReference { get; set; }

            [MapProperty]
            public double? NumericValue { get; set; }

            [MapProperty]
            public DateTime? DateTimeValue { get; set; }

            [MapProperty]
            public double? NullableNumericValue { get; set; }

            [MapProperty]
            public DateTime? NullableDateTimeValue { get; set; }
        }

        [MapEntity(EntityType = typeof(Entity))]
        public class EntityModel2 : IEntityModel
        {
            [MustBeNotNull(WithMessage = "isnull")]
            [MapProperty]
            public int? ID { get; set; }

            [MustBeNotNull(WithMessage = "isnull")]
            [MustExist(WithMessage = "mustexist")]
            [MapProperty()]
            public int? Reference { get; set; }

            [MustExist(WithMessage = "mustexist")]
            [MapProperty()]
            public int? SecondReference { get; set; }

            [MustBeNotNull(WithMessage = "isnull")]
            [MustBeInDbValueRange(WithMessage = "outofrange")]
            [MapProperty]
            public double? NumericValue { get; set; }

            [MustBeNotNull(WithMessage = "isnull")]
            [MustBeInDbValueRange(WithMessage = "outofrange")]
            [MapProperty]
            public DateTime? DateTimeValue { get; set; }

            [MustBeInDbValueRange(WithMessage = "outofrange")]
            [MapProperty]
            public double? NullableNumericValue { get; set; }

            [MustBeInDbValueRange(WithMessage = "outofrange")]
            [MapProperty]
            public DateTime? NullableDateTimeValue { get; set; }
        }

        public class MessageProvider : IEfValidatorMessageProvider
        {
            public string GetMessage(EntityDescriptor entityDescriptor, TableDescriptor.ColumnInfo column, int validationErrorCode)
            {
                switch (validationErrorCode)
                {
                    case (int)EfValidationErrorCode.NullValue:
                        return "isnull";
                    case (int)EfValidationErrorCode.NumberIsOutOfRange:
                        return "outofrange";
                    case (int)EfValidationErrorCode.DateIsOutRange:
                        return "outofrange";
                    case (int)EfValidationErrorCode.StringIsTooLong:
                        return "toolong";
                    case (int)EfValidationErrorCode.ReferenceDoesNotExists:
                        return "mustexist";
                    case (int)EfValidationErrorCode.ValueIsNotUnique:
                        return "notunique";
                    default:
                        return "unknowncode";
                }
            }
        }

        public class DictionaryValidatorAutoCreate : EfModelValidator<DictionaryModel1>
        {
            public DictionaryValidatorAutoCreate(IValidatorConnectionFactory connectionFactory = null) : base(new DummySqlSpecifics(), connectionFactory)
            {
                ValidateModel(new MessageProvider());
            }
        }

        public class DictionaryValidatorByRule : EfModelValidator<DictionaryModel1>
        {
            public DictionaryValidatorByRule(IValidatorConnectionFactory connectionFactory = null) : base(new DummySqlSpecifics(), connectionFactory)
            {
                RuleFor(nameof(DictionaryModel1.ID)).NotNull().WithMessage("isnull");
                RuleFor(x => x.Name).NotNull().WithMessage("isnull");
                RuleFor(x => x.Name).MustHaveValidDbSize().WithMessage("toolong");
                RuleFor(x => x.Name).WhenValue(x => !string.IsNullOrEmpty(x)).MustBeUnique().WithMessage("notunique");
            }
        }

        public class EntityValidatorByAttributes : EfModelValidator<EntityModel2>
        {
            public EntityValidatorByAttributes(IValidatorConnectionFactory connectionFactory = null) : base(new DummySqlSpecifics(), connectionFactory)
            {
            }
        }

        public class EntityValidatorAutoCreate : EfModelValidator<EntityModel1>
        {
            public EntityValidatorAutoCreate(IValidatorConnectionFactory connectionFactory = null) : base(new DummySqlSpecifics(), connectionFactory)
            {
                ValidateModel(new MessageProvider());
            }
        }

        public class EntityValidatorByRule : EfModelValidator<EntityModel1>
        {
            public EntityValidatorByRule(IValidatorConnectionFactory connectionFactory = null) : base(new DummySqlSpecifics(), connectionFactory)
            {
                RuleFor(a => a.ID).NotNull().WithMessage("isnull");
                RuleFor(a => a.Reference).NotNull().WithMessage("isnull");
                RuleFor(a => a.Reference).MustExists().WithMessage("mustexist");
                RuleFor(a => a.SecondReference).MustExists().WithMessage("mustexist");
                RuleFor(a => a.NumericValue).NotNull().WithMessage("isnull");
                RuleFor(a => a.NumericValue).MustBeInValidDbRange().WithMessage("outofrange");
                RuleFor(a => a.DateTimeValue).NotNull().WithMessage("isnull");
                RuleFor(a => a.DateTimeValue).MustBeInValidDbRange().WithMessage("outofrange");
                RuleFor(a => a.NullableNumericValue).MustBeInValidDbRange().WithMessage("outofrange").WhenNotNull();
                RuleFor(a => a.NullableDateTimeValue).MustBeInValidDbRange().WithMessage("outofrange").WhenNotNull();
            }
        }

        public class DictionaryValidatorByAttributes : EfModelValidator<DictionaryModel2>
        {
            public DictionaryValidatorByAttributes(IValidatorConnectionFactory connectionFactory = null) : base(new DummySqlSpecifics(), connectionFactory)
            {
            }
        }

        private void Test<TDM, TEM>(bool hasConnection, BaseValidator dictionaryValidator, BaseValidator entityValidator)
            where TDM : IDictionaryModel, new()
            where TEM : IEntityModel, new()
        {
            ValidationResult result;

            //validate entity model
            TDM dictionary = new TDM() { ID = null, Name = null };
            result = dictionaryValidator.Validate(dictionary);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(2, result.Failures.Count);
            Assert.IsTrue(result.Failures.Contains(nameof(IDictionaryModel.ID), "isnull"));
            Assert.IsTrue(result.Failures.Contains(nameof(IDictionaryModel.Name), "isnull"));

            dictionary.ID = 0;
            dictionary.Name = new string('a', 256);
            result = dictionaryValidator.Validate(dictionary);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Failures.Count);
            Assert.IsTrue(result.Failures.Contains(nameof(IDictionaryModel.Name), "toolong"));

            TEM entity = new TEM();
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(4, result.Failures.Count);
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.ID), "isnull"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.Reference), "isnull"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.NumericValue), "isnull"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.DateTimeValue), "isnull"));

            entity.ID = 1;
            entity.Reference = 3;
            entity.SecondReference = 4;
            entity.NumericValue = 10000;
            entity.DateTimeValue = new DateTime(1000, 1, 1);
            entity.NullableNumericValue = 100000;
            entity.NullableDateTimeValue = new DateTime(1000, 1, 1);
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(hasConnection ? 6 : 4, result.Failures.Count);
            Assert.IsTrue(!hasConnection || result.Failures.Contains(nameof(IEntityModel.Reference), "mustexist"));
            Assert.IsTrue(!hasConnection || result.Failures.Contains(nameof(IEntityModel.SecondReference), "mustexist"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.NumericValue), "outofrange"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.DateTimeValue), "outofrange"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.NullableNumericValue), "outofrange"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.NullableDateTimeValue), "outofrange"));

            result = entityValidator.ValidateAsync(entity).Result;
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(hasConnection ? 6 : 4, result.Failures.Count);
            Assert.IsTrue(!hasConnection || result.Failures.Contains(nameof(IEntityModel.Reference), "mustexist"));
            Assert.IsTrue(!hasConnection || result.Failures.Contains(nameof(IEntityModel.SecondReference), "mustexist"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.NumericValue), "outofrange"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.DateTimeValue), "outofrange"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.NullableNumericValue), "outofrange"));
            Assert.IsTrue(result.Failures.Contains(nameof(IEntityModel.NullableDateTimeValue), "outofrange"));

            CancellationTokenSource tokenSource = new CancellationTokenSource();
            result = entityValidator.ValidateAsync(entity, tokenSource.Token).Result;
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(hasConnection ? 6 : 4, result.Failures.Count);

            tokenSource.Cancel();
            Assert.Throws<OperationCanceledException>(() => entityValidator.ValidateAsync(entity, tokenSource.Token).ConfigureAwait(true).GetAwaiter().GetResult());

            if (hasConnection)
            {
                dictionary.Name = "Record1";
                result = dictionaryValidator.Validate(dictionary);
                Assert.IsFalse(result.IsValid);
                Assert.AreEqual(1, result.Failures.Count);
                Assert.IsTrue(result.Failures.Contains(nameof(IDictionaryModel.Name), "notunique"));

                dictionary.ID = 1;
                result = dictionaryValidator.Validate(dictionary);
                Assert.IsTrue(result.IsValid);

                dictionary.Name = "Record3";
                result = dictionaryValidator.Validate(dictionary);
                Assert.IsTrue(result.IsValid);
            }
        }

        private SqlDbConnection InitializeConnection()
        {
            SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();

            using (var query = connection.GetCreateEntityQuery<Dictionary>())
                query.Execute();

            using (var query = connection.GetCreateEntityQuery<Entity>())
                query.Execute();

            using (var query = connection.GetInsertEntityQuery<Dictionary>())
            {
                Dictionary dictionary = new Dictionary() { Name = "Record1" };
                query.Execute(dictionary);
                dictionary = new Dictionary() { Name = "Record2" };
                query.Execute(dictionary);
            }

            return connection;
        }

        [Test]
        public void TestAttributeValidationNoDB()
        {
            var dictionaryValidator = new DictionaryValidatorByAttributes();
            var entityValidator = new EntityValidatorByAttributes();
            Test<DictionaryModel2, EntityModel2>(false, dictionaryValidator, entityValidator);
        }

        [Test]
        public void TestAutoValidationNoDB()
        {
            var dictionaryValidator = new DictionaryValidatorAutoCreate();
            var entityValidator = new EntityValidatorAutoCreate();
            Test<DictionaryModel1, EntityModel1>(false, dictionaryValidator, entityValidator);
        }

        [Test]
        public void TestRuleValidationNoDB()
        {
            var dictionaryValidator = new DictionaryValidatorByRule();
            var entityValidator = new EntityValidatorByRule();
            Test<DictionaryModel1, EntityModel1>(false, dictionaryValidator, entityValidator);
        }

        [Test]
        public void TestAttributeValidationDB()
        {
            using (SqlDbConnection connection = InitializeConnection())
            {
                var dictionaryValidator = new DictionaryValidatorByAttributes(new ValidatorSingletonConnectionFactory(connection));
                var entityValidator = new EntityValidatorByAttributes(new ValidatorSingletonConnectionFactory(connection));
                Test<DictionaryModel2, EntityModel2>(true, dictionaryValidator, entityValidator);
            }
        }

        [Test]
        public void TestAutoValidationDB()
        {
            using (SqlDbConnection connection = InitializeConnection())
            {
                var dictionaryValidator = new DictionaryValidatorAutoCreate(new ValidatorSingletonConnectionFactory(connection));
                var entityValidator = new EntityValidatorAutoCreate(new ValidatorSingletonConnectionFactory(connection));
                Test<DictionaryModel1, EntityModel1>(true, dictionaryValidator, entityValidator);
            }
        }

        [Test]
        public void TestRuleValidationDB()
        {
            using (SqlDbConnection connection = InitializeConnection())
            {
                var dictionaryValidator = new DictionaryValidatorByRule(new ValidatorSingletonConnectionFactory(connection));
                var entityValidator = new EntityValidatorByRule(new ValidatorSingletonConnectionFactory(connection));
                Test<DictionaryModel1, EntityModel1>(true, dictionaryValidator, entityValidator);
            }
        }
    }
}

