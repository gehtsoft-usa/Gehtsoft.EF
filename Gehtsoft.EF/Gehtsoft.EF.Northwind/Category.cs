using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Northwind
{
    [Entity(Table = "nw_cat", Scope = "northwind")]
    public class Category
    {
        [EntityProperty(Field = "categoryID", PrimaryKey = true, Autoincrement = true)]
        public int CategoryID { get; set; }

        [EntityProperty(Field = "categoryName", Size = 128, Nullable = false, Sorted = true)]
        public string CategoryName { get; set; }

        [EntityProperty(Field = "description", Nullable = false)]
        public string Description { get; set; }
    }
}