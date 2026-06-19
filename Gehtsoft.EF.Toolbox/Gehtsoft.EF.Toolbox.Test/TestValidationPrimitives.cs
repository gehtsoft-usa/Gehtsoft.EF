using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Gehtsoft.Validator;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    public class TestValidationPrimitives
    {
        public enum TestEnum
        {
            E1 = 1,
            E2 = 2,
            E3 = 3,
        }

        public class TestTarget1
        {
            public int ID { get; set; }
            public string[] Names { get; set; }
            public IEnumerable<string> ENames => Names;
        }

        public class TestTarget2
        {
            public int ID { get; set; }
            public TestTarget2 SelfReference { get; set; }
            public TestTarget1 Reference { get; set; }
            public TestTarget1[] MultiReference { get; set; }
        }

        [Fact]
        public void TestPredicates()
        {
            IValidationPredicate predicate;
            predicate = new IsNullPredicate(typeof(object));
            predicate.Validate(null).Should().BeTrue();
            predicate.Validate(5).Should().BeFalse();
            predicate.Validate(predicate).Should().BeFalse();

            predicate = new IsNotNullPredicate(typeof(object));
            predicate.Validate(null).Should().BeFalse();
            predicate.Validate(5).Should().BeTrue();
            predicate.Validate("").Should().BeTrue();
            predicate.Validate(predicate).Should().BeTrue();

            predicate = new IsNotNullOrEmptyPredicate(typeof(object));
            predicate.Validate(null).Should().BeFalse();
            predicate.Validate(5).Should().BeTrue();
            predicate.Validate("").Should().BeFalse();
            predicate.Validate("     ").Should().BeTrue();
            predicate.Validate(predicate).Should().BeTrue();
            predicate.Validate("1").Should().BeTrue();

            predicate.Validate(new int[] { 1 }).Should().BeTrue();
            predicate.Validate(new int[0]).Should().BeFalse();
            List<int> l = new List<int>();
            predicate.Validate(l).Should().BeFalse();
            l.Add(1);
            predicate.Validate(l).Should().BeTrue();

            predicate = new IsNotNullOrWhitespacePredicate(typeof(object));
            predicate.Validate(null).Should().BeFalse();
            predicate.Validate(5).Should().BeTrue();
            predicate.Validate("").Should().BeFalse();
            predicate.Validate("     ").Should().BeFalse();
            predicate.Validate(predicate).Should().BeTrue();
            predicate.Validate("1").Should().BeTrue();

            predicate = new ValueIsBetweenPredicate(typeof(int), 1.0, true, null, false);
            predicate.Validate(0.9999999).Should().BeFalse();
            predicate.Validate(1.0).Should().BeTrue();
            predicate.Validate(1.1).Should().BeTrue();

            predicate = new ValueIsBetweenPredicate(typeof(int), 1.0, false, null, false);
            predicate.Validate(0.9999999).Should().BeFalse();
            predicate.Validate(1.0).Should().BeFalse();
            predicate.Validate(1.1).Should().BeTrue();

            predicate = new ValueIsBetweenPredicate(typeof(int), null, false, 1.0, true);
            predicate.Validate(0.9999999).Should().BeTrue();
            predicate.Validate(1.0).Should().BeTrue();
            predicate.Validate(1.1).Should().BeFalse();

            predicate = new ValueIsBetweenPredicate(typeof(int), null, false, 1.0, false);
            predicate.Validate(0.9999999).Should().BeTrue();
            predicate.Validate(1.0).Should().BeFalse();
            predicate.Validate(1.1).Should().BeFalse();

            predicate = new FunctionPredicate<int>(a => a > 0);
            predicate.Validate(1).Should().BeTrue();
            predicate.Validate(0.0).Should().BeFalse();
            predicate.Validate("-1").Should().BeFalse();

            predicate = new FunctionPredicate<TestEnum>(a => a == TestEnum.E2);
            predicate.Validate(0).Should().BeFalse();
            predicate.Validate(1).Should().BeFalse();
            predicate.Validate(TestEnum.E1).Should().BeFalse();
            predicate.Validate(2).Should().BeTrue();
            predicate.Validate(TestEnum.E2).Should().BeTrue();
            predicate.Validate(3).Should().BeFalse();
            predicate.Validate(TestEnum.E3).Should().BeFalse();
            predicate.Validate(4).Should().BeFalse();

            predicate = new IsEnumValueCorrectPredicate(typeof(TestEnum?));
            predicate.Validate(TestEnum.E1).Should().BeTrue();
            predicate.Validate(1).Should().BeTrue();
            predicate.Validate("E1").Should().BeTrue();
            predicate.Validate("1").Should().BeFalse();
            predicate.Validate(0).Should().BeFalse();
            predicate.Validate(null).Should().BeFalse();

            predicate = new DoesMatchPredicate(typeof(string), "^1.+");
            predicate.Validate(null).Should().BeFalse();
            predicate.Validate("").Should().BeFalse();
            predicate.Validate("2aaaa").Should().BeFalse();
            predicate.Validate("a1aaaa").Should().BeFalse();
            predicate.Validate(1000).Should().BeTrue();
            predicate.Validate("1aaaa").Should().BeTrue();

            predicate = new IsShorterThanPredicate(typeof(object), 5);
            predicate.Validate(null).Should().BeTrue();
            predicate.Validate("").Should().BeTrue();
            predicate.Validate("123").Should().BeTrue();
            predicate.Validate(123).Should().BeTrue();
            predicate.Validate(new int[4]).Should().BeTrue();
            predicate.Validate(new Dictionary<int, int>() { { 1, 1 }, { 2, 2 } }).Should().BeTrue();
            predicate.Validate("12345").Should().BeFalse();
            predicate.Validate(-1234).Should().BeFalse();
            predicate.Validate(new int[5]).Should().BeFalse();
            predicate.Validate(new Dictionary<int, int>() { { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 }, { 5, 5 } }).Should().BeFalse();

            predicate = new EmailAddressPredicate();
            predicate.Validate("a@b.c").Should().BeFalse();
            predicate.Validate("a@b.co").Should().BeTrue();
            predicate.Validate("a@b.com").Should().BeTrue();
            predicate.Validate("my.address@mydomain.it.can.be.long.veryNewDomain").Should().BeTrue();
            predicate.Validate("my.add'ress@mydomain.it.can.be.long.veryNewDomain").Should().BeTrue();
            predicate.Validate("петяпупупкин@из.петушков.рф").Should().BeTrue();
            predicate.Validate("@mydomain.it.can.be.long.veryNewDomain").Should().BeFalse();
            predicate.Validate("address@mydomain").Should().BeFalse();
            predicate.Validate("address").Should().BeFalse();

            predicate = new PhoneNumberPredicate();
            predicate.Validate("2013108861").Should().BeTrue();
            predicate.Validate("(201) 310 8861").Should().BeTrue();
            predicate.Validate("201-310-8861").Should().BeTrue();
            predicate.Validate("+1 201-310-8861").Should().BeTrue();
            predicate.Validate("+79139701980").Should().BeTrue();
            predicate.Validate("+7 (913) 970-1980").Should().BeTrue();
            predicate.Validate("201310886").Should().BeFalse();
            predicate.Validate("-7 (913) 970-1980").Should().BeFalse();
            predicate.Validate("+7 (913) 970-198").Should().BeFalse();
            predicate.Validate("(913) 97 1980").Should().BeFalse();

            predicate = new CreditCardNumberPredicate(typeof(string));
            predicate.Validate("4024007118787224").Should().BeTrue();
            predicate.Validate("4024 0071 1878 7224").Should().BeTrue();
            predicate.Validate("4024-0071-1878-7224").Should().BeTrue();
            predicate.Validate("379381132444727").Should().BeTrue();
            predicate.Validate("5499403770619321").Should().BeTrue();
            predicate.Validate("6376124198503386").Should().BeTrue();
            predicate.Validate("5373792250060563").Should().BeTrue();
            predicate.Validate("4024-0071-1878-7225").Should().BeFalse();
            predicate.Validate("4024-0071-1878-722").Should().BeFalse();
            predicate.Validate("4024-0071-1878-72").Should().BeFalse();

            predicate = new DoesNotMatchPredicate(typeof(string), @"(\w+)\s+(\w+)");
            predicate.Validate("abcd").Should().BeTrue();
            predicate.Validate("abcd efg").Should().BeFalse();

            predicate = new HtmlInjectionPredicate();
            predicate.Validate("abcd & eqlm").Should().BeTrue();
            predicate.Validate("<a href=123>").Should().BeFalse();
        }

        [Fact]
        public void TestExpressionUtils()
        {
            Expression<Func<TestTarget2, object>> expression1;
            expression1 = target2 => target2;
            ExpressionUtils.ExpressionToName(expression1).Should().Be("");
            expression1 = target2 => target2.ID;
            ExpressionUtils.ExpressionToName(expression1).Should().Be("ID");
            expression1 = target2 => target2.SelfReference.ID;
            ExpressionUtils.ExpressionToName(expression1).Should().Be("SelfReference.ID");
            expression1 = target2 => target2.MultiReference[1];
            ExpressionUtils.ExpressionToName(expression1).Should().Be("MultiReference[1]");
            expression1 = target2 => target2.MultiReference[1].Names[2];
            ExpressionUtils.ExpressionToName(expression1).Should().Be("MultiReference[1].Names[2]");
        }

        [Fact]
        public void TestTargets()
        {
            TestTarget1 target1 = new TestTarget1() { };
            TestTarget1 target2 = new TestTarget1() { ID = 1, Names = new string[] { } };
            TestTarget1 target3 = new TestTarget1() { ID = 2, Names = new string[] { "aaa", "bbb", "ccc" } };

            ValidationTarget target;

            target = new EntityValidationTarget(typeof(TestTarget1), "entity");
            target.ValueType.Should().Be(typeof(TestTarget1));
            target.IsSingleValue.Should().BeTrue();
            target.First(target1).Value.Should().Be(target1);
            target.First(target1).Name.Should().Be("entity");
            target.All(target1).Length.Should().Be(1);

            target = new PropertyValidationTarget(typeof(TestTarget1), nameof(TestTarget1.ID));

            target.ValueType.Should().Be(typeof(int));
            target.IsSingleValue.Should().BeTrue();
            target.First(target2).Value.Should().Be(1);
            target.First(target1).Name.Should().Be("ID");
            target.All(target1).Length.Should().Be(1);
            target.IsProperty.Should().BeTrue();

            target = new PropertyValidationArrayTarget(typeof(TestTarget1), nameof(TestTarget1.Names));
            target.ValueType.Should().Be(typeof(string));
            target.IsSingleValue.Should().BeFalse();
            target.First(target3).Value.Should().Be("aaa");
            target.First(target3).Name.Should().Be("Names[0]");
            target.All(target3).Length.Should().Be(3);
            target.All(target3)[2].Value.Should().Be("ccc");
            target.All(target3)[2].Name.Should().Be("Names[2]");
            target.All(target1).Length.Should().Be(0);
            target.All(target2).Length.Should().Be(0);

            target = new PropertyValidationArrayTarget(typeof(TestTarget1), nameof(TestTarget1.ENames));
            target.ValueType.Should().Be(typeof(string));
            target.IsSingleValue.Should().BeFalse();
            target.All(target2).Length.Should().Be(0);
            target.All(target3).Length.Should().Be(3);

            target = new FunctionValidationTarget<TestTarget1, int>(a => a.ID, null);
            target.ValueType.Should().Be(typeof(int));
            target.IsSingleValue.Should().BeTrue();
            target.First(target1).Name.Should().Be("ID");
            target.First(target1).Value.Should().Be(0);
            target.IsProperty.Should().BeTrue();
            target.PropertyName.Should().Be(nameof(TestTarget1.ID));

            target = new FunctionValidationTarget<TestTarget1, int>(a => a.ID + 2, "f1");
            target.ValueType.Should().Be(typeof(int));
            target.IsSingleValue.Should().BeTrue();
            target.First(target1).Name.Should().Be("f1");
            target.First(target1).Value.Should().Be(2);
            target.IsProperty.Should().BeFalse();
            target.PropertyName.Should().BeNull();

            ((Action)(() => new FunctionValidationTarget<TestTarget1, int>(a => a.ID + 2, null))).Should().Throw<ArgumentException>();

            target = new FunctionValidationArrayTarget<TestTarget1, string[]>(a => a.Names, null);
            target.ValueType.Should().Be(typeof(string));
            target.IsSingleValue.Should().BeFalse();
            target.First(target3).Value.Should().Be("aaa");
            target.First(target3).Name.Should().Be("Names[0]");
            target.All(target3).Length.Should().Be(3);
            target.All(target3)[2].Value.Should().Be("ccc");
            target.All(target3)[2].Name.Should().Be("Names[2]");
            target.All(target2).Length.Should().Be(0);
        }
    }
}