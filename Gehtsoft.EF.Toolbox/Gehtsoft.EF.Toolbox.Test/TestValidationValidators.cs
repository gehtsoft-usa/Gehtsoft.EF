using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.Validator;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    [TestFixture()]
    public class TestValidationValidators
    {
        public enum TestEnum1
        {
            E1 = 1,
            E2 = 2,
            E3 = 3,
        }

        public class TestEntity1
        {
            public bool Valid { get; set; }
            public int IntValue { get; set; }
            public string StringValue { get; set; }
            public DateTime? DateTimeValue { get; set; }
            public string[] ArrayValue { get; set; }
            public List<int> ListValue { get; set; }
            public TestEnum1? EnumValue { get; set; }
        }

        public class TestEntity1Validator : AbstractValidator<TestEntity1>
        {
            public TestEntity1Validator()
            {
                Unless(a => a == null);
                RuleForEntity("whole").Must(a => a.Valid).WithMessage("message1").WithCode(1000);

                RuleFor<int>(nameof(TestEntity1.IntValue)).EnumIsCorrect<TestEnum1>().WithMessage("message21").WithCode(2001);
                RuleFor(e => e.IntValue).Between(1, true, 3, false).Must(x => x >= 0 && x <= 5).WithMessage("message22").WithCode(2002);
                RuleFor(e => e.IntValue).Between(1, true, 3, false).Between(1, 3).WithMessage("message23").WithCode(2003);

                RuleFor(e => e.StringValue).NotNullOrWhitespace().WithMessage("message31").WithCode(3001);
                RuleFor(e => e.StringValue).ShorterThan(5).WithMessage("message32").WithCode(3002);

                RuleFor(e => e.DateTimeValue).Between(new DateTime(2000, 1, 1), true, new DateTime(2005, 1, 1), false).WhenValue(v => v != null).WithMessage("message41").WithCode(4001);

                RuleFor(e => e.ArrayValue).ShorterThan(10).WithCode(5001).WithMessage("message51");
                RuleForAll<string>(nameof(TestEntity1.ArrayValue)).NotNullOrWhitespace().WithCode(5002).WithMessage("message52");

                RuleFor(e => e.ListValue).ShorterThan(5).WithCode(6001).WithMessage("message61");
                RuleForAll(e => e.ListValue).Between(10, 20).WithCode(6002).WithMessage("message62");

                RuleFor(e => e.EnumValue).WhenValue(v => v != null).EnumIsCorrect().WithCode(7001).WithMessage("message71");
            }
        }

        [Test]
        public void TestRules()
        {
            TestEntity1Validator validator = new TestEntity1Validator();
            ValidationResult result = validator.Validate(null);
            Assert.IsTrue(result.IsValid);

            TestEntity1 entity1 = new TestEntity1();
            result = validator.Validate(entity1);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Failures.Contains("whole", 1000));
            entity1.Valid = true;
            result = validator.Validate(entity1);
            Assert.IsFalse(result.IsValid);
            Assert.IsFalse(result.Failures.Contains("whole", 1000));

            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.IntValue), 2001));
            Assert.AreEqual("message21", result.Failures.Find(nameof(TestEntity1.IntValue), 2001).Message);
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.IntValue), 2002));
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.IntValue), 2003));
            Assert.AreEqual("message23", result.Failures.Find(nameof(TestEntity1.IntValue), 2003).Message);

            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.StringValue), 3001));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.StringValue), 3002));

            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.DateTimeValue), 4001));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ArrayValue), 5001));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ArrayValue), 5002));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ListValue), 6001));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ListValue), 6002));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.EnumValue), 7001));

            entity1.IntValue = (int)TestEnum1.E2;
            entity1.StringValue = new string('x', 50);
            entity1.EnumValue = TestEnum1.E1;
            entity1.ArrayValue = new string[] { "abc", "", null, "   " };
            entity1.ListValue = new List<int>(new int[] { 1, 11, 12, 13, 14, 15, 2 });
            entity1.DateTimeValue = new DateTime(2004, 12, 31, 23, 59, 59);
            result = validator.Validate(entity1);
            Assert.IsFalse(result.IsValid);

            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.IntValue), 2001));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.IntValue), 2002));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.IntValue), 2003));

            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.StringValue), 3001));
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.StringValue), 3002));

            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.DateTimeValue), 4001));

            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ArrayValue), 5001));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ArrayValue), 5002));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ArrayValue) + "[0]", 5002));
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.ArrayValue) + "[1]", 5002));
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.ArrayValue) + "[2]", 5002));
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.ArrayValue) + "[3]", 5002));

            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.ListValue), 6001));
            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.ListValue), 6002));
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.ListValue) + "[0]", 6002));
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.ListValue) + "[6]", 6002));

            Assert.IsFalse(result.Failures.Contains(nameof(TestEntity1.EnumValue), 7001));

            entity1.EnumValue = (TestEnum1)50;
            result = validator.Validate(entity1);
            Assert.IsTrue(result.Failures.Contains(nameof(TestEntity1.EnumValue), 7001));

            entity1.IntValue = (int)TestEnum1.E2;
            entity1.StringValue = new string('x', 4);
            entity1.EnumValue = TestEnum1.E1;
            entity1.ArrayValue = new string[] { "abc" };
            entity1.ListValue = new List<int>(new int[] { 11, 12 });
            entity1.DateTimeValue = null;
            result = validator.Validate(entity1);
            Assert.IsTrue(result.IsValid);
        }

        public class TestEntity2
        {
            public int ID { get; set; }
            public TestEntity2 Self { get; set; }
        }

        public class TestEntity3
        {
            public int ID { get; set; }
            public TestEntity2 Reference { get; set; }
            public TestEntity2[] MultiReference { get; set; }
        }

        public class TestEntity2Validator : AbstractValidator<TestEntity2>
        {
            public TestEntity2Validator()
            {
                Unless(x => x == null);
                RuleFor(x => x.ID).Must(v => v > 0).WithCode(1);
                RuleFor(x => x.Self).UnlessValue(v => v == null).ValidateUsing<TestEntity2Validator>();
            }
        }

        public class TestEntity3Validator : AbstractValidator<TestEntity3>
        {
            public TestEntity3Validator()
            {
                Unless(x => x == null);
                RuleFor(x => x.ID).Must(v => v > 0).WithCode(2);
                RuleFor(x => x.Reference).ValidateUsing<TestEntity2Validator>().UnlessValue(v => v == null);
                RuleFor(x => x.MultiReference).NotNull().WithCode(3);
                RuleForAll(x => x.MultiReference).NotNull().WithCode(4);
                RuleForAll(x => x.MultiReference).ValidateUsing<TestEntity2Validator>().UnlessValue(v => v == null).WithCode(5);
            }
        }

        [Test]
        public void TestValidateUsing()
        {
            TestEntity2 e2 = new TestEntity2() { ID = 0, Self = new TestEntity2() { ID = 0 } };
            TestEntity2Validator validator2 = new TestEntity2Validator();
            ValidationResult result = validator2.Validate(e2);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Failures.Contains("ID", 1));
            Assert.IsTrue(result.Failures.Contains("Self.ID", 1));

            e2.ID = 5;
            result = validator2.Validate(e2);
            Assert.IsFalse(result.IsValid);
            Assert.IsFalse(result.Failures.Contains("ID", 1));
            Assert.IsTrue(result.Failures.Contains("Self.ID", 1));

            TestEntity3 e3 = new TestEntity3()
            {
                ID = 0,
                Reference = e2,
            };
            TestEntity3Validator validator3 = new TestEntity3Validator();
            result = validator3.Validate(e3);
            Assert.IsTrue(result.Failures.Contains("ID", 2));
            Assert.IsTrue(result.Failures.Contains("Reference.Self.ID", 1));
            Assert.IsTrue(result.Failures.Contains("MultiReference", 3));

            e3.ID = 5;
            e3.MultiReference = new TestEntity2[] { null, e2, new TestEntity2() { ID = 0 }, new TestEntity2() { ID = 1 } };
            result = validator3.Validate(e3);
            Assert.IsFalse(result.Failures.Contains("ID", 2));
            Assert.IsTrue(result.Failures.Contains("Reference.Self.ID", 1));
            Assert.IsFalse(result.Failures.Contains("MultiReference", 3));
            Assert.IsTrue(result.Failures.Contains("MultiReference[0]", 4));
            Assert.IsTrue(result.Failures.Contains("MultiReference[1].Self.ID", 1));
            Assert.IsTrue(result.Failures.Contains("MultiReference[2].ID", 1));
            Assert.IsFalse(result.Failures.Contains("MultiReference[3].ID", 1));
            Assert.IsFalse(result.Failures.Contains("MultiReference[3]", 3));
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class Test4NameAttribute : Attribute
        {
            public string DisplayName { get; set; }
        }

        public static class TestEntity4Messages
        {
            public const string NOTNULL = "notnull";
            public const string NOTEMPTY = "notempty";
            public const string OUTOFRANGE = "outofrange";
            public const string TOOLONG = "toolong";
            public const string ELEMENTTOOLONG = "elementtoolong";
            public const string NOTZERO = "notzero";
        }

        public class TestEntity4MessageResolver : IValidationMessageResolver
        {
            private static readonly Dictionary<string, string> gMessages = new Dictionary<string, string>()
            {
                {TestEntity4Messages.NOTNULL, "{0} must be not null"},
                {TestEntity4Messages.NOTEMPTY, "{0} must be not empty"},
                {TestEntity4Messages.TOOLONG, "{0} is too long"},
                {TestEntity4Messages.ELEMENTTOOLONG, "An element of {0} is too long"},
                {TestEntity4Messages.OUTOFRANGE, "{0} is out of range"},
            };

            public string Resolve(Type entityType, ValidationTarget target, int code, string message)
            {
                string name = target.GetCustomAttribute<Test4NameAttribute>()?.DisplayName ?? target.TargetName;
                if (!gMessages.TryGetValue(message, out string translatedMessage))
                    translatedMessage = message;
                if (translatedMessage.Contains("{0}"))
                    translatedMessage = string.Format(translatedMessage, name);
                return translatedMessage;
            }
        }

        public class TestEntity4
        {
            [Test4Name(DisplayName = "String Value")]
            [MustBeNotNullOrWhitespace(WithMessage = TestEntity4Messages.NOTEMPTY)]
            [MustBeShorterThan(10, WithMessage = TestEntity4Messages.TOOLONG)]
            public string StringValue { get; set; }

            [Test4Name(DisplayName = "Array of String")]
            [MustBeNotNull(WithMessage = TestEntity4Messages.NOTNULL)]
            [MustBeShorterThan(5, WithMessage = TestEntity4Messages.TOOLONG)]
            [MustMatch("a.+", ForElement = true, WithMessage = "An element of {0} value does not match to the pattern")]
            public string[] StringArray { get; set; }

            [MustBeInRange(Mininum = 10, Maximum = 20, WithMessage = TestEntity4Messages.OUTOFRANGE)]
            public int IntegerValue { get; set; }
        }

        public class Test4Validator : AbstractValidator<TestEntity4>
        {
            public Test4Validator()
            {
                RuleForAll(e => e.StringArray).ShorterThan(12).WithMessage(TestEntity4Messages.ELEMENTTOOLONG);
                RuleFor(e => e.IntegerValue != 0, "customRule").Must(v => v).WithMessage(TestEntity4Messages.NOTZERO);
            }
        }

        [Test]
        public void TestAttributeBaseValidationAndMessageResolver()
        {
            ValidationMessageResolverFactory.SetResolverFor<TestEntity4>(new TestEntity4MessageResolver());
            Test4Validator validator = new Test4Validator();

            TestEntity4 entity = new TestEntity4
            {
                StringValue = new string(' ', 50)
            };
            ValidationResult result = validator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(5, result.Failures.Count);
            Assert.AreEqual("String Value must be not empty", result.Failures[0].Message);
            Assert.AreEqual("String Value is too long", result.Failures[1].Message);
            Assert.AreEqual("Array of String must be not null", result.Failures[2].Message);
            Assert.AreEqual("IntegerValue is out of range", result.Failures[3].Message);
            Assert.AreEqual("notzero", result.Failures[4].Message);

            entity.StringValue = "123";
            entity.IntegerValue = 15;

            entity.StringArray = new[]
            {
                "abc", "abd", "efg", new string('a', 20), "aaa", "eee"
            };
            result = validator.Validate(entity);
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(4, result.Failures.Count);

            Assert.AreEqual("An element of Array of String value does not match to the pattern", result.Failures[0].Message);
            Assert.AreEqual("An element of Array of String value does not match to the pattern", result.Failures[1].Message);
            Assert.AreEqual("Array of String is too long", result.Failures[2].Message);
            Assert.AreEqual("An element of Array of String is too long", result.Failures[3].Message);
        }

        public class SubValidator : BaseValidator
        {
            public int mMagicNumber;

            public SubValidator() : base(typeof(TestEntity2))
            {
                mMagicNumber = 0;
            }

            public SubValidator(int magicNumber) : base(typeof(TestEntity2))
            {
                mMagicNumber = magicNumber;
            }

            protected override Task<ValidationResult> ValidateCore(bool sync, object entity, CancellationToken? token)
            {
                ValidationResult rs = new ValidationResult();
                if (mMagicNumber != 0x1234)
                    rs.Failures.Add(new ValidationFailure("all", "noconstructor"));
                return Task.FromResult(rs);
            }
        }

        [Test]
        public void TestValidateUsingOptions()
        {
            TestEntity3 entity = new TestEntity3() { Reference = new TestEntity2() };
            AbstractValidator<TestEntity3> validator;
            ValidationResult rs;

            validator = new AbstractValidator<TestEntity3>();
            validator.RuleFor(e => e.Reference).ValidateUsing(new SubValidator());

            rs = validator.Validate(entity);
            Assert.IsFalse(rs.IsValid);
            Assert.IsTrue(rs.Failures.Contains("Reference.all", "noconstructor"));

            validator = new AbstractValidator<TestEntity3>();
            validator.RuleFor(e => e.Reference).ValidateUsing(new SubValidator(0x1234));

            rs = validator.Validate(entity);
            Assert.IsTrue(rs.IsValid);

            validator = new AbstractValidator<TestEntity3>();
            validator.RuleFor(e => e.Reference).ValidateUsing<SubValidator>();

            rs = validator.Validate(entity);
            Assert.IsFalse(rs.IsValid);
            Assert.IsTrue(rs.Failures.Contains("Reference.all", "noconstructor"));

            validator = new AbstractValidator<TestEntity3>();
            validator.RuleFor(e => e.Reference).ValidateUsing<SubValidator>(new object[] { 0x1234 });

            rs = validator.Validate(entity);
            Assert.IsTrue(rs.IsValid);
        }
    }
}

