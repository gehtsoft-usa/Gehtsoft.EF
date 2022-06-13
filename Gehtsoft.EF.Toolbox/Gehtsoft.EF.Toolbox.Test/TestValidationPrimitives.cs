using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Gehtsoft.Validator;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    [TestFixture()]
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

        [Test]
        public void TestPredicates()
        {
            IValidationPredicate predicate;
            predicate = new IsNullPredicate(typeof(object));
            Assert.IsTrue(predicate.Validate(null));
            Assert.IsFalse(predicate.Validate(5));
            Assert.IsFalse(predicate.Validate(predicate));

            predicate = new IsNotNullPredicate(typeof(object));
            Assert.IsFalse(predicate.Validate(null));
            Assert.IsTrue(predicate.Validate(5));
            Assert.IsTrue(predicate.Validate(""));
            Assert.IsTrue(predicate.Validate(predicate));

            predicate = new IsNotNullOrEmptyPredicate(typeof(object));
            Assert.IsFalse(predicate.Validate(null));
            Assert.IsTrue(predicate.Validate(5));
            Assert.IsFalse(predicate.Validate(""));
            Assert.IsTrue(predicate.Validate("     "));
            Assert.IsTrue(predicate.Validate(predicate));
            Assert.IsTrue(predicate.Validate("1"));

            Assert.IsTrue(predicate.Validate(new int[] { 1 }));
            Assert.IsFalse(predicate.Validate(new int[0]));
            List<int> l = new List<int>();
            Assert.IsFalse(predicate.Validate(l));
            l.Add(1);
            Assert.IsTrue(predicate.Validate(l));

            predicate = new IsNotNullOrWhitespacePredicate(typeof(object));
            Assert.IsFalse(predicate.Validate(null));
            Assert.IsTrue(predicate.Validate(5));
            Assert.IsFalse(predicate.Validate(""));
            Assert.IsFalse(predicate.Validate("     "));
            Assert.IsTrue(predicate.Validate(predicate));
            Assert.IsTrue(predicate.Validate("1"));

            predicate = new ValueIsBetweenPredicate(typeof(int), 1.0, true, null, false);
            Assert.IsFalse(predicate.Validate(0.9999999));
            Assert.IsTrue(predicate.Validate(1.0));
            Assert.IsTrue(predicate.Validate(1.1));

            predicate = new ValueIsBetweenPredicate(typeof(int), 1.0, false, null, false);
            Assert.IsFalse(predicate.Validate(0.9999999));
            Assert.IsFalse(predicate.Validate(1.0));
            Assert.IsTrue(predicate.Validate(1.1));

            predicate = new ValueIsBetweenPredicate(typeof(int), null, false, 1.0, true);
            Assert.IsTrue(predicate.Validate(0.9999999));
            Assert.IsTrue(predicate.Validate(1.0));
            Assert.IsFalse(predicate.Validate(1.1));

            predicate = new ValueIsBetweenPredicate(typeof(int), null, false, 1.0, false);
            Assert.IsTrue(predicate.Validate(0.9999999));
            Assert.IsFalse(predicate.Validate(1.0));
            Assert.IsFalse(predicate.Validate(1.1));

            predicate = new FunctionPredicate<int>(a => a > 0);
            Assert.IsTrue(predicate.Validate(1));
            Assert.IsFalse(predicate.Validate(0.0));
            Assert.IsFalse(predicate.Validate("-1"));

            predicate = new FunctionPredicate<TestEnum>(a => a == TestEnum.E2);
            Assert.IsFalse(predicate.Validate(0));
            Assert.IsFalse(predicate.Validate(1));
            Assert.IsFalse(predicate.Validate(TestEnum.E1));
            Assert.IsTrue(predicate.Validate(2));
            Assert.IsTrue(predicate.Validate(TestEnum.E2));
            Assert.IsFalse(predicate.Validate(3));
            Assert.IsFalse(predicate.Validate(TestEnum.E3));
            Assert.IsFalse(predicate.Validate(4));

            predicate = new IsEnumValueCorrectPredicate(typeof(TestEnum?));
            Assert.IsTrue(predicate.Validate(TestEnum.E1));
            Assert.IsTrue(predicate.Validate(1));
            Assert.IsTrue(predicate.Validate("E1"));
            Assert.IsFalse(predicate.Validate("1"));
            Assert.IsFalse(predicate.Validate(0));
            Assert.IsFalse(predicate.Validate(null));

            predicate = new DoesMatchPredicate(typeof(string), "^1.+");
            Assert.IsFalse(predicate.Validate(null));
            Assert.IsFalse(predicate.Validate(""));
            Assert.IsFalse(predicate.Validate("2aaaa"));
            Assert.IsFalse(predicate.Validate("a1aaaa"));
            Assert.IsTrue(predicate.Validate(1000));
            Assert.IsTrue(predicate.Validate("1aaaa"));

            predicate = new IsShorterThanPredicate(typeof(object), 5);
            Assert.IsTrue(predicate.Validate(null));
            Assert.IsTrue(predicate.Validate(""));
            Assert.IsTrue(predicate.Validate("123"));
            Assert.IsTrue(predicate.Validate(123));
            Assert.IsTrue(predicate.Validate(new int[4]));
            Assert.IsTrue(predicate.Validate(new Dictionary<int, int>() { { 1, 1 }, { 2, 2 } }));
            Assert.IsFalse(predicate.Validate("12345"));
            Assert.IsFalse(predicate.Validate(-1234));
            Assert.IsFalse(predicate.Validate(new int[5]));
            Assert.IsFalse(predicate.Validate(new Dictionary<int, int>() { { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 }, { 5, 5 } }));

            predicate = new EmailAddressPredicate();
            Assert.IsFalse(predicate.Validate("a@b.c"));
            Assert.IsTrue(predicate.Validate("a@b.co"));
            Assert.IsTrue(predicate.Validate("a@b.com"));
            Assert.IsTrue(predicate.Validate("my.address@mydomain.it.can.be.long.veryNewDomain"));
            Assert.IsTrue(predicate.Validate("my.add'ress@mydomain.it.can.be.long.veryNewDomain"));
            Assert.IsTrue(predicate.Validate("петяпупупкин@из.петушков.рф"));
            Assert.IsFalse(predicate.Validate("@mydomain.it.can.be.long.veryNewDomain"));
            Assert.IsFalse(predicate.Validate("address@mydomain"));
            Assert.IsFalse(predicate.Validate("address"));

            predicate = new PhoneNumberPredicate();
            Assert.IsTrue(predicate.Validate("2013108861"));
            Assert.IsTrue(predicate.Validate("(201) 310 8861"));
            Assert.IsTrue(predicate.Validate("201-310-8861"));
            Assert.IsTrue(predicate.Validate("+1 201-310-8861"));
            Assert.IsTrue(predicate.Validate("+79139701980"));
            Assert.IsTrue(predicate.Validate("+7 (913) 970-1980"));
            Assert.IsFalse(predicate.Validate("201310886"));
            Assert.IsFalse(predicate.Validate("-7 (913) 970-1980"));
            Assert.IsFalse(predicate.Validate("+7 (913) 970-198"));
            Assert.IsFalse(predicate.Validate("(913) 97 1980"));

            predicate = new CreditCardNumberPredicate(typeof(string));
            Assert.IsTrue(predicate.Validate("4024007118787224"));
            Assert.IsTrue(predicate.Validate("4024 0071 1878 7224"));
            Assert.IsTrue(predicate.Validate("4024-0071-1878-7224"));
            Assert.IsTrue(predicate.Validate("379381132444727"));
            Assert.IsTrue(predicate.Validate("5499403770619321"));
            Assert.IsTrue(predicate.Validate("6376124198503386"));
            Assert.IsTrue(predicate.Validate("5373792250060563"));
            Assert.IsFalse(predicate.Validate("4024-0071-1878-7225"));
            Assert.IsFalse(predicate.Validate("4024-0071-1878-722"));
            Assert.IsFalse(predicate.Validate("4024-0071-1878-72"));

            predicate = new DoesNotMatchPredicate(typeof(string), @"(\w+)\s+(\w+)");
            Assert.IsTrue(predicate.Validate("abcd"));
            Assert.IsFalse(predicate.Validate("abcd efg"));

            predicate = new HtmlInjectionPredicate();
            Assert.IsTrue(predicate.Validate("abcd & eqlm"));
            Assert.IsFalse(predicate.Validate("<a href=123>"));
        }

        [Test]
        public void TestExpressionUtils()
        {
            Expression<Func<TestTarget2, object>> expression1;
            expression1 = target2 => target2;
            Assert.AreEqual("", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.ID;
            Assert.AreEqual("ID", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.SelfReference.ID;
            Assert.AreEqual("SelfReference.ID", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.MultiReference[1];
            Assert.AreEqual("MultiReference[1]", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.MultiReference[1].Names[2];
            Assert.AreEqual("MultiReference[1].Names[2]", ExpressionUtils.ExpressionToName(expression1));
        }

        [Test]
        public void TestTargets()
        {
            TestTarget1 target1 = new TestTarget1() { };
            TestTarget1 target2 = new TestTarget1() { ID = 1, Names = new string[] { } };
            TestTarget1 target3 = new TestTarget1() { ID = 2, Names = new string[] { "aaa", "bbb", "ccc" } };

            ValidationTarget target;

            target = new EntityValidationTarget(typeof(TestTarget1), "entity");
            Assert.AreEqual(typeof(TestTarget1), target.ValueType);
            Assert.IsTrue(target.IsSingleValue);
            Assert.AreEqual(target1, target.First(target1).Value);
            Assert.AreEqual("entity", target.First(target1).Name);
            Assert.AreEqual(1, target.All(target1).Length);

            target = new PropertyValidationTarget(typeof(TestTarget1), nameof(TestTarget1.ID));

            Assert.AreEqual(typeof(int), target.ValueType);
            Assert.IsTrue(target.IsSingleValue);
            Assert.AreEqual(1, target.First(target2).Value);
            Assert.AreEqual("ID", target.First(target1).Name);
            Assert.AreEqual(1, target.All(target1).Length);
            Assert.IsTrue(target.IsProperty);

            target = new PropertyValidationArrayTarget(typeof(TestTarget1), nameof(TestTarget1.Names));
            Assert.AreEqual(typeof(string), target.ValueType);
            Assert.IsFalse(target.IsSingleValue);
            Assert.AreEqual("aaa", target.First(target3).Value);
            Assert.AreEqual("Names[0]", target.First(target3).Name);
            Assert.AreEqual(3, target.All(target3).Length);
            Assert.AreEqual("ccc", target.All(target3)[2].Value);
            Assert.AreEqual("Names[2]", target.All(target3)[2].Name);
            Assert.AreEqual(0, target.All(target1).Length);
            Assert.AreEqual(0, target.All(target2).Length);

            target = new PropertyValidationArrayTarget(typeof(TestTarget1), nameof(TestTarget1.ENames));
            Assert.AreEqual(typeof(string), target.ValueType);
            Assert.IsFalse(target.IsSingleValue);
            Assert.AreEqual(0, target.All(target2).Length);
            Assert.AreEqual(3, target.All(target3).Length);

            target = new FunctionValidationTarget<TestTarget1, int>(a => a.ID, null);
            Assert.AreEqual(typeof(int), target.ValueType);
            Assert.IsTrue(target.IsSingleValue);
            Assert.AreEqual("ID", target.First(target1).Name);
            Assert.AreEqual(0, target.First(target1).Value);
            Assert.IsTrue(target.IsProperty);
            Assert.AreEqual(nameof(TestTarget1.ID), target.PropertyName);

            target = new FunctionValidationTarget<TestTarget1, int>(a => a.ID + 2, "f1");
            Assert.AreEqual(typeof(int), target.ValueType);
            Assert.IsTrue(target.IsSingleValue);
            Assert.AreEqual("f1", target.First(target1).Name);
            Assert.AreEqual(2, target.First(target1).Value);
            Assert.IsFalse(target.IsProperty);
            Assert.IsNull(target.PropertyName);

            Assert.Throws<ArgumentException>(() => new FunctionValidationTarget<TestTarget1, int>(a => a.ID + 2, null));

            target = new FunctionValidationArrayTarget<TestTarget1, string[]>(a => a.Names, null);
            Assert.AreEqual(typeof(string), target.ValueType);
            Assert.IsFalse(target.IsSingleValue);
            Assert.AreEqual("aaa", target.First(target3).Value);
            Assert.AreEqual("Names[0]", target.First(target3).Name);
            Assert.AreEqual(3, target.All(target3).Length);
            Assert.AreEqual("ccc", target.All(target3)[2].Value);
            Assert.AreEqual("Names[2]", target.All(target3)[2].Name);
            Assert.AreEqual(0, target.All(target2).Length);
        }
    }
}