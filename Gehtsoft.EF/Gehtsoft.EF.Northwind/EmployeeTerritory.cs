using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_empl_terr", Scope = "northwind")]
    public class EmployeeTerritory
    {
        [AutoId(Field = "referenceID")]
        public int Id { get; set; }

        [EntityProperty(Field = "employeeID", ForeignKey = true, Nullable = false)]
        public Employee Employee { get; set; }

        [EntityProperty(Field = "territoryID", ForeignKey = true, Nullable = false)]
        public Territory Territory { get; set; }
    }
}