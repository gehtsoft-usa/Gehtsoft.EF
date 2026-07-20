using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbSpecifics : Sql92LanguageSpecifics
    {
        public override string DbName => DbNameSpec;
        public string DbNameSpec { get; set; } = "dummy";
        public override bool AllNonAggregatesInGroupBy => AllNonAggregatesInGroupBySpec;
        public bool AllNonAggregatesInGroupBySpec { get; set; } = false;
        public override bool OuterJoinSupported => OuterJoinSupportedSpec;
        public bool OuterJoinSupportedSpec { get; set; } = false;
        public override bool SupportFunctionsInIndexes => SupportFunctionsInIndexesSpec;
        public bool SupportFunctionsInIndexesSpec { get; set; } = false;
        public override bool DropColumnSupported => DropColumnSupportedSpec;
        public bool DropColumnSupportedSpec { get; set; } = true;

        // Geometry query support is opt-in so the "unsupported dialect throws" tests keep a non-geo
        // dialect. When enabled, the dummy renders the portable OGC ST_* grammar (borrowed from SQLite),
        // which is enough to exercise the driver-agnostic builder wiring off a live database.
        public override bool SupportsGeometry => SupportsGeometrySpec;
        public bool SupportsGeometrySpec { get; set; } = false;

        public override string GeometryFunction(in GeoFunctionRequest request)
            => SupportsGeometrySpec ? RenderOgcGeometryFunction(request) : base.GeometryFunction(request);

        public override string GeometryPredicate(in GeoPredicateRequest request)
            => SupportsGeometrySpec ? RenderOgcGeometryPredicate(request, nativeDWithin: false) : base.GeometryPredicate(request);
    }
}
