using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    public class TestEntityValidators
    {
        public enum ValidatorTestValues
        {
            EnumValue1,
            EnumValue2,
            EnumValue3,
        }

        [Entity(Table = "ValidatorTestTableDict")]
        public class ValidatorTestEntityDict
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Size = 256, Unique = true)]
            public string StringValue { get; set; }
        }

        [Entity(Table = "ValidatorTestTable")]
        public class ValidatorTestEntity
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Size = 256, Unique = true, Nullable = false)]
            public string StringValue { get; set; }

            [EntityProperty(DbType = DbType.Date, Nullable = true)]
            public DateTime? DateValue { get; set; }

            [EntityProperty(DbType = DbType.DateTime)]
            public DateTime TsValue { get; set; }

            [EntityProperty(DbType = DbType.Double, Size = 6, Precision = 2)]
            public double DoubleValue { get; set; }

            [EntityProperty(DbType = DbType.Decimal, Size = 6, Precision = 2)]
            public decimal DecimalValue { get; set; }

            [EntityProperty(DbType = DbType.Int32)]
            public int IntValue { get; set; }

            [EntityProperty(DbType = DbType.Int32, Nullable = true)]
            public ValidatorTestValues? EnumValue { get; set; }

            [EntityProperty(ForeignKey = true, Nullable = true)]
            public ValidatorTestEntityDict Reference { get; set; }
        }

        public class DummySqlLanguageSpecifics : SqlDbLanguageSpecifics
        {
            public override DateTime? MinDate => new DateTime(1761, 1, 1);
            public override DateTime? MaxDate => new DateTime(2167, 1, 1);
            public override DateTime? MinTimestamp => new DateTime(1970, 1, 1);
            public override DateTime? MaxTimestamp => new DateTime(2038, 1, 1);

            public override string TypeName(DbType type, int size, int precision, bool autoincrement)
            {
                switch (type)
                {
                    case DbType.Int32:
                        return "INTEGER";
                    case DbType.Int64:
                        return "NUMERIC(19, 0)";
                    case DbType.Double:
                    case DbType.Decimal:
                        return $"NUMERIC({size}, {precision})";
                    case DbType.Boolean:
                        return "VARCHAR(1)";
                    case DbType.String:
                        return $"VARCHAR({size})";
                    case DbType.Binary:
                        if (size > 0)
                            return $"BLOB({size})";
                        else
                            return "BLOB";
                    case DbType.Date:
                        return "DATE";
                    case DbType.DateTime:
                        return "TIMESTAMP";
                    case DbType.Guid:
                        return "VARCHAR(40)";
                    default:
                        throw new ArgumentException($"Type {type} is not supported in SQL92", nameof(type));
                }
            }

            public override bool TypeToDb(Type type, out DbType dbtype)
            {
                type = Nullable.GetUnderlyingType(type) ?? type;

                if (type == typeof(bool) || type == typeof(Guid))
                {
                    dbtype = DbType.String;
                    return true;
                }

                return base.TypeToDb(type, out dbtype);
            }


        }

        public TestEntityValidators()
        {
            AllEntities.Inst[typeof(ValidatorTestEntityDict), false].Should().NotBeNull();
            AllEntities.Inst[typeof(ValidatorTestEntity), false].Should().NotBeNull();
        }

        [Fact]
        public void TestBareValidator()
        {
            EfEntityValidator<ValidatorTestEntity> entityValidator = new EfEntityValidator<ValidatorTestEntity>(new DummySqlLanguageSpecifics());

            ValidatorTestEntity entity = new ValidatorTestEntity();
            ValidationResult result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Count.Should().Be(2);
            result.Failures.Contains(nameof(ValidatorTestEntity.StringValue), (int)EfValidationErrorCode.NullValue).Should().BeTrue();
            result.Failures.Contains(nameof(ValidatorTestEntity.TsValue), (int)EfValidationErrorCode.TimestampIsOutOfRange).Should().BeTrue();

            entity.StringValue = "123";
            entity.DateValue = DateTime.Now;
            entity.TsValue = DateTime.Now;
            entity.DoubleValue = 123.45;
            entity.DecimalValue = 123.45m;
            entity.EnumValue = ValidatorTestValues.EnumValue1;
            entity.IntValue = 0;
            entity.Reference = null;

            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeTrue();

            entity.Reference = new ValidatorTestEntityDict() { ID = 1 };
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeTrue();

            entity.StringValue = new string('0', 257);
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Count.Should().Be(1);
            result.Failures.Contains(nameof(ValidatorTestEntity.StringValue), (int)EfValidationErrorCode.StringIsTooLong).Should().BeTrue();
            entity.StringValue = "";

            entity.DateValue = new DateTime(2050, 1, 1);
            entity.TsValue = new DateTime(2050, 1, 1);
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Count.Should().Be(1);
            result.Failures.Contains(nameof(ValidatorTestEntity.TsValue), (int)EfValidationErrorCode.TimestampIsOutOfRange).Should().BeTrue();

            entity.DateValue = new DateTime(9999, 1, 1);
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Count.Should().Be(2);
            result.Failures.Contains(nameof(ValidatorTestEntity.DateValue), (int)EfValidationErrorCode.DateIsOutRange).Should().BeTrue();
            result.Failures.Contains(nameof(ValidatorTestEntity.TsValue), (int)EfValidationErrorCode.TimestampIsOutOfRange).Should().BeTrue();

            entity.DateValue = DateTime.Now;
            entity.TsValue = DateTime.Now;

            entity.DoubleValue = 9999.99;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeTrue();
            entity.DoubleValue = -9999.99;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeTrue();
            entity.DoubleValue = 10000;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(ValidatorTestEntity.DoubleValue), (int)EfValidationErrorCode.NumberIsOutOfRange).Should().BeTrue();
            entity.DoubleValue = -10000;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(ValidatorTestEntity.DoubleValue), (int)EfValidationErrorCode.NumberIsOutOfRange).Should().BeTrue();
            entity.DoubleValue = 0;

            entity.DecimalValue = 9999.99m;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeTrue();
            entity.DecimalValue = -9999.99m;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeTrue();
            entity.DecimalValue = 10000;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(ValidatorTestEntity.DecimalValue), (int)EfValidationErrorCode.NumberIsOutOfRange).Should().BeTrue();
            entity.DecimalValue = -10000;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(ValidatorTestEntity.DecimalValue), (int)EfValidationErrorCode.NumberIsOutOfRange).Should().BeTrue();
            entity.DecimalValue = 0;

            entity.EnumValue = ValidatorTestValues.EnumValue1;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeTrue();

            entity.EnumValue = (ValidatorTestValues)123;
            result = entityValidator.Validate(entity);
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(ValidatorTestEntity.EnumValue), (int)EfValidationErrorCode.EnumerationValueIsInvalid).Should().BeTrue();
        }

        [Fact]
        public void TestDbValidator()
        {
            using (SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory())
            {
                EfEntityValidator<ValidatorTestEntityDict> dictValidator = new EfEntityValidator<ValidatorTestEntityDict>(connection.GetLanguageSpecifics(), new ValidatorSingletonConnectionFactory(connection));
                EfEntityValidator<ValidatorTestEntity> entityValidator = new EfEntityValidator<ValidatorTestEntity>(connection.GetLanguageSpecifics(), new ValidatorSingletonConnectionFactory(connection));

                using (EntityQuery query = connection.GetCreateEntityQuery<ValidatorTestEntityDict>())
                    query.Execute();

                using (EntityQuery query = connection.GetCreateEntityQuery<ValidatorTestEntity>())
                    query.Execute();

                ValidatorTestEntityDict dictEntry;
                ValidatorTestEntity entity;

                using (ModifyEntityQuery query = connection.GetInsertEntityQuery<ValidatorTestEntityDict>())
                {
                    dictEntry = new ValidatorTestEntityDict
                    {
                        StringValue = "entity1"
                    };
                    query.Execute(dictEntry);

                    dictEntry = new ValidatorTestEntityDict
                    {
                        StringValue = "entity2"
                    };
                    query.Execute(dictEntry);
                }

                dictEntry = new ValidatorTestEntityDict
                {
                    StringValue = "entity2"
                };
                ValidationResult res = dictValidator.Validate(dictEntry);
                res.IsValid.Should().BeFalse();
                res.Failures.Contains(nameof(ValidatorTestEntityDict.StringValue), (int)EfValidationErrorCode.ValueIsNotUnique).Should().BeTrue();

                dictEntry.ID = 2;
                dictEntry.StringValue = "entity2";
                res = dictValidator.Validate(dictEntry);
                res.IsValid.Should().BeTrue();

                dictEntry.ID = 0;
                dictEntry.StringValue = "entity3";
                res = dictValidator.Validate(dictEntry);
                res.IsValid.Should().BeTrue();

                entity = new ValidatorTestEntity
                {
                    StringValue = "123",
                    DateValue = DateTime.Now,
                    TsValue = DateTime.Now,
                    DoubleValue = 123.45,
                    DecimalValue = 123.45m,
                    EnumValue = ValidatorTestValues.EnumValue1,
                    IntValue = 0,
                    Reference = null
                };

                res = entityValidator.Validate(entity);
                res.IsValid.Should().BeTrue();

                entity.Reference = new ValidatorTestEntityDict() { ID = 2 };
                res = entityValidator.Validate(entity);
                res.IsValid.Should().BeTrue();

                entity.Reference = new ValidatorTestEntityDict() { ID = 3 };
                res = entityValidator.Validate(entity);
                res.IsValid.Should().BeFalse();
                res.Failures.Contains(nameof(ValidatorTestEntity.Reference), (int)EfValidationErrorCode.ReferenceDoesNotExists).Should().BeTrue();
            }
        }

        public class MessageProvider : IEfValidatorMessageProvider
        {
            public string GetMessage(EntityDescriptor entityDescriptor, TableDescriptor.ColumnInfo column, int validationErrorCode)
            {
                return $"{entityDescriptor.EntityType.Name}.{column.PropertyAccessor.Name} - error {validationErrorCode}";
            }
        }

        [Fact]
        public void TestMessageProvider()
        {
            EfEntityValidator<ValidatorTestEntityDict> validator = new EfEntityValidator<ValidatorTestEntityDict>(null, null, new MessageProvider());
            ValidatorTestEntityDict rec = new ValidatorTestEntityDict
            {
                StringValue = new string('a', 512)
            };
            ValidationResult res = validator.Validate(rec);
            res.IsValid.Should().BeFalse();
            res.Failures.Count.Should().Be(1);
            res.Failures[0].Message.Should().Be($"ValidatorTestEntityDict.StringValue - error {(int)EfValidationErrorCode.StringIsTooLong}");
        }
    }
}
