using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Test.Legacy.Entities
{
    public enum EmpoyeeType
    {
        Manager,
        Salesman,
    }

    [Entity(Scope = "entities", Table = "temployee")]
    public class Employee
    {
        [EntityProperty(AutoId = true)]
        public int ID { get; set; }

        [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
        public string Name { get; set; }

        [EntityProperty(ForeignKey = true, Nullable = true)]
        public Employee Manager { get; set; }

        [EntityProperty(Field = "type", DbType = DbType.Int32, Sorted = true, Nullable = true)]
        public EmpoyeeType? EmpoyeeType1 { get; set; } = EmpoyeeType.Salesman;

        [EntityProperty(Sorted = true)]
        public DateTime? LastCheck { get; set; }

        public Employee()
        {
        }

        public Employee(string name)
        {
            Name = name;
        }

        public Employee(string name, Employee manager)
        {
            Name = name;
            Manager = manager;
        }

        internal bool Found { get; set; }
    }

    [Entity(Scope = "entities", Table = "tcategory")]
    public class Category
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true)]
        public int ID { get; set; }

        [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true)]
        public string Name { get; set; }

        public int Count { get; set; }

        public double Total { get; set; }

        public int Count1 { get; set; }

        public double Total1 { get; set; }

        public Category()
        {
        }

        public Category(int id, string name)
        {
            ID = id;
            Name = name;
        }
    }

    [Entity(Scope = "entities", Table = "tgood")]
    public class Good
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
        public int ID { get; set; }

        [EntityProperty(Field = "category", ForeignKey = true)]
        public Category Category { get; set; }

        [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true, Unique = true)]
        public string Name { get; set; }

        public Good()
        {
        }

        public Good(Category cat, string name)
        {
            Category = cat;
            Name = name;
        }
    }

    [Entity(Scope = "entities", Table = "tsale", Metadata = typeof(SalesMetadata))]
    [PagingLimit(10)]
    public class Sale
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
        public int ID { get; set; }

        [EntityProperty(Field = "saledate", DbType = DbType.DateTime, Sorted = true)]
        public DateTime SalesDate { get; set; }

        [EntityProperty(Field = "salesperson", ForeignKey = true)]
        public Employee SalesPerson { get; set; }

        [EntityProperty(Field = "referenceperson", ForeignKey = true, Nullable = true)]
        public Employee ReferencePerson { get; set; }

        [EntityProperty(Field = "good", ForeignKey = true)]
        public Good Good { get; set; }

        [EntityProperty(Field = "total", DbType = DbType.Double, Size = 8, Precision = 2, DefaultValue = 10)]
        public double Total { get; set; }

        [EntityProperty(Field = "note", Size = 64, Nullable = true)]
        public string Note { get; set; }

        public Sale()
        {
        }

        public Sale(Employee salesPerson, DateTime dateTime, Good good, double total)
        {
            SalesDate = dateTime;
            SalesPerson = salesPerson;
            Good = good;
            Total = total;
        }

        public Sale(Employee salesPerson, Employee reference, DateTime dateTime, Good good, double total)
        {
            SalesDate = dateTime;
            ReferencePerson = reference;
            SalesPerson = salesPerson;
            Good = good;
            Total = total;
        }

        public override string ToString()
        {
            return $"[{ID}]{SalesDate} - {Good.Name} @ {Total} by {SalesPerson.Name}";
        }
    }

    public class SalesMetadata : ICompositeIndexMetadata
    {
        public IEnumerable<CompositeIndex> Indexes
        {
            get
            {
                CompositeIndex index = new CompositeIndex(typeof(Sale), "Index1")
                {
                    nameof(Sale.SalesDate),
                    { "salesperson", SortDir.Desc }
                };
                yield return index;

                index = new CompositeIndex(typeof(Sale), "Index2")
                {
                    FailIfUnsupported = false
                };
                index.Add(SqlFunctionId.Upper, nameof(Sale.Note));
                yield return index;
            }
        }
    }

    public class GoodUpdateRecord : GenericEntityAccessorUpdateRecord
    {
        [UpdateRecordProperty(PropertyName = nameof(Good.Category))]
        public Category Category { get; set; }

        public GoodUpdateRecord() : base(typeof(Good))
        {
        }
    }

    public class GoodFilter : GenericEntityAccessorFilter
    {
        [FilterProperty(PropertyName = nameof(Good.Name), Operation = CmpOp.Eq)]
        public string NameIs { get; set; }

        [FilterProperty(PropertyName = nameof(Good.Name), Operation = CmpOp.Like)]
        public string NameStartsWith { get; set; }

        [FilterProperty(PropertyName = nameof(Good.Category), Operation = CmpOp.Eq)]
        public Category Category { get; set; }

        public GoodFilter() : base(typeof(Good))
        {
        }
    }

    public class SalesFilter : GenericEntityAccessorFilter
    {
        [FilterProperty(PropertyName = nameof(Sale.ReferencePerson), Operation = CmpOp.IsNull)]
        public bool? HasReferencePerson { get; set; }

        [FilterProperty(PropertyName = nameof(Sale.Good) + "." + nameof(Good.Name), Operation = CmpOp.Eq)]
        public string GoodName { get; set; }

        public SalesFilter() : base(typeof(Sale))
        {
        }
    }

    public class CustomSaleTargetClass
    {
        public int ID { get; set; }

        public DateTime SalesDate { get; set; }

        public int SaleDay { get; set; }

        public int SaleMonth { get; set; }

        public int SaleYear { get; set; }

        public DayOfWeek SaleDayOfWeek { get; set; }

        public string SalesPersonName { get; set; }

        public string GoodName { get; set; }

        public double Total { get; set; }

        public double AdjustedTotal { get; set; }
    }

    public class SumOfSales
    {
        public int ID { get; set; }
        public double Total { get; set; }
        public bool Checked { get; set; }
    }

    public class SumOfSalesCollection : IEnumerable<SumOfSales>
    {
        private readonly Dictionary<int, SumOfSales> mDictionary = new Dictionary<int, SumOfSales>();

        public SumOfSales this[int id]
        {
            get
            {
                if (mDictionary.TryGetValue(id, out SumOfSales s))
                    return s;
                s = new SumOfSales() { ID = id, Checked = false, Total = 0 };
                mDictionary[id] = s;
                return s;
            }
        }

        public IEnumerator<SumOfSales> GetEnumerator()
        {
            return mDictionary.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [Entity(Table = "tdefault")]
    public class TestDefaults
    {
        [EntityProperty(AutoId = true)]
        public int ID { get; set; }

        [EntityProperty(Field = "dv", DbType = DbType.String, Size = 128, Sorted = true)]
        public string DVal { get; set; }

        [EntityProperty(DbType = DbType.String, Size = 128, Sorted = true, DefaultValue = "default")]
        public string StringVal { get; set; }

        [EntityProperty(DefaultValue = true)]
        public bool BoolVal { get; set; }

        [EntityProperty(DefaultValue = 123)]
        public int IntVal { get; set; }
    }

    [Entity(Table = "tcallback")]
    public class SerializationCallback : IEntitySerializationCallback
    {
        private static readonly string[] names = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };

        private static string ToName(int? value)
        {
            if (value == null || value < 0 || value >= names.Length)
                return null;
            return names[(int)value];
        }

        private static int? ToValue(string name)
        {
            if (name == null)
                return null;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                    return i;
            }
            return null;
        }

        [EntityProperty(AutoId = true)]
        public int ID { get; set; }

        [EntityProperty(DbType = DbType.Int32, Nullable = true)]
        private int? IntVal { get; set; }

        public string StringValue { get; set; } = "zero";

        public void BeforeSerialization(SqlDbConnection connection)
        {
            IntVal = ToValue(StringValue);
        }

        public void AfterDeserealization(SelectEntitiesQueryBase query)
        {
            StringValue = ToName(IntVal);
        }
    }
}
