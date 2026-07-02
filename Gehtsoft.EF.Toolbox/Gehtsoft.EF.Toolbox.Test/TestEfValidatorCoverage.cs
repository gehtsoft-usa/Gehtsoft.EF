using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
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
    /// <summary>
    /// Targeted coverage for <c>Gehtsoft.EF.Validator</c> paths the scenario suites
    /// (<see cref="TestEntityValidators"/> / <see cref="TestModelValidator"/>) do not reach:
    /// the stand-alone <see cref="EfPredicateFactory.GetPredicates"/> helper, the
    /// <see cref="ValidatorConnectionFactory"/> delegate wrapper, the value-conversion and
    /// client-script branches of the number/decimal range predicates, the owned-connection
    /// dispose branch of <see cref="DatabasePredicate"/>, the default message provider fallbacks,
    /// and the (otherwise unused) <see cref="EntityPropertyTarget"/>.
    /// </summary>
    public class TestEfValidatorCoverage
    {
        public enum CovEnum
        {
            One,
            Two,
        }

        [Entity(Table = "EfValCovEntity")]
        public class EfValCovEntity
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Size = 10, Nullable = false)]
            public string Str { get; set; }

            [EntityProperty(DbType = DbType.Double, Size = 6, Precision = 2)]
            public double Dbl { get; set; }

            [EntityProperty(DbType = DbType.Decimal, Size = 6, Precision = 2)]
            public decimal Dec { get; set; }

            [EntityProperty(DbType = DbType.Date, Nullable = true)]
            public DateTime? Date { get; set; }

            [EntityProperty(DbType = DbType.DateTime)]
            public DateTime Ts { get; set; }

            [EntityProperty(DbType = DbType.Int32)]
            public CovEnum EnumNonNull { get; set; }

            [EntityProperty(DbType = DbType.Int32, Nullable = true)]
            public CovEnum? EnumNull { get; set; }
        }

        private static TableDescriptor.ColumnInfo Column(Type entityType, string propertyName)
        {
            EntityDescriptor descriptor = AllEntities.Inst[entityType];
            foreach (TableDescriptor.ColumnInfo column in descriptor.TableDescriptor)
                if (column.PropertyAccessor.Name == propertyName)
                    return column;
            throw new InvalidOperationException("column not found: " + propertyName);
        }

        // ----------------------------------------------- EfPredicateFactory.GetPredicates

        [Fact]
        public void GetPredicates_Builds_Predicates_For_Every_Column_Kind()
        {
            var specifics = new TestEntityValidators.DummySqlLanguageSpecifics();
            EntityDescriptor descriptor = AllEntities.Inst[typeof(EfValCovEntity)];

            var producedTypes = new HashSet<Type>();
            foreach (TableDescriptor.ColumnInfo column in descriptor.TableDescriptor)
                foreach (IValidationPredicate predicate in EfPredicateFactory.GetPredicates(column, specifics))
                    producedTypes.Add(predicate.GetType());

            producedTypes.Should().Contain(typeof(IsNotNullOrEmptyPredicate));  // non-nullable columns
            producedTypes.Should().Contain(typeof(IsShorterThanPredicate));     // Str (String, Size 10)
            producedTypes.Should().Contain(typeof(NumberPropertyRangePredicate)); // Dbl (Double)
            producedTypes.Should().Contain(typeof(DecimalPropertyRangePredicate)); // Dec (Decimal)
            producedTypes.Should().Contain(typeof(ValueIsBetweenPredicate));    // Date + Ts (with specifics)
            producedTypes.Should().Contain(typeof(IsEnumValueCorrectPredicate)); // EnumNonNull + EnumNull
        }

        [Fact]
        public void AddDbValidation_Enforces_NonNullable_Enum_Column()
        {
            // exercises the non-nullable-enum branch of AddDbValidation (the scenario suites only
            // use nullable enum columns, which take the else-if branch).
            var specifics = new TestEntityValidators.DummySqlLanguageSpecifics();
            var validator = new EfEntityValidator<EfValCovEntity>(specifics);

            var good = new EfValCovEntity { Str = "ok", Ts = DateTime.Now, EnumNonNull = CovEnum.One };
            validator.Validate(good).IsValid.Should().BeTrue();

            var bad = new EfValCovEntity { Str = "ok", Ts = DateTime.Now, EnumNonNull = (CovEnum)999 };
            ValidationResult result = validator.Validate(bad);
            result.IsValid.Should().BeFalse();
            result.Failures.Contains(nameof(EfValCovEntity.EnumNonNull), (int)EfValidationErrorCode.EnumerationValueIsInvalid)
                .Should().BeTrue();
        }

        // --------------------------------------------------- ValidatorConnectionFactory

        [Fact]
        public void ValidatorConnectionFactory_Wraps_Sync_And_Async_Delegates()
        {
            SqlDbConnectionFactory sync = _ => SqliteDbConnectionFactory.CreateMemory();
            SqlDbConnectionFactoryAsync asyncFactory = (_, __) => Task.FromResult(SqliteDbConnectionFactory.CreateMemory());

            var factory = new ValidatorConnectionFactory(sync, asyncFactory, "ignored");
            factory.NeedToDispose.Should().BeTrue();

            using (SqlDbConnection c = factory.GetConnection())
                c.Should().NotBeNull();
        }

        [Fact]
        public async Task ValidatorConnectionFactory_Async_Overloads_Return_Connection()
        {
            SqlDbConnectionFactory sync = _ => SqliteDbConnectionFactory.CreateMemory();
            SqlDbConnectionFactoryAsync asyncFactory = (_, __) => Task.FromResult(SqliteDbConnectionFactory.CreateMemory());

            var factory = new ValidatorConnectionFactory(sync, asyncFactory, "ignored");

            using (SqlDbConnection c1 = await factory.GetConnectionAsync())
                c1.Should().NotBeNull();
            using (SqlDbConnection c2 = await factory.GetConnectionAsync(CancellationToken.None))
                c2.Should().NotBeNull();
        }

        [Fact]
        public void ValidatorConnectionFactory_Sync_Only_Constructor()
        {
            // the two-argument constructor chains to the full one with a null async factory
            SqlDbConnectionFactory sync = _ => SqliteDbConnectionFactory.CreateMemory();
            var factory = new ValidatorConnectionFactory(sync, "ignored");

            using (SqlDbConnection c = factory.GetConnection())
                c.Should().NotBeNull();
        }

        // ---------------------------------------- Number / Decimal range predicate Validate

        [Fact]
        public void NumberPropertyRangePredicate_Validate_Handles_Null_InRange_OutOfRange_And_Conversion()
        {
            var predicate = new NumberPropertyRangePredicate(6, 2); // max = 10^(6-2) = 10000

            predicate.Validate(null).Should().BeTrue();       // null is accepted
            predicate.Validate(5000.0).Should().BeTrue();     // value is double
            predicate.Validate(-5000.0).Should().BeTrue();
            predicate.Validate(10000.0).Should().BeFalse();   // out of range
            predicate.Validate(5000).Should().BeTrue();       // int -> Convert.ChangeType(double)
            predicate.RemoteScript(null).Should().Contain("jsv_and");
        }

        [Fact]
        public void DecimalPropertyRangePredicate_Validate_Handles_Null_InRange_OutOfRange_And_Conversion()
        {
            var predicate = new DecimalPropertyRangePredicate(6, 2); // max = 10000

            predicate.Validate(null).Should().BeTrue();
            predicate.Validate(5000m).Should().BeTrue();      // value is decimal
            predicate.Validate(-5000m).Should().BeTrue();
            predicate.Validate(10000m).Should().BeFalse();
            predicate.Validate(5000).Should().BeTrue();       // int -> Convert.ChangeType(decimal)
            predicate.RemoteScript(null).Should().Contain("jsv_and");
        }

        // --------------------------------------- DatabasePredicate owned-connection dispose

        private sealed class OwnedConnectionFactory : IValidatorConnectionFactory
        {
            private readonly SqlDbConnection mConnection;
            public OwnedConnectionFactory(SqlDbConnection connection) => mConnection = connection;
            public bool NeedToDispose => true;
            public SqlDbConnection GetConnection() => mConnection;
            public Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null) => Task.FromResult(mConnection);
        }

        [Fact]
        public void DatabasePredicate_Disposes_Owned_Connection_And_RemoteScript_Is_Null()
        {
            SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (EntityQuery query = connection.GetCreateEntityQuery<TestEntityValidators.ValidatorTestEntityDict>())
                query.Execute();
            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<TestEntityValidators.ValidatorTestEntityDict>())
                query.Execute(new TestEntityValidators.ValidatorTestEntityDict { StringValue = "dup" });

            TableDescriptor.ColumnInfo column = Column(typeof(TestEntityValidators.ValidatorTestEntityDict), "StringValue");
            var predicate = new IsUniquePredicate(new OwnedConnectionFactory(connection),
                typeof(TestEntityValidators.ValidatorTestEntityDict), column);

            // server-side predicate: no client script, server execution side
            predicate.RemoteScript(null).Should().BeNull();
            predicate.Side.Should().Be(RuleExecutionSide.Server);

            // duplicate value -> not unique; the factory owns the connection, so Validate disposes it
            bool valid = predicate.Validate(new TestEntityValidators.ValidatorTestEntityDict { ID = 0, StringValue = "dup" });
            valid.Should().BeFalse();
        }

        // ------------------------------------ DefaultEfValidatorMessageProvider fallbacks

        [Fact]
        public void DefaultMessageProvider_Falls_Back_To_English_And_To_Unknown_Code()
        {
            var provider = new DefaultEfValidatorMessageProvider("xx"); // unknown language -> English

            provider.GetMessage(null, null, (int)EfValidationErrorCode.NullValue)
                .Should().Be("The value must not be empty");
            provider.GetMessage(null, null, 987654) // unknown code -> the -1 fallback
                .Should().Be("Unknown Error");
        }

        // ------------------------------------------------------- EntityPropertyTarget

        [Fact]
        public void EntityPropertyTarget_Exposes_Property_Value_And_Attribute()
        {
            TableDescriptor.ColumnInfo column = Column(typeof(EfValCovEntity), nameof(EfValCovEntity.Str));
            var target = new EntityPropertyTarget(column.PropertyAccessor);

            target.TargetName.Should().Be(nameof(EfValCovEntity.Str));
            target.PropertyName.Should().Be(nameof(EfValCovEntity.Str));
            target.IsProperty.Should().BeTrue();
            target.IsSingleValue.Should().BeTrue();
            target.ValueType.Should().Be(typeof(string));
            target.GetCustomAttribute<EntityPropertyAttribute>().Should().NotBeNull();

            var entity = new EfValCovEntity { Str = "hello" };
            ValidationTarget.ValidationValue value = target.First(entity);
            value.Name.Should().Be(nameof(EfValCovEntity.Str));
            value.Value.Should().Be("hello");

            ValidationTarget.ValidationValue[] all = target.All(entity);
            all.Should().HaveCount(1);
            all[0].Value.Should().Be("hello");
        }
    }
}
