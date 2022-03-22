using System;
using System.Data;
using System.IO;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    public static class SqliteInitializerNet4
    {
        private static bool gInitialized = false;

        public static void Initialize()
        {
            if (!gInitialized)
            {
#if NET4
                string basePath = new FileInfo(Path.GetFullPath(typeof(SqliteInitializerNet4).Assembly.Location)).DirectoryName;
                Assert.IsTrue(Environment.OSVersion.Platform == PlatformID.Win32NT, "Must be run on windows only");
                string path = Path.Combine(basePath, "runtimes", Environment.Is64BitProcess ? "win-x64" : "win-x86", "native", "e_sqlite3.dll");
                Assert.IsTrue(File.Exists(path), "Runtime should exist");
                File.Copy(path, Path.Combine(basePath, "e_sqlite3.dll"), true);
#endif

                gInitialized = true;
            }
        }
    }

    [TestFixture()]
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

        [OneTimeSetUp]
        public void Setup()
        {
            SqliteInitializerNet4.Initialize();
            Assert.IsNotNull(AllEntities.Inst[typeof(ValidatorTestEntityDict), false]);
            Assert.IsNotNull(AllEntities.Inst[typeof(ValidatorTestEntity), false]);
        }

        [Test]
        public void TestBareValidator()
        {
            EfEntityValidator<ValidatorTestEntity> entityValidator = new EfEntityValidator<ValidatorTestEntity>(new DummySqlLanguageSpecifics());

            ValidatorTestEntity entity = new ValidatorTestEntity();
            ValidationResult result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(2, result.Failures.Count);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.StringValue), (int)EfValidationErrorCode.NullValue));
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.TsValue), (int)EfValidationErrorCode.TimestampIsOutOfRange));

            entity.StringValue = "123";
            entity.DateValue = DateTime.Now;
            entity.TsValue = DateTime.Now;
            entity.DoubleValue = 123.45;
            entity.DecimalValue = 123.45m;
            entity.EnumValue = ValidatorTestValues.EnumValue1;
            entity.IntValue = 0;
            entity.Reference = null;

            result = entityValidator.Validate(entity);
            Assert.IsTrue(result.IsValid);

            entity.Reference = new ValidatorTestEntityDict() { ID = 1 };
            result = entityValidator.Validate(entity);
            Assert.IsTrue(result.IsValid);

            entity.StringValue = new string('0', 257);
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Failures.Count);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.StringValue), (int)EfValidationErrorCode.StringIsTooLong));
            entity.StringValue = "";

            entity.DateValue = new DateTime(2050, 1, 1);
            entity.TsValue = new DateTime(2050, 1, 1);
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(1, result.Failures.Count);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.TsValue), (int)EfValidationErrorCode.TimestampIsOutOfRange));

            entity.DateValue = new DateTime(9999, 1, 1);
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(2, result.Failures.Count);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.DateValue), (int)EfValidationErrorCode.DateIsOutRange));
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.TsValue), (int)EfValidationErrorCode.TimestampIsOutOfRange));

            entity.DateValue = DateTime.Now;
            entity.TsValue = DateTime.Now;

            entity.DoubleValue = 9999.99;
            result = entityValidator.Validate(entity);
            Assert.IsTrue(result.IsValid);
            entity.DoubleValue = -9999.99;
            result = entityValidator.Validate(entity);
            Assert.IsTrue(result.IsValid);
            entity.DoubleValue = 10000;
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.DoubleValue), (int)EfValidationErrorCode.NumberIsOutOfRange));
            entity.DoubleValue = -10000;
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.DoubleValue), (int)EfValidationErrorCode.NumberIsOutOfRange));
            entity.DoubleValue = 0;

            entity.DecimalValue = 9999.99m;
            result = entityValidator.Validate(entity);
            Assert.IsTrue(result.IsValid);
            entity.DecimalValue = -9999.99m;
            result = entityValidator.Validate(entity);
            Assert.IsTrue(result.IsValid);
            entity.DecimalValue = 10000;
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.DecimalValue), (int)EfValidationErrorCode.NumberIsOutOfRange));
            entity.DecimalValue = -10000;
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.DecimalValue), (int)EfValidationErrorCode.NumberIsOutOfRange));
            entity.DecimalValue = 0;

            entity.EnumValue = ValidatorTestValues.EnumValue1;
            result = entityValidator.Validate(entity);
            Assert.IsTrue(result.IsValid);

            entity.EnumValue = (ValidatorTestValues)123;
            result = entityValidator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Failures.Contains(nameof(ValidatorTestEntity.EnumValue), (int)EfValidationErrorCode.EnumerationValueIsInvalid));
        }

        [Test]
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
                Assert.IsFalse(res.IsValid);
                Assert.IsTrue(res.Failures.Contains(nameof(ValidatorTestEntityDict.StringValue), (int)EfValidationErrorCode.ValueIsNotUnique));

                dictEntry.ID = 2;
                dictEntry.StringValue = "entity2";
                res = dictValidator.Validate(dictEntry);
                Assert.IsTrue(res.IsValid);

                dictEntry.ID = 0;
                dictEntry.StringValue = "entity3";
                res = dictValidator.Validate(dictEntry);
                Assert.IsTrue(res.IsValid);

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
                Assert.IsTrue(res.IsValid);

                entity.Reference = new ValidatorTestEntityDict() { ID = 2 };
                res = entityValidator.Validate(entity);
                Assert.IsTrue(res.IsValid);

                entity.Reference = new ValidatorTestEntityDict() { ID = 3 };
                res = entityValidator.Validate(entity);
                Assert.IsFalse(res.IsValid);
                Assert.IsTrue(res.Failures.Contains(nameof(ValidatorTestEntity.Reference), (int)EfValidationErrorCode.ReferenceDoesNotExists));
            }
        }

        public class MessageProvider : IEfValidatorMessageProvider
        {
            public string GetMessage(EntityDescriptor entityDescriptor, TableDescriptor.ColumnInfo column, int validationErrorCode)
            {
                return $"{entityDescriptor.EntityType.Name}.{column.PropertyAccessor.Name} - error {validationErrorCode}";
            }
        }

        [Test]
        public void TestMessageProvider()
        {
            EfEntityValidator<ValidatorTestEntityDict> validator = new EfEntityValidator<ValidatorTestEntityDict>(null, null, new MessageProvider());
            ValidatorTestEntityDict rec = new ValidatorTestEntityDict
            {
                StringValue = new string('a', 512)
            };
            ValidationResult res = validator.Validate(rec);
            Assert.IsFalse(res.IsValid);
            Assert.AreEqual(1, res.Failures.Count);
            Assert.AreEqual($"ValidatorTestEntityDict.StringValue - error {(int)EfValidationErrorCode.StringIsTooLong}", res.Failures[0].Message);
        }
    }
}
