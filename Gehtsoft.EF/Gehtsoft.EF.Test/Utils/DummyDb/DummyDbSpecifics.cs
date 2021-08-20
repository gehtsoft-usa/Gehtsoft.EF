using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbSpecifics : Sql92LanguageSpecifics
    {
        public override bool SupportFunctionsInIndexes => SupportFunctionsInIndexesSpec;
        public bool SupportFunctionsInIndexesSpec { get; set; } = false;
    }
}
