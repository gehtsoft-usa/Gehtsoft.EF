using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Gehtsoft.Validator;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
            ClassicAssert.IsTrue(predicate.Validate(null));
            ClassicAssert.IsFalse(predicate.Validate(5));
            ClassicAssert.IsFalse(predicate.Validate(predicate));

            predicate = new IsNotNullPredicate(typeof(object));
            ClassicAssert.IsFalse(predicate.Validate(null));
            ClassicAssert.IsTrue(predicate.Validate(5));
            ClassicAssert.IsTrue(predicate.Validate(""));
            ClassicAssert.IsTrue(predicate.Validate(predicate));

            predicate = new IsNotNullOrEmptyPredicate(typeof(object));
            ClassicAssert.IsFalse(predicate.Validate(null));
            ClassicAssert.IsTrue(predicate.Validate(5));
            ClassicAssert.IsFalse(predicate.Validate(""));
            ClassicAssert.IsTrue(predicate.Validate("     "));
            ClassicAssert.IsTrue(predicate.Validate(predicate));
            ClassicAssert.IsTrue(predicate.Validate("1"));

            ClassicAssert.IsTrue(predicate.Validate(new int[] { 1 }));
            ClassicAssert.IsFalse(predicate.Validate(new int[0]));
            List<int> l = new List<int>();
            ClassicAssert.IsFalse(predicate.Validate(l));
            l.Add(1);
            ClassicAssert.IsTrue(predicate.Validate(l));

            predicate = new IsNotNullOrWhitespacePredicate(typeof(object));
            ClassicAssert.IsFalse(predicate.Validate(null));
            ClassicAssert.IsTrue(predicate.Validate(5));
            ClassicAssert.IsFalse(predicate.Validate(""));
            ClassicAssert.IsFalse(predicate.Validate("     "));
            ClassicAssert.IsTrue(predicate.Validate(predicate));
            ClassicAssert.IsTrue(predicate.Validate("1"));

            predicate = new ValueIsBetweenPredicate(typeof(int), 1.0, true, null, false);
            ClassicAssert.IsFalse(predicate.Validate(0.9999999));
            ClassicAssert.IsTrue(predicate.Validate(1.0));
            ClassicAssert.IsTrue(predicate.Validate(1.1));

            predicate = new ValueIsBetweenPredicate(typeof(int), 1.0, false, null, false);
            ClassicAssert.IsFalse(predicate.Validate(0.9999999));
            ClassicAssert.IsFalse(predicate.Validate(1.0));
            ClassicAssert.IsTrue(predicate.Validate(1.1));

            predicate = new ValueIsBetweenPredicate(typeof(int), null, false, 1.0, true);
            ClassicAssert.IsTrue(predicate.Validate(0.9999999));
            ClassicAssert.IsTrue(predicate.Validate(1.0));
            ClassicAssert.IsFalse(predicate.Validate(1.1));

            predicate = new ValueIsBetweenPredicate(typeof(int), null, false, 1.0, false);
            ClassicAssert.IsTrue(predicate.Validate(0.9999999));
            ClassicAssert.IsFalse(predicate.Validate(1.0));
            ClassicAssert.IsFalse(predicate.Validate(1.1));

            predicate = new FunctionPredicate<int>(a => a > 0);
            ClassicAssert.IsTrue(predicate.Validate(1));
            ClassicAssert.IsFalse(predicate.Validate(0.0));
            ClassicAssert.IsFalse(predicate.Validate("-1"));

            predicate = new FunctionPredicate<TestEnum>(a => a == TestEnum.E2);
            ClassicAssert.IsFalse(predicate.Validate(0));
            ClassicAssert.IsFalse(predicate.Validate(1));
            ClassicAssert.IsFalse(predicate.Validate(TestEnum.E1));
            ClassicAssert.IsTrue(predicate.Validate(2));
            ClassicAssert.IsTrue(predicate.Validate(TestEnum.E2));
            ClassicAssert.IsFalse(predicate.Validate(3));
            ClassicAssert.IsFalse(predicate.Validate(TestEnum.E3));
            ClassicAssert.IsFalse(predicate.Validate(4));

            predicate = new IsEnumValueCorrectPredicate(typeof(TestEnum?));
            ClassicAssert.IsTrue(predicate.Validate(TestEnum.E1));
            ClassicAssert.IsTrue(predicate.Validate(1));
            ClassicAssert.IsTrue(predicate.Validate("E1"));
            ClassicAssert.IsFalse(predicate.Validate("1"));
            ClassicAssert.IsFalse(predicate.Validate(0));
            ClassicAssert.IsFalse(predicate.Validate(null));

            predicate = new DoesMatchPredicate(typeof(string), "^1.+");
            ClassicAssert.IsFalse(predicate.Validate(null));
            ClassicAssert.IsFalse(predicate.Validate(""));
            ClassicAssert.IsFalse(predicate.Validate("2aaaa"));
            ClassicAssert.IsFalse(predicate.Validate("a1aaaa"));
            ClassicAssert.IsTrue(predicate.Validate(1000));
            ClassicAssert.IsTrue(predicate.Validate("1aaaa"));

            predicate = new IsShorterThanPredicate(typeof(object), 5);
            ClassicAssert.IsTrue(predicate.Validate(null));
            ClassicAssert.IsTrue(predicate.Validate(""));
            ClassicAssert.IsTrue(predicate.Validate("123"));
            ClassicAssert.IsTrue(predicate.Validate(123));
            ClassicAssert.IsTrue(predicate.Validate(new int[4]));
            ClassicAssert.IsTrue(predicate.Validate(new Dictionary<int, int>() { { 1, 1 }, { 2, 2 } }));
            ClassicAssert.IsFalse(predicate.Validate("12345"));
            ClassicAssert.IsFalse(predicate.Validate(-1234));
            ClassicAssert.IsFalse(predicate.Validate(new int[5]));
            ClassicAssert.IsFalse(predicate.Validate(new Dictionary<int, int>() { { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 }, { 5, 5 } }));

            predicate = new EmailAddressPredicate();
            ClassicAssert.IsFalse(predicate.Validate("a@b.c"));
            ClassicAssert.IsTrue(predicate.Validate("a@b.co"));
            ClassicAssert.IsTrue(predicate.Validate("a@b.com"));
            ClassicAssert.IsTrue(predicate.Validate("my.address@mydomain.it.can.be.long.veryNewDomain"));
            ClassicAssert.IsTrue(predicate.Validate("my.add'ress@mydomain.it.can.be.long.veryNewDomain"));
            ClassicAssert.IsTrue(predicate.Validate("петяпупупкин@из.петушков.рф"));
            ClassicAssert.IsFalse(predicate.Validate("@mydomain.it.can.be.long.veryNewDomain"));
            ClassicAssert.IsFalse(predicate.Validate("address@mydomain"));
            ClassicAssert.IsFalse(predicate.Validate("address"));

            predicate = new PhoneNumberPredicate();
            ClassicAssert.IsTrue(predicate.Validate("2013108861"));
            ClassicAssert.IsTrue(predicate.Validate("(201) 310 8861"));
            ClassicAssert.IsTrue(predicate.Validate("201-310-8861"));
            ClassicAssert.IsTrue(predicate.Validate("+1 201-310-8861"));
            ClassicAssert.IsTrue(predicate.Validate("+79139701980"));
            ClassicAssert.IsTrue(predicate.Validate("+7 (913) 970-1980"));
            ClassicAssert.IsFalse(predicate.Validate("201310886"));
            ClassicAssert.IsFalse(predicate.Validate("-7 (913) 970-1980"));
            ClassicAssert.IsFalse(predicate.Validate("+7 (913) 970-198"));
            ClassicAssert.IsFalse(predicate.Validate("(913) 97 1980"));

            predicate = new CreditCardNumberPredicate(typeof(string));
            ClassicAssert.IsTrue(predicate.Validate("4024007118787224"));
            ClassicAssert.IsTrue(predicate.Validate("4024 0071 1878 7224"));
            ClassicAssert.IsTrue(predicate.Validate("4024-0071-1878-7224"));
            ClassicAssert.IsTrue(predicate.Validate("379381132444727"));
            ClassicAssert.IsTrue(predicate.Validate("5499403770619321"));
            ClassicAssert.IsTrue(predicate.Validate("6376124198503386"));
            ClassicAssert.IsTrue(predicate.Validate("5373792250060563"));
            ClassicAssert.IsFalse(predicate.Validate("4024-0071-1878-7225"));
            ClassicAssert.IsFalse(predicate.Validate("4024-0071-1878-722"));
            ClassicAssert.IsFalse(predicate.Validate("4024-0071-1878-72"));

            predicate = new DoesNotMatchPredicate(typeof(string), @"(\w+)\s+(\w+)");
            ClassicAssert.IsTrue(predicate.Validate("abcd"));
            ClassicAssert.IsFalse(predicate.Validate("abcd efg"));

            predicate = new HtmlInjectionPredicate();
            ClassicAssert.IsTrue(predicate.Validate("abcd & eqlm"));
            ClassicAssert.IsFalse(predicate.Validate("<a href=123>"));
        }

        [Test]
        public void TestExpressionUtils()
        {
            Expression<Func<TestTarget2, object>> expression1;
            expression1 = target2 => target2;
            ClassicAssert.AreEqual("", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.ID;
            ClassicAssert.AreEqual("ID", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.SelfReference.ID;
            ClassicAssert.AreEqual("SelfReference.ID", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.MultiReference[1];
            ClassicAssert.AreEqual("MultiReference[1]", ExpressionUtils.ExpressionToName(expression1));
            expression1 = target2 => target2.MultiReference[1].Names[2];
            ClassicAssert.AreEqual("MultiReference[1].Names[2]", ExpressionUtils.ExpressionToName(expression1));
        }

        [Test]
        public void TestTargets()
        {
            TestTarget1 target1 = new TestTarget1() { };
            TestTarget1 target2 = new TestTarget1() { ID = 1, Names = new string[] { } };
            TestTarget1 target3 = new TestTarget1() { ID = 2, Names = new string[] { "aaa", "bbb", "ccc" } };

            ValidationTarget target;

            target = new EntityValidationTarget(typeof(TestTarget1), "entity");
            ClassicAssert.AreEqual(typeof(TestTarget1), target.ValueType);
            ClassicAssert.IsTrue(target.IsSingleValue);
            ClassicAssert.AreEqual(target1, target.First(target1).Value);
            ClassicAssert.AreEqual("entity", target.First(target1).Name);
            ClassicAssert.AreEqual(1, target.All(target1).Length);

            target = new PropertyValidationTarget(typeof(TestTarget1), nameof(TestTarget1.ID));

            ClassicAssert.AreEqual(typeof(int), target.ValueType);
            ClassicAssert.IsTrue(target.IsSingleValue);
            ClassicAssert.AreEqual(1, target.First(target2).Value);
            ClassicAssert.AreEqual("ID", target.First(target1).Name);
            ClassicAssert.AreEqual(1, target.All(target1).Length);
            ClassicAssert.IsTrue(target.IsProperty);

            target = new PropertyValidationArrayTarget(typeof(TestTarget1), nameof(TestTarget1.Names));
            ClassicAssert.AreEqual(typeof(string), target.ValueType);
            ClassicAssert.IsFalse(target.IsSingleValue);
            ClassicAssert.AreEqual("aaa", target.First(target3).Value);
            ClassicAssert.AreEqual("Names[0]", target.First(target3).Name);
            ClassicAssert.AreEqual(3, target.All(target3).Length);
            ClassicAssert.AreEqual("ccc", target.All(target3)[2].Value);
            ClassicAssert.AreEqual("Names[2]", target.All(target3)[2].Name);
            ClassicAssert.AreEqual(0, target.All(target1).Length);
            ClassicAssert.AreEqual(0, target.All(target2).Length);

            target = new PropertyValidationArrayTarget(typeof(TestTarget1), nameof(TestTarget1.ENames));
            ClassicAssert.AreEqual(typeof(string), target.ValueType);
            ClassicAssert.IsFalse(target.IsSingleValue);
            ClassicAssert.AreEqual(0, target.All(target2).Length);
            ClassicAssert.AreEqual(3, target.All(target3).Length);

            target = new FunctionValidationTarget<TestTarget1, int>(a => a.ID, null);
            ClassicAssert.AreEqual(typeof(int), target.ValueType);
            ClassicAssert.IsTrue(target.IsSingleValue);
            ClassicAssert.AreEqual("ID", target.First(target1).Name);
            ClassicAssert.AreEqual(0, target.First(target1).Value);
            ClassicAssert.IsTrue(target.IsProperty);
            ClassicAssert.AreEqual(nameof(TestTarget1.ID), target.PropertyName);

            target = new FunctionValidationTarget<TestTarget1, int>(a => a.ID + 2, "f1");
            ClassicAssert.AreEqual(typeof(int), target.ValueType);
            ClassicAssert.IsTrue(target.IsSingleValue);
            ClassicAssert.AreEqual("f1", target.First(target1).Name);
            ClassicAssert.AreEqual(2, target.First(target1).Value);
            ClassicAssert.IsFalse(target.IsProperty);
            ClassicAssert.IsNull(target.PropertyName);

            ClassicAssert.Throws<ArgumentException>(() => new FunctionValidationTarget<TestTarget1, int>(a => a.ID + 2, null));

            target = new FunctionValidationArrayTarget<TestTarget1, string[]>(a => a.Names, null);
            ClassicAssert.AreEqual(typeof(string), target.ValueType);
            ClassicAssert.IsFalse(target.IsSingleValue);
            ClassicAssert.AreEqual("aaa", target.First(target3).Value);
            ClassicAssert.AreEqual("Names[0]", target.First(target3).Name);
            ClassicAssert.AreEqual(3, target.All(target3).Length);
            ClassicAssert.AreEqual("ccc", target.All(target3)[2].Value);
            ClassicAssert.AreEqual("Names[2]", target.All(target3)[2].Name);
            ClassicAssert.AreEqual(0, target.All(target2).Length);
        }
    }
}