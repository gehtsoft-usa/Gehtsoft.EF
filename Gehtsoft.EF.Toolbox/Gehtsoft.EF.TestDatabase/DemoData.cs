using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.Tools2.Algorithm;
using Gehtsoft.Tools2.Extensions;

//disable static public field and static initialization warnings
#pragma warning disable S1104, S2386, S3963

namespace Gehtsoft.EF.TestDatabase
{
    public static class DemoData
    {
        public static readonly Employee[] Employees = new Employee[]
        {
            new Employee() {FirstName = "Paule", LastName = "Follitt", EmployedSince = new DateTime(2000, 2, 21), Active = true, EmployeeType = EmployeeType.Manager},
            new Employee() {FirstName = "Benedikt", LastName = "Von Der Empten", EmployedSince = new DateTime(2014, 3, 4), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Adele", LastName = "Bernhart", EmployedSince = new DateTime(2009, 7, 8), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Kerry", LastName = "Shoebottom", EmployedSince = new DateTime(2007, 10, 4), Active = true, EmployeeType = EmployeeType.Manager},
            new Employee() {FirstName = "Sarita", LastName = "Coulman", EmployedSince = new DateTime(2003, 10, 29), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Pamela", LastName = "Morit", EmployedSince = new DateTime(2015, 1, 22), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Fayina", LastName = "Orbine", EmployedSince = new DateTime(2006, 12, 2), Active = true, EmployeeType = EmployeeType.Manager},
            new Employee() {FirstName = "Jamie", LastName = "Stanislaw", EmployedSince = new DateTime(2001, 1, 31), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Swen", LastName = "McTurk", EmployedSince = new DateTime(2000, 1, 12), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Bobby", LastName = "Braker", EmployedSince = new DateTime(2002, 9, 24), Active = true, EmployeeType = EmployeeType.Manager},
            new Employee() {FirstName = "Mace", LastName = "Naire", EmployedSince = new DateTime(2009, 6, 18), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Jeno", LastName = "Coverlyn", EmployedSince = new DateTime(2001, 7, 16), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Von", LastName = "Rosenvasser", EmployedSince = new DateTime(2011, 9, 19), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Libbi", LastName = "Caw", EmployedSince = new DateTime(2014, 8, 26), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Perceval", LastName = "Towne", EmployedSince = new DateTime(2004, 12, 17), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Forester", LastName = "Voules", EmployedSince = new DateTime(2011, 3, 25), Active = false, EmployeeType = EmployeeType.Manager},
            new Employee() {FirstName = "Bennett", LastName = "Hatry", EmployedSince = new DateTime(2013, 9, 27), Active = true, EmployeeType = EmployeeType.Manager},
            new Employee() {FirstName = "Ardelia", LastName = "Bunning", EmployedSince = new DateTime(2016, 2, 9), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Jarib", LastName = "Duckerin", EmployedSince = new DateTime(2014, 7, 15), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Seana", LastName = "Auchinleck", EmployedSince = new DateTime(2013, 12, 15), Active = false, EmployeeType = EmployeeType.Manager},
            new Employee() {FirstName = "Susanetta", LastName = "Lundy", EmployedSince = new DateTime(2016, 2, 18), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Selestina", LastName = "Brimley", EmployedSince = new DateTime(2014, 1, 26), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Ofelia", LastName = "Froggatt", EmployedSince = new DateTime(2014, 7, 7), Active = false, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Dana", LastName = "Friday", EmployedSince = new DateTime(2011, 6, 2), Active = true, EmployeeType = EmployeeType.Salesman},
            new Employee() {FirstName = "Darnell", LastName = "Lithgow", EmployedSince = new DateTime(2015, 3, 15), Active = true, EmployeeType = EmployeeType.Salesman},
        };

        public static readonly Category CategoryGrocery = new Category() { Name = "Grocery" };
        public static readonly Category CategoryCar = new Category() { Name = "Car" };

        public static readonly Good[] Goods = new Good[]
        {
            new Good() {Name = "Cookie Double Choco", Category = CategoryGrocery},
            new Good() {Name = "Cake Circle, Paprus", Category = CategoryGrocery},
            new Good() {Name = "Pork - Loin, Bone - In", Category = CategoryGrocery},
            new Good() {Name = "Wine - Kwv Chenin Blanc South", Category = CategoryGrocery},
            new Good() {Name = "Butter - Pod", Category = CategoryGrocery},
            new Good() {Name = "Pernod", Category = CategoryGrocery},
            new Good() {Name = "Bread - Focaccia Quarter", Category = CategoryGrocery},
            new Good() {Name = "Sprouts - Corn", Category = CategoryGrocery},
            new Good() {Name = "Sage - Ground", Category = CategoryGrocery},
            new Good() {Name = "Propel Sport Drink", Category = CategoryGrocery},
            new Good() {Name = "Veal - Chops, Split, Frenched", Category = CategoryGrocery},
            new Good() {Name = "Container - Foam Dixie 12 Oz", Category = CategoryGrocery},
            new Good() {Name = "Bread Roll Foccacia", Category = CategoryGrocery},
            new Good() {Name = "Wine - Casablanca Valley", Category = CategoryGrocery},
            new Good() {Name = "Pork - Bacon,back Peameal", Category = CategoryGrocery},
            new Good() {Name = "Sproutsmustard Cress", Category = CategoryGrocery},
            new Good() {Name = "Compound - Mocha", Category = CategoryGrocery},
            new Good() {Name = "Energy Drink Red Bull", Category = CategoryGrocery},
            new Good() {Name = "Cheese - Fontina", Category = CategoryGrocery},
            new Good() {Name = "Clam - Cherrystone", Category = CategoryGrocery},
            new Good() {Name = "Tamarillo", Category = CategoryGrocery},
            new Good() {Name = "Yoplait Drink", Category = CategoryGrocery},
            new Good() {Name = "Cleaner - Bleach", Category = CategoryGrocery},
            new Good() {Name = "Sausage - Chorizo", Category = CategoryGrocery},
            new Good() {Name = "Chestnuts - Whole,canned", Category = CategoryGrocery},
            new Good() {Name = "Appetizer - Mushroom Tart", Category = CategoryGrocery},
            new Good() {Name = "Bread - Crusty Italian Poly", Category = CategoryGrocery},
            new Good() {Name = "Glycerine", Category = CategoryGrocery},
            new Good() {Name = "Scallops - U - 10", Category = CategoryGrocery},
            new Good() {Name = "Wine - Magnotta - Bel Paese White", Category = CategoryGrocery},
            new Good() {Name = "Nut - Hazelnut, Ground, Natural", Category = CategoryGrocery},
            new Good() {Name = "Cheese - Parmigiano Reggiano", Category = CategoryGrocery},
            new Good() {Name = "Beef - Tongue, Cooked", Category = CategoryGrocery},
            new Good() {Name = "Rice - Long Grain", Category = CategoryGrocery},
            new Good() {Name = "Eel Fresh", Category = CategoryGrocery},
            new Good() {Name = "Shrimp - Black Tiger 6 - 8", Category = CategoryGrocery},
            new Good() {Name = "Butter - Salted", Category = CategoryGrocery},
            new Good() {Name = "Pork - Ham, Virginia", Category = CategoryGrocery},
            new Good() {Name = "Glaze - Clear", Category = CategoryGrocery},
            new Good() {Name = "Table Cloth 120 Round White", Category = CategoryGrocery},
            new Good() {Name = "Yogurt - Plain", Category = CategoryGrocery},
            new Good() {Name = "Broom And Broom Rack White", Category = CategoryGrocery},
            new Good() {Name = "Towel Multifold", Category = CategoryGrocery},
            new Good() {Name = "Kiwi", Category = CategoryGrocery},
            new Good() {Name = "Maintenance Removal Charge", Category = CategoryGrocery},
            new Good() {Name = "Muffin Carrot - Individual", Category = CategoryGrocery},
            new Good() {Name = "Wine - Jafflin Bourgongone", Category = CategoryGrocery},
            new Good() {Name = "Parsley - Dried", Category = CategoryGrocery},
            new Good() {Name = "Catfish - Fillets", Category = CategoryGrocery},
            new Good() {Name = "Vinegar - White Wine", Category = CategoryGrocery},
            new Good() {Name = "Corn Shoots", Category = CategoryGrocery},
            new Good() {Name = "Country Roll", Category = CategoryGrocery},
            new Good() {Name = "Table Cloth 62x114 White", Category = CategoryGrocery},
            new Good() {Name = "Glass - Wine, Plastic, Clear 5 Oz", Category = CategoryGrocery},
            new Good() {Name = "Wine - Magnotta - Red, Baco", Category = CategoryGrocery},
            new Good() {Name = "Chocolate - Sugar Free Semi Choc", Category = CategoryGrocery},
            new Good() {Name = "Danishes - Mini Cheese", Category = CategoryGrocery},
            new Good() {Name = "Flour - Corn, Fine", Category = CategoryGrocery},
            new Good() {Name = "Oil - Truffle, Black", Category = CategoryGrocery},
            new Good() {Name = "Boogies", Category = CategoryGrocery},
            new Good() {Name = "Onions - Vidalia", Category = CategoryGrocery},
            new Good() {Name = "Tea - Apple Green Tea", Category = CategoryGrocery},
            new Good() {Name = "Oil - Olive, Extra Virgin", Category = CategoryGrocery},
            new Good() {Name = "Arctic Char - Fresh, Whole", Category = CategoryGrocery},
            new Good() {Name = "Bacardi Breezer - Tropical", Category = CategoryGrocery},
            new Good() {Name = "Potatoes - Yukon Gold, 80 Ct", Category = CategoryGrocery},
            new Good() {Name = "Wine - Red, Lurton Merlot De", Category = CategoryGrocery},
            new Good() {Name = "Glucose", Category = CategoryGrocery},
            new Good() {Name = "Beer - True North Lager", Category = CategoryGrocery},
            new Good() {Name = "Rye Special Old", Category = CategoryGrocery},
            new Good() {Name = "Wine - Masi Valpolocell", Category = CategoryGrocery},
            new Good() {Name = "Wine - Magnotta, Merlot Sr Vqa", Category = CategoryGrocery},
            new Good() {Name = "Cheese - Pont Couvert", Category = CategoryGrocery},
            new Good() {Name = "Thermometer Digital", Category = CategoryGrocery},
            new Good() {Name = "Wine - Magnotta - Cab Franc", Category = CategoryGrocery},
            new Good() {Name = "Rice - Wild", Category = CategoryGrocery},
            new Good() {Name = "Milk - Skim", Category = CategoryGrocery},
            new Good() {Name = "Stock - Fish", Category = CategoryGrocery},
            new Good() {Name = "Steampan - Foil", Category = CategoryGrocery},
            new Good() {Name = "Bagel - Everything", Category = CategoryGrocery},
            new Good() {Name = "Jameson - Irish Whiskey", Category = CategoryGrocery},
            new Good() {Name = "Wine - George Duboeuf Rose", Category = CategoryGrocery},
            new Good() {Name = "Soup - Campbells, Lentil", Category = CategoryGrocery},
            new Good() {Name = "Pepper - Scotch Bonnet", Category = CategoryGrocery},
            new Good() {Name = "Wine - Casillero Del Diablo", Category = CategoryGrocery},
            new Good() {Name = "Scallops - Live In Shell", Category = CategoryGrocery},
            new Good() {Name = "Sauerkraut", Category = CategoryGrocery},
            new Good() {Name = "Pineapple - Canned, Rings", Category = CategoryGrocery},
            new Good() {Name = "Wine - White, Chardonnay", Category = CategoryGrocery},
            new Good() {Name = "Wine - Guy Sage Touraine", Category = CategoryGrocery},
            new Good() {Name = "Wine - Riesling Alsace Ac 2001", Category = CategoryGrocery},
            new Good() {Name = "Potatoes - Yukon Gold 5 Oz", Category = CategoryGrocery},
            new Good() {Name = "Remy Red Berry Infusion", Category = CategoryGrocery},
            new Good() {Name = "Wine - Placido Pinot Grigo", Category = CategoryGrocery},
            new Good() {Name = "Quail - Jumbo", Category = CategoryGrocery},
            new Good() {Name = "Bread - Corn Muffaletta", Category = CategoryGrocery},
            new Good() {Name = "Spoon - Soup, Plastic", Category = CategoryGrocery},
            new Good() {Name = "Bread - Pumpernickle, Rounds", Category = CategoryGrocery},
            new Good() {Name = "Beef - Bones, Marrow", Category = CategoryGrocery},
            new Good() {Name = "Rabbit - Saddles", Category = CategoryGrocery},

            new Good() {Name = "Saab 9-5", Category = CategoryCar},
            new Good() {Name = "Volkswagen Passat", Category = CategoryCar},
            new Good() {Name = "Subaru XT", Category = CategoryCar},
            new Good() {Name = "BMW Z8", Category = CategoryCar},
            new Good() {Name = "GMC Vandura G3500", Category = CategoryCar},
            new Good() {Name = "Mercury Cougar", Category = CategoryCar},
            new Good() {Name = "Kia Optima", Category = CategoryCar},
            new Good() {Name = "Mazda B-Series", Category = CategoryCar},
            new Good() {Name = "Ford Freestar", Category = CategoryCar},
            new Good() {Name = "Toyota MR2", Category = CategoryCar},
            new Good() {Name = "Bentley Continental", Category = CategoryCar},
            new Good() {Name = "Chevrolet 3500", Category = CategoryCar},
            new Good() {Name = "Dodge Stealth", Category = CategoryCar},
            new Good() {Name = "Toyota Tacoma", Category = CategoryCar},
            new Good() {Name = "Infiniti G35", Category = CategoryCar},
            new Good() {Name = "Ford Mustang", Category = CategoryCar},
            new Good() {Name = "Mercedes-Benz SLK-Class", Category = CategoryCar},
            new Good() {Name = "Dodge Grand Caravan", Category = CategoryCar},
            new Good() {Name = "Toyota TundraMax", Category = CategoryCar},
            new Good() {Name = "Mazda RX-7", Category = CategoryCar},
            new Good() {Name = "Bentley Continental GT", Category = CategoryCar},
            new Good() {Name = "Chrysler PT Cruiser", Category = CategoryCar},
            new Good() {Name = "Nissan Quest", Category = CategoryCar},
            new Good() {Name = "Ford F150", Category = CategoryCar},
            new Good() {Name = "Suzuki Equator", Category = CategoryCar},
            new Good() {Name = "Mitsubishi Galant", Category = CategoryCar},
            new Good() {Name = "Toyota Celica", Category = CategoryCar},
            new Good() {Name = "Toyota Venza", Category = CategoryCar},
            new Good() {Name = "Buick Riviera", Category = CategoryCar},
            new Good() {Name = "Acura TL", Category = CategoryCar},
        };

        private static readonly List<Employee> activeSalesmen;

        static DemoData()
        {
            Employee currentManager = null;
            Employee boss = null;

            activeSalesmen = new List<Employee>();

            foreach (Employee emp in Employees)
            {
                if (boss == null)
                {
                    boss = emp;
                    currentManager = emp;
                    continue;
                }

                if (emp.EmployeeType == EmployeeType.Manager)
                {
                    emp.Manager = boss;
                    if (emp.Active)
                        currentManager = emp;
                }
                else
                {
                    emp.Manager = currentManager;
                    if (emp.Active)
                        activeSalesmen.Add(emp);
                }
            }
        }

        public static readonly Type[] AllTypes = new Type[] { typeof(Employee), typeof(Category), typeof(Good), typeof(Sale) };

        public static void CreateTables(SqlDbConnection connection)
        {
            DropTables(connection);
            foreach (Type type in AllTypes)
                using (EntityQuery query = connection.GetCreateEntityQuery(type))
                    query.Execute();
        }

        public static void DropTables(SqlDbConnection connection)
        {
            foreach (Type type in AllTypes.Reverse())
                using (EntityQuery query = connection.GetDropEntityQuery(type))
                    query.Execute();
        }

        public static Sale[] CreateData(SqlDbConnection connection, int approxSalesToCreate = 10000, DateTime? startSalesDate = null, int salesDayCount = 720)
        {
            if (startSalesDate == null)
                startSalesDate = DateTime.Now.AddDays(-salesDayCount).TruncateTime();

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Employee>())
            {
                foreach (Employee employee in Employees)
                {
                    query.Execute(employee);
                }
            }

            using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Category>())
            {
                query.Execute(CategoryGrocery);
                query.Execute(CategoryCar);
            }

            using (SqlDbTransaction transaction = connection.BeginTransaction())
            {
                using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Good>())
                {
                    foreach (Good good in Goods)
                        query.Execute(good);
                }

                transaction.Commit();
            }

            Random r = new Random();
            List<Sale> sales = new List<Sale>();

            using (SqlDbTransaction transaction = connection.BeginTransaction())
            {
                using (ModifyEntityQuery query = connection.GetInsertEntityQuery<Sale>())
                {
                    double avgSales = (double)approxSalesToCreate / (double)salesDayCount;
                    int variation = (int)(avgSales / 5 + 2) / 2;
                    DateTime saleDate = (DateTime)startSalesDate;
                    for (int i = 0; i < salesDayCount; i++, saleDate = saleDate.AddDays(1))
                    {
                        int daySales = (int)avgSales + (variation - r.Next(variation * 2));
                        for (int j = 0; j < daySales; j++)
                        {
                            Sale sale = new Sale
                            {
                                SaleDate = saleDate
                            };
                            Employee salesmen = null;
                            int attempt = 50;
                            while (true)
                            {
                                salesmen = r.Next(activeSalesmen);
                                if (salesmen.EmployedSince < saleDate || attempt <= 0)
                                    break;
                                attempt--;
                            }

                            sale.SoldBy = salesmen;
                            sale.Good = r.Next(Goods);
                            if (sale.Good.Category.ID == CategoryGrocery.ID)
                            {
                                sale.Amount = Math.Round(r.NextDouble(0.01, 100), 2);
                                if (r.IsChance(0.1))
                                    sale.SalesTax = null;
                                else
                                    sale.SalesTax = Math.Round(sale.Amount * 0.0475, 2);
                            }
                            else
                            {
                                sale.Amount = Math.Round(r.NextDouble(5000, 30000), 2);
                                sale.SalesTax = Math.Round(sale.Amount * 0.06, 2);
                                int invoiceSize = r.Next(50, 5000);
                                byte[] invoice = new byte[invoiceSize + 4];
                                r.NextBytes(invoice);
                                Crc32 crc32 = new Crc32();
                                byte[] hash = crc32.ComputeHash(invoice, 0, invoiceSize);
                                invoice[invoiceSize - 4] = hash[0];
                                invoice[invoiceSize - 3] = hash[1];
                                invoice[invoiceSize - 2] = hash[2];
                                invoice[invoiceSize - 1] = hash[3];
                                sale.Invoice = invoice;
                            }
                            query.Execute(sale);
                            sales.Add(sale);
                        }
                    }
                }

                transaction.Commit();
            }

            return sales.ToArray();
        }
    }
}
