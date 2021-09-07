using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbSpecifics : Sql92LanguageSpecifics
    {
        public override bool AllNonAggregatesInGroupBy => AllNonAggregatesInGroupBySpec;
        public bool AllNonAggregatesInGroupBySpec { get; set; } = false;
        public override bool OuterJoinSupported => OuterJoinSupportedSpec;
        public bool OuterJoinSupportedSpec { get; set; } = false;
        public override bool SupportFunctionsInIndexes => SupportFunctionsInIndexesSpec;
        public bool SupportFunctionsInIndexesSpec { get; set; } = false;
        public override bool DropColumnSupported => DropColumnSupportedSpec;
        public bool DropColumnSupportedSpec { get; set; } = true;
    }
}
