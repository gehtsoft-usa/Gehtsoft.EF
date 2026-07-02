using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gehtsoft.EF.Mapper;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    /// <summary>
    /// Targeted coverage for Gehtsoft.Mapper paths the scenario suites in
    /// <see cref="TestMappingNewMaps"/> / <see cref="TestMappingNewPrimitives"/> do not reach:
    /// the interface-typed fluent overloads, the MappingAction predicate helpers, the property /
    /// action collections (incl. the never-used non-generic collection and explicit interface
    /// members), the filtered <c>MapPropertiesByName</c> overload, the positional
    /// <see cref="MapPropertyAttribute"/> constructors, the <see cref="ClassToModelInitializer"/>
    /// error/skip branches, the map guard clauses and a couple of <see cref="ValueMapper"/> edges.
    /// Local <c>new Map&lt;,&gt;()</c> instances are used wherever possible so the tests do not
    /// pollute the process-wide <see cref="MapFactory"/> registry.
    /// </summary>
    public class TestMapperCoverage
    {
        public class Src
        {
            public string Value { get; set; }
            public int Number { get; set; }
            public string Other { get; set; }
        }

        public class Dst
        {
            public string Value { get; set; }
            public int Number { get; set; }
            public string Other { get; set; }
        }

        private static PropertyInfo Prop<T>(string name) => typeof(T).GetProperty(name);

        // =====================================================================
        // PropertyMapping<> fluent overloads that take the mapping interfaces directly
        // =====================================================================

        [Fact]
        public void Fluent_Interface_Overloads_Build_A_Working_Rule()
        {
            var map = new Map<Src, Dst>();
            map.For(d => d.Value)
                .From(new ClassPropertyAccessor(Prop<Src>(nameof(Src.Value))) as IMappingSource)
                .To(new ClassPropertyAccessor(Prop<Dst>(nameof(Dst.Value))) as IMappingTarget)
                .When(new MappingPredicate<Src>(s => true) as IMappingPredicate)
                .WithFlags(MapFlag.TrimStrings);

            var dst = new Dst();
            map.Do(new Src { Value = "  hi  " }, dst);
            dst.Value.Should().Be("hi"); // trimmed via WithFlags
        }

        [Fact]
        public void Fluent_To_Action_Overload_Sets_Via_Action()
        {
            var map = new Map<Src, Dst>();
            map.For(d => d.Value).From(s => s.Value).To<string>((d, v) => d.Value = v + "!");

            var dst = new Dst();
            map.Do(new Src { Value = "x" }, dst);
            dst.Value.Should().Be("x!");
        }

        [Fact]
        public void Fluent_From_String_Throws_When_Property_Missing()
        {
            var map = new Map<Src, Dst>();
            var mapping = map.For(d => d.Value);
            mapping.Invoking(m => m.From("Nonexistent")).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Otherwise_On_Unconditional_Rule_Never_Applies()
        {
            var map = new Map<Src, Dst>();
            // primary rule has no When predicate; Otherwise() must produce a never-matching branch
            var primary = map.For(d => d.Value).From(s => s.Value);
            var otherwise = primary.Otherwise().Assign("fallback");

            var dst = new Dst();
            map.Do(new Src { Value = "real" }, dst);
            dst.Value.Should().Be("real"); // the Otherwise branch is suppressed
        }

        [Fact]
        public void PropertyMapping_Map_Object_Overload_Applies_Rule()
        {
            var map = new Map<Src, Dst>();
            var pm = map.For(d => d.Value).From(s => s.Value);

            object source = new Src { Value = "z" };
            object destination = new Dst();
            pm.Map(source, destination); // the object/object overload
            ((Dst)destination).Value.Should().Be("z");
        }

        // =====================================================================
        // MappingAction predicate helpers (tested directly against Perform)
        // =====================================================================

        [Fact]
        public void MappingAction_Predicate_Helpers()
        {
            var dst = new Dst();

            int calls = 0;
            var whenNull = new MappingAction<Src, Dst>((s, d) => calls++);
            whenNull.WhenNull();
            whenNull.Perform(null, dst); // s == null -> runs
            whenNull.Perform(new Src(), dst); // s != null -> skipped
            calls.Should().Be(1);

            calls = 0;
            var whenNotNull = new MappingAction<Src, Dst>((s, d) => calls++);
            whenNotNull.WhenNotNull();
            whenNotNull.Perform(new Src(), dst); // runs
            whenNotNull.Perform(null, dst); // skipped
            calls.Should().Be(1);

            calls = 0;
            var unless = new MappingAction<Src, Dst>((s, d) => calls++);
            unless.Unless((s, t) => true); // predicate true -> Perform returns early
            unless.Perform(new Src(), dst);
            calls.Should().Be(0);

            calls = 0;
            var withPredicate = new MappingAction<Src, Dst>((s, d) => calls++, (s, t) => s != null);
            withPredicate.Perform(new Src(), dst);
            withPredicate.Perform(null, dst);
            calls.Should().Be(1);
        }

        // =====================================================================
        // Collections
        // =====================================================================

        [Fact]
        public void MappingActionCollection_Explicit_Interface_Members()
        {
            var collection = new MappingActionCollection<Src, Dst>();
            IMappingActionCollection asInterface = collection;

            var action = new MappingAction<Src, Dst>((s, d) => { });
            asInterface.Add(action);
            collection.Count.Should().Be(1);
            asInterface[0].Should().BeSameAs(action);

            ((IEnumerable<IMappingAction>)collection).Count().Should().Be(1);
            ((IEnumerable)collection).GetEnumerator().Should().NotBeNull();

            // an action of the wrong generic type is silently ignored
            asInterface.Add(new MappingAction<Dst, Src>((d, s) => { }));
            collection.Count.Should().Be(1);
        }

        [Fact]
        public void NonGeneric_PropertyMappingCollection_Works()
        {
            var map = new Map<Src, Dst>();
            var first = map.For(d => d.Value);
            var second = map.For(d => d.Other);

            var collection = new PropertyMappingCollection();
            collection.Count.Should().Be(0);
            collection.Add(first);
            collection.Count.Should().Be(1);
            collection[0].Should().BeSameAs(first);
            ((IPropertyMappingCollection)collection)[0].Should().BeSameAs(first);

            collection.Find(first.Target).Should().Be(0);
            collection.Find(second.Target).Should().Be(-1);

            ((IEnumerable<IPropertyMapping>)collection).Count().Should().Be(1);
            ((IEnumerable)collection).GetEnumerator().Should().NotBeNull();
        }

        [Fact]
        public void Generic_PropertyMappingCollection_Explicit_Members()
        {
            var map = new Map<Src, Dst>();
            map.For(d => d.Value);
            var collection = map.Mappings;
            IPropertyMappingCollection asInterface = collection;

            asInterface[0].Should().NotBeNull();
            collection.Find(collection[0].Target).Should().Be(0);
            collection.Find(new ClassPropertyAccessor(Prop<Dst>(nameof(Dst.Other)))).Should().Be(-1);
            ((IEnumerable)collection).GetEnumerator().Should().NotBeNull();

            // adding a compatible mapping through the interface does not throw
            asInterface.Invoking(c => c.Add(collection[0])).Should().NotThrow();

            // adding an incompatible mapping does
            var wrong = new Map<Dst, Src>().For(s => s.Value);
            asInterface.Invoking(c => c.Add(wrong)).Should().Throw<ArgumentException>();
        }

        // =====================================================================
        // MapPropertiesByName with filters
        // =====================================================================

        public class ByNameSource
        {
            public int Keep { get; set; }
            public string Text { get; set; }
            public ByNameSource Nested { get; set; }
            [DoNotAutoMap] public int SkippedSource { get; set; }
            public int OnlyInSource { get; set; }
            public int DestIsIgnored { get; set; }
            public Guid TypeIgnored { get; set; }
        }

        public class ByNameDest
        {
            public int Keep { get; set; }
            public string Text { get; set; }
            public ByNameDest Nested { get; set; }
            [DoNotAutoMap] public int DestIsIgnored { get; set; }
            public Guid TypeIgnored { get; set; }
        }

        [Fact]
        public void MapPropertiesByName_Honours_All_Filters()
        {
            var map = new Map<ByNameSource, ByNameDest>();
            map.MapPropertiesByName(
                onlyValueTypes: true,
                propertyIgnoreList: new[] { nameof(ByNameSource.Text) },
                typeIgnoreList: new[] { typeof(Guid) });

            map.ContainsRuleFor(nameof(ByNameDest.Keep)).Should().BeTrue();      // value type, not filtered
            map.ContainsRuleFor(nameof(ByNameDest.Text)).Should().BeFalse();     // in property ignore list
            map.ContainsRuleFor(nameof(ByNameDest.Nested)).Should().BeFalse();   // not a value type (onlyValueTypes)
            map.ContainsRuleFor(nameof(ByNameDest.DestIsIgnored)).Should().BeFalse(); // dest has [DoNotAutoMap]
            map.ContainsRuleFor(nameof(ByNameDest.TypeIgnored)).Should().BeFalse();   // type in typeIgnoreList
            map.ContainsRuleFor(nameof(ByNameSource.SkippedSource)).Should().BeFalse(); // source has [DoNotAutoMap]
            map.ContainsRuleFor(nameof(ByNameSource.OnlyInSource)).Should().BeFalse();  // no matching dest property

            var dst = new ByNameDest();
            map.Do(new ByNameSource { Keep = 42, Text = "x" }, dst);
            dst.Keep.Should().Be(42);
            dst.Text.Should().BeNull();
        }

        // =====================================================================
        // MapPropertyAttribute positional constructors + ClassToModelInitializer
        // =====================================================================

        public class PosSource
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        [MapClass(typeof(PosSource))]
        public class PosModel
        {
            [MapProperty("A", MapFlag.TrimStrings)] public string A { get; set; }
            [MapProperty(MapFlag.TrimStrings)] public string B { get; set; }
        }

        [Fact]
        public void Positional_MapProperty_Ctors_And_SourceToModel()
        {
            var map = new Map<PosSource, PosModel>();
            new ClassToModelInitializer().SourceToModel(map);

            var model = new PosModel();
            map.Do(new PosSource { A = "  a  ", B = "  b  " }, model);
            model.A.Should().Be("a");
            model.B.Should().Be("b");
        }

        [Fact]
        public void ModelToSource_Maps_Back()
        {
            var map = new Map<PosModel, PosSource>();
            new ClassToModelInitializer().ModelToSource(map);

            var source = new PosSource();
            map.Do(new PosModel { A = "  a  ", B = "  b  " }, source);
            source.A.Should().Be("a");
            source.B.Should().Be("b");
        }

        public class NoAttr
        {
            public string A { get; set; }
        }

        [MapClass(typeof(PosSource))]
        public class BadNameModel
        {
            [MapProperty(Name = "Nonexistent")] public string A { get; set; }
        }

        [MapClass(typeof(PosSource))]
        public class SkipModel
        {
            [DoNotAutoMap] public string A { get; set; }
            [MapProperty] public string B { get; set; }
        }

        public class SrcWithSkip
        {
            [DoNotAutoMap] public string B { get; set; }
            public string A { get; set; }
        }

        [MapClass(typeof(SrcWithSkip))]
        public class OtherSkipModel
        {
            [MapProperty] public string B { get; set; }
        }

        [Fact]
        public void ClassToModelInitializer_Error_And_Skip_Branches()
        {
            var initializer = new ClassToModelInitializer();

            // SourceToModel: model without a matching MapClass attribute
            initializer.Invoking(i => i.SourceToModel(new Map<PosSource, NoAttr>()))
                .Should().Throw<InvalidOperationException>();

            // SourceToModel: mapped property missing in the other class
            initializer.Invoking(i => i.SourceToModel(new Map<PosSource, BadNameModel>()))
                .Should().Throw<InvalidOperationException>();

            // SourceToModel: [DoNotAutoMap] on the model property is skipped
            var skipMap = new Map<PosSource, SkipModel>();
            initializer.SourceToModel(skipMap);
            skipMap.ContainsRuleFor(nameof(SkipModel.A)).Should().BeFalse();
            skipMap.ContainsRuleFor(nameof(SkipModel.B)).Should().BeTrue();

            // SourceToModel: [DoNotAutoMap] on the other-class property is skipped
            var otherSkipMap = new Map<SrcWithSkip, OtherSkipModel>();
            initializer.SourceToModel(otherSkipMap);
            otherSkipMap.ContainsRuleFor(nameof(OtherSkipModel.B)).Should().BeFalse();

            // ModelToSource mirrors the same guards
            initializer.Invoking(i => i.ModelToSource(new Map<NoAttr, PosSource>()))
                .Should().Throw<InvalidOperationException>();
            initializer.Invoking(i => i.ModelToSource(new Map<BadNameModel, PosSource>()))
                .Should().Throw<InvalidOperationException>();

            var modelToSkip = new Map<SkipModel, PosSource>();
            initializer.ModelToSource(modelToSkip);
            modelToSkip.ContainsRuleFor(nameof(PosSource.B)).Should().BeTrue();

            var otherToSkip = new Map<OtherSkipModel, SrcWithSkip>();
            initializer.ModelToSource(otherToSkip);
            otherToSkip.ContainsRuleFor(nameof(SrcWithSkip.B)).Should().BeFalse();
        }

        // =====================================================================
        // Map guard clauses and IMap members
        // =====================================================================

        [Fact]
        public void Map_For_Guards()
        {
            var map = new Map<Src, Dst>();
            map.Invoking(m => m.For("Nonexistent")).Should().Throw<ArgumentException>();
            map.Invoking(m => m.For(d => d.Value.Length)).Should().Throw<InvalidOperationException>();
            map.ContainsRuleFor("Nonexistent").Should().BeFalse();
        }

        [Fact]
        public void Map_For_Accepts_Convert_Expression()
        {
            var map = new Map<Src, Dst>();
            // a cast in the target expression must be unwrapped to the underlying property
            map.Invoking(m => m.For(d => (long)d.Number)).Should().NotThrow();
            map.ContainsRuleFor(nameof(Dst.Number)).Should().BeTrue();
        }

        [Fact]
        public void Map_IMap_Members()
        {
            var map = new Map<Src, Dst>();
            map.For(d => d.Value).From(s => s.Value);
            IMap asInterface = map;

            asInterface.Pre.Should().NotBeNull();
            asInterface.Post.Should().NotBeNull();

            var dst = new Dst();
            asInterface.Do(new Src { Value = "x" }, dst, true);
            dst.Value.Should().Be("x");

            int count = 0;
            foreach (IPropertyMapping mapping in asInterface)
                count++;
            count.Should().Be(1);

            ((IEnumerable)map).GetEnumerator().Should().NotBeNull();
        }

        [Fact]
        public void Map_Do_Returns_Default_When_Null_And_NullToNull_Disabled()
        {
            var map = new Map<Src, Dst> { MapNullToNull = false };
            map.Do((Src)null).Should().BeNull();
        }

        // =====================================================================
        // MapFactory / ValueMapper edges
        // =====================================================================

        public class FactorySource { public int Value { get; set; } }
        public class FactoryDest { public int Value { get; set; } public string Tag { get; set; } }

        [Fact]
        public void MapFactory_HasMap_Reflects_Registration()
        {
            try
            {
                MapFactory.HasMap<FactorySource, FactoryDest>().Should().BeFalse();
                MapFactory.CreateMap<FactorySource, FactoryDest>();
                MapFactory.HasMap(typeof(FactorySource), typeof(FactoryDest)).Should().BeTrue();
            }
            finally
            {
                MapFactory.RemoveMap<FactorySource, FactoryDest>();
            }
        }

        [Fact]
        public void ValueMapper_Maps_Object_Target_As_Passthrough()
        {
            var src = new Src { Value = "x" };
            ValueMapper.MapValue(src, typeof(object)).Should().BeSameAs(src);
        }

        [Fact]
        public void ValueMapper_Maps_Array_Into_NonGeneric_List()
        {
            var result = ValueMapper.MapValue(new[] { 1, 2, 3 }, typeof(ArrayList));
            result.Should().BeOfType<ArrayList>();
            ((ArrayList)result).Count.Should().Be(3);
        }

        [Fact]
        public void Map_Find_Returns_Matching_Rules()
        {
            var map = new Map<Src, Dst>();
            map.For(d => d.Value).From(s => s.Value);

            map.Find(nameof(Dst.Value)).Should().HaveCount(1);
            map.Find(d => d.Value).Should().HaveCount(1);
            map.Find(nameof(Dst.Other)).Should().BeEmpty();
            map.Find("Nonexistent").Should().BeEmpty();

            // Find<TValue> is an iterator, so the invalid-expression guard throws on enumeration
            map.Invoking(m => m.Find(d => d.Value.Length).ToList()).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Map_Equality_Is_By_Source_And_Destination()
        {
            var a = new Map<Src, Dst>();
            var b = new Map<Src, Dst>();
            var c = new Map<Dst, Src>();

            a.Equals((object)a).Should().BeTrue();
            a.Equals((object)b).Should().BeTrue();
            a.Equals((IMap)b).Should().BeTrue();
            a.Equals((object)c).Should().BeFalse();
            a.Equals((object)null).Should().BeFalse();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void ActionTarget_And_ConstSource_Basics()
        {
            var map = new Map<Src, Dst>();
            var mapping = map.For(d => d.Value).To<string>((d, v) => d.Value = v);
            IMappingTarget target = mapping.Target;

            target.Name.Should().Be("Action");
            target.Equals(target).Should().BeTrue();
            target.Equals(new ClassPropertyAccessor(Prop<Dst>(nameof(Dst.Value)))).Should().BeFalse();
            target.Set(null, "x"); // null destination is a no-op

            var source = new ConstSource<int>(5);
            source.Name.Should().Be("const");
            source.ValueType.Should().Be(typeof(int));
            source.Get(new Src()).Should().Be(5);
        }

        [Fact]
        public void ValueMapper_Uses_Map_Factory_When_Present()
        {
            try
            {
                var map = MapFactory.CreateMap<FactorySource, FactoryDest>();
                map.Factory = s => new FactoryDest { Tag = "from-factory" };
                map.For(d => d.Value).From(s => s.Value);

                var result = (FactoryDest)ValueMapper.MapValue(new FactorySource { Value = 7 }, typeof(FactoryDest));
                result.Tag.Should().Be("from-factory");
                result.Value.Should().Be(7);
            }
            finally
            {
                MapFactory.RemoveMap<FactorySource, FactoryDest>();
            }
        }
    }
}
