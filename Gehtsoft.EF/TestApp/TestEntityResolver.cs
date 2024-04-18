using System;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;
using NUnit.Framework;
using FluentAssertions;
using NUnit.Framework.Legacy;

namespace TestApp
{
    [TestFixture]
    internal class TestEntityResolver
    {
        public class Entity<T>
        {
            public Type ContanierFor => typeof(T);
        }

        #region entities
        [Flags]
        public enum UserRoles
        {
            None = 0,
            Administrator = 0x1,
            DeliveryAdministrator = 0x2,
            DeliveryOperator = 0x4,
            DryPlantAdministrator = 0x8,
            DryPlantOperator = 0x10,
            MillAdministrator = 0x20,
            MillOperator = 0x40,
            OrchardAdministrator = 0x80,
            OrchardOperator = 0x100,
            ShippingAdminsitrator = 0x200,
            ShippingOperator = 0x400,
            LabAdminisrator = 0x800,
            LabOperator = 0x1000,
            HarvestAdministrator = 0x2000,
            HarvestOperator = 0x4000,
        }

        public enum UserStatus
        {
            None = 0x0,
            WaitConfirmation = 0x1,
            Suspended = 0x2,
            Active = 0x3,
        }

        [Entity(Scope = "mzc", Table = "users")]
        public class User : Entity<User>
        {
            public bool New { get; internal set; }

            [EntityProperty(Field = "username", DbType = DbType.String, Size = 32, PrimaryKey = true)]
            public string Name { get; set; }

            [EntityProperty(Field = "email", DbType = DbType.String, Size = 128)]
            public string Email { get; set; }

            [EntityProperty(Field = "passwordhash", DbType = DbType.String, Size = 128)]
            public string PasswordHash { get; set; }

            [EntityProperty(Field = "roles", DbType = DbType.Int32)]
            public UserRoles Roles { get; set; }

            [EntityProperty(Field = "status", DbType = DbType.Int32)]
            public UserStatus Status { get; set; }

            public User()
            {
                New = true;
                Status = UserStatus.None;
            }
            public User(string name)
            {
                New = false;
                Name = name;
                Status = UserStatus.None;
            }

            public User(string name, string passwordHash, UserRoles roles, string email, UserStatus status)
            {
                New = false;
                Name = name;
                PasswordHash = passwordHash;
                Roles = roles;
                Email = email;
                Status = status;
            }

            public User Clone()
            {
                return new User(Name, PasswordHash, Roles, Email, Status);
            }

            public bool Is(UserRoles role)
            {
                return (Roles & role) == role;
            }
        }

        [Entity(Scope = "mzc", Table = "harvestperiods")]
        public class HarvestPeriod : Entity<HarvestPeriod>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32)]
            public string Name { get; set; }

            private DateTime mStart;

            [EntityProperty(Field = "periodstart", DbType = DbType.Date, Sorted = true)]
            public DateTime Start
            {
                get { return mStart; }
                set { mStart = new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Unspecified); }
            }

            private DateTime mEnd;

            [EntityProperty(Field = "periodend", DbType = DbType.Date, Nullable = true)]
            public DateTime End
            {
                get { return mEnd; }
                set { mEnd = new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Unspecified); }
            }

            public HarvestPeriod()
            {
                ID = -1;
            }

            public HarvestPeriod(int id, string name, DateTime start, DateTime end)
            {
                ID = id;
                Name = name;
                Start = start;
                End = end;
            }

            public bool IsOpen => End.Ticks == 0;

            public bool IsInPeriod(DateTime datetime)
            {
                if (IsOpen)
                    return mStart <= datetime;
                else
                    return mStart <= datetime && mEnd > datetime;
            }
        }

        public enum BagType
        {
            /** BB */
            BurlapBag = 0,
            /** SS */
            SuperSack = 1,
            /** MC - essentially - returned back due to quality issues*/
            MetalCrate = 2,
        }

        [Entity(Scope = "mzc", Table = "bins")]
        public class Bin
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "process", ForeignKey = true)]
            public MillProcess Process { get; set; }

            /** Must be in form Process.NoXX, e.g. bins for process 10 will be 1001, 1002, 1003 and so on */

            [EntityProperty(Field = "binno", DbType = DbType.Int32, Sorted = true)]
            public int BinNo { get; set; }

            /** May and will be different from process date/time. */

            [EntityProperty(Field = "processed", DbType = DbType.DateTime, Sorted = true)]
            public DateTime ProcessedDate { get; set; }

            [EntityProperty(Field = "processedweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double ProcessedWeight { get; set; }

            [EntityProperty(Field = "reprocessedweight", DbType = DbType.Double, Size = 10, Precision = 2, Nullable = true)]
            public double? RerocessedWeight { get; set; }

            [EntityProperty(Field = "processoperator", DbType = DbType.String, Size = 8, Nullable = true)]
            public string ProcessOperator { get; set; }

            [EntityProperty(Field = "gradeclass", ForeignKey = true, Nullable = true)]
            public GradeClass GradeClass { get; set; }

            [EntityProperty(Field = "millgrade", ForeignKey = true, Nullable = true)]
            public StateGrade MillGrade { get; set; }

            [EntityProperty(Field = "labsample", DbType = DbType.DateTime, Nullable = true, Sorted = true)]
            public DateTime? LabSampleDate { get; set; }

            [EntityProperty(Field = "labgrade", ForeignKey = true, Nullable = true)]
            public StateGrade LabGrade { get; set; }

            /** in percents 0..100 with two-digit precision */
            [EntityProperty(Field = "defects", DbType = DbType.Double, Size = 5, Precision = 2, Nullable = true)]
            public double? Defects { get; set; }

            [EntityProperty(Field = "bagged", DbType = DbType.DateTime, Nullable = true, Sorted = true)]
            public DateTime? BaggedDate { get; set; }

            [EntityProperty(Field = "baggedgrade", ForeignKey = true, Nullable = true)]
            public StateGrade BaggedGrade { get; set; }

            [EntityProperty(Field = "baggedoperator", DbType = DbType.String, Size = 8, Nullable = true)]
            public string BaggedOperator { get; set; }

            [EntityProperty(Field = "rebagged", DbType = DbType.DateTime, Nullable = true, Sorted = true)]
            public DateTime? ReBaggedDate { get; set; }

            [EntityProperty(Field = "rebaggedgrade", ForeignKey = true, Nullable = true)]
            public StateGrade ReBaggedGrade { get; set; }

            [EntityProperty(Field = "rebaggedoperator", DbType = DbType.String, Size = 8, Nullable = true)]
            public string ReBaggedOperator { get; set; }

            [EntityProperty(Field = "baggedas", DbType = DbType.Int32, Nullable = true, Sorted = true)]
            public BagType? BagType { get; set; }

            [EntityProperty(Field = "gradingcomment", DbType = DbType.String, Size = 64, Nullable = true)]
            public string Comment { get; set; }

            [EntityProperty(Field = "location", ForeignKey = true, Nullable = true)]
            public Location Location { get; set; }

            public Bin(int id)
            {
                ID = id;
            }

            public Bin() { }
        }

        [Entity(Scope = "mzc", Table = "dryerbatches")]
        public class DryerBatch : Entity<DryerBatch>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "batch", DbType = DbType.Int32, Sorted = true)]
            public int Batch { get; set; }

            [EntityProperty(Field = "dryer", DbType = DbType.Int32)]
            public int Dryer { get; set; }

            [EntityProperty(Field = "loaded", DbType = DbType.DateTime, Sorted = true)]
            public DateTime Loaded { get; set; }

            [EntityProperty(Field = "crewloaded", DbType = DbType.Int32)]
            public int CrewLoaded { get; set; }

            [EntityProperty(Field = "shiftloaded", DbType = DbType.Int32)]
            public int ShiftLoaded { get; set; }

            [EntityProperty(Field = "unloaded", DbType = DbType.DateTime, Sorted = true, Nullable = true)]
            public DateTime? Unloaded { get; set; }

            [EntityProperty(Field = "beantype", DbType = DbType.Int32, Sorted = true)]
            public BeanType BeanType { get; set; }

            [EntityProperty(Field = "bins", DbType = DbType.Int32, Nullable = true)]
            public int? Bins { get; set; }

            [EntityProperty(Field = "moisture", DbType = DbType.Double, Size = 8, Precision = 2, Nullable = true)]
            public double? Moisture { get; set; }

            [EntityProperty(Field = "density", DbType = DbType.Double, Size = 8, Precision = 2, Nullable = true)]
            public double? Density { get; set; }

            [EntityProperty(Field = "temperature", DbType = DbType.Double, Size = 8, Precision = 2, Nullable = true)]
            public double? Temperature { get; set; }

            [EntityProperty(Field = "field1", ForeignKey = true)]
            public Field Field1 { get; set; }

            [EntityProperty(Field = "variety1", ForeignKey = true)]
            public Variety Variety1 { get; set; }

            [EntityProperty(Field = "field2", ForeignKey = true, Nullable = true)]
            public Field Field2 { get; set; }

            [EntityProperty(Field = "variety2", ForeignKey = true, Nullable = true)]
            public Variety Variety2 { get; set; }

            [EntityProperty(Field = "crewunloaded", DbType = DbType.Int32, Nullable = true)]
            public int? CrewUnloaded { get; set; }

            [EntityProperty(Field = "shiftunloaded", DbType = DbType.Int32, Nullable = true)]
            public int? ShiftUnloaded { get; set; }

            public DryerBatch()
            {
                ID = -1;
            }

            public DryerBatch(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "gradeclasses")]
        public class GradeClass
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64)]
            public string Name { get; set; }

            [EntityProperty(Field = "sortorder", DbType = DbType.String, Size = 4, Sorted = true)]
            public string SortOrder { get; set; }

            public GradeClass()
            {
            }

            public GradeClass(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "locations")]
        public class Location
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32)]
            public string Name { get; set; }

            public Location(int id)
            {
                ID = id;
            }

            public Location() { }
        }

        public enum MetalCrateStatus
        {
            Inventory,
            Process,
            Shipped,
            Trash,
        }

        [Entity(Scope = "mzc", Table = "metalcrates", View = true)]
        public class MetalCrateBase
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "tag", DbType = DbType.String, Size = 16, Sorted = true)]
            public string Tag { get; set; }
        }

        [Entity(Scope = "mzc", Table = "metalcrates")]
        public class MetalCrate : MetalCrateBase
        {
            [EntityProperty(Field = "crate", DbType = DbType.Int32, Sorted = true)]
            public int CrateNumber { get; set; }

            [EntityProperty(Field = "batch", ForeignKey = true)]
            public DryerBatch Batch { get; set; }

            [EntityProperty(Field = "weight", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double Weight { get; set; }

            [EntityProperty(Field = "row", DbType = DbType.Int32, Sorted = true)]
            public int Row { get; set; }

            [EntityProperty(Field = "spot", DbType = DbType.Int32, Sorted = true)]
            public int Spot { get; set; }

            [EntityProperty(Field = "elevation", DbType = DbType.Int32, Sorted = true)]
            public int Elevation { get; set; }

            [EntityProperty(Field = "status", DbType = DbType.Int32, Sorted = true)]
            public MetalCrateStatus Status { get; set; }

            [EntityProperty(Field = "comment", DbType = DbType.String, Size = 32, Nullable = true)]
            public string Comment { get; set; }

            public MetalCrate(int id)
            {
                ID = id;
            }

            public MetalCrate()
            {
                Status = MetalCrateStatus.Inventory;
            }
        }
        [Entity(Scope = "mzc", Table = "mills")]
        public class Mill
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32)]
            public string Name { get; set; }

            public Mill(int id)
            {
                ID = id;
            }

            public Mill() { }
        }
        [Entity(Scope = "mzc", Table = "millprocesses")]
        public class MillProcess
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "mill", ForeignKey = true)]
            public Mill Mill { get; set; }

            [EntityProperty(Field = "process", DbType = DbType.Int32, Sorted = true)]
            public int ProcessNo { get; set; }

            [EntityProperty(Field = "type", ForeignKey = true)]
            public ProductionBeanType BeanType { get; set; }

            [EntityProperty(Field = "started", DbType = DbType.Date, Sorted = true)]
            public DateTime Started { get; set; }

            public MillProcess(int id)
            {
                ID = id;
            }

            public MillProcess() { }
        }
        [Entity(Scope = "mzc", Table = "millprocessinputs")]
        public class MillProcessInput
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "process", ForeignKey = true)]
            public MillProcess Process { get; set; }

            private MetalCrate mCrate;

            [EntityProperty(Field = "mc", ForeignKey = true)]
            public MetalCrate Crate
            {
                get { return mCrate; }
                set
                {
                    if ((value == null && mCrate != null) ||
                        (mCrate == null && value != null) ||
                        (mCrate?.ID != value?.ID))
                        CrateChanged = true;
                    mCrate = value;
                }
            }

            [EntityProperty(Field = "weight", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double Weight { get; set; }

            [EntityProperty(Field = "moisture", DbType = DbType.Double, Size = 8, Precision = 2)]
            public double Moisture { get; set; }
            [EntityProperty(Field = "density", DbType = DbType.Double, Size = 8, Precision = 2)]
            public double Density { get; set; }

            [EntityProperty(Field = "temperature", DbType = DbType.Double, Size = 8, Precision = 2)]
            public double Temperature { get; set; }

            internal bool CrateChanged { get; set; }
            internal MetalCrate OriginalMetalCrate { get; set; }

            public MillProcessInput(int id)
            {
                ID = id;
            }

            public MillProcessInput() { }
        }
        [Entity(Scope = "mzc", Table = "beantype")]
        public class ProductionBeanType
        {
            public bool IsNew { get; internal set; }

            [EntityProperty(Field = "type", DbType = DbType.Int32, PrimaryKey = true)]
            public int TypeID { get; set; }

            [EntityProperty(Field = "descriptor", DbType = DbType.String, Size = 64)]
            public string Descriptor { get; set; }

            [EntityProperty(Field = "shortdescriptor", DbType = DbType.String, Size = 16)]
            public string ShortDescriptor { get; set; }

            [EntityProperty(Field = "content", DbType = DbType.String, Size = 64)]
            public string Content { get; set; }

            [EntityProperty(Field = "sorter", DbType = DbType.String, Size = 64, Sorted = true)]
            public string Sorter { get; set; }

            [EntityProperty(Field = "iid", DbType = DbType.String, Size = 1, Sorted = true)]
            public string InternalID { get; set; }

            [EntityProperty(Field = "sortorder", DbType = DbType.Int32, Sorted = true)]
            public int SortOrder { get; set; }

            [EntityProperty(Field = "basetype", DbType = DbType.Int32, Sorted = true)]
            public BeanType BeanType { get; set; }

            public ProductionBeanType()
            {
                IsNew = true;
            }
            public ProductionBeanType(int type)
            {
                TypeID = type;
            }
        }
        public enum BeanSizeType
        {
            Regular = 1,
            Peaberry = 2,
        }

        [Entity(Scope = "mzc", Table = "stategrades")]
        public class StateGrade
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64)]
            public string Name { get; set; }

            [EntityProperty(Field = "sortorder", DbType = DbType.String, Size = 4, Sorted = true)]
            public string SortOrder { get; set; }

            [EntityProperty(Field = "beansize", DbType = DbType.Int32, Sorted = true)]
            public BeanSizeType BeanSize { get; set; }

            //optional information with grade description
            [EntityProperty(Field = "otballowed", DbType = DbType.String, Size = 64, Nullable = true)]
            public string OtbAllowed { get; set; }

            [EntityProperty(Field = "defallowed", DbType = DbType.String, Size = 64, Nullable = true)]
            public string DefAllowed { get; set; }

            [EntityProperty(Field = "moisture", DbType = DbType.String, Size = 64, Nullable = true)]
            public string Moisture { get; set; }

            [EntityProperty(Field = "screen", DbType = DbType.Int32, Nullable = true)]
            public int? Screen { get; set; }

            [EntityProperty(Field = "screentype", DbType = DbType.String, Size = 32, Nullable = true)]
            public string ScreenType { get; set; }

            public StateGrade()
            {
            }

            public StateGrade(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "cherydeliveries")]
        public class CherryDeliveryRecord : Entity<CherryDeliveryRecord>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "ticket", DbType = DbType.Int32, Sorted = true)]
            public int Ticket { get; set; }

            [EntityProperty(Field = "pass", DbType = DbType.Int32)]
            public int Pass { get; set; }

            private DateTime mShiftDate;

            [EntityProperty(Field = "shiftdate", DbType = DbType.Date)]
            public DateTime ShiftDate
            {
                get { return mShiftDate; }
                set
                {
                    if (value.Hour != 0 || value.Minute != 0 || value.Second != 0 || value.Millisecond != 0)
                        mShiftDate = new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    else
                        mShiftDate = value;
                }
            }

            [EntityProperty(Field = "unloaded", DbType = DbType.DateTime, Sorted = true)]
            public DateTime UnloadDateTime { get; set; }

            [EntityProperty(Field = "shift", DbType = DbType.Int32)]
            public int Shift { get; set; }

            [EntityProperty(Field = "fieldlot", ForeignKey = true)]
            public FieldLot FieldLot { get; set; }

            [EntityProperty(Field = "truck", ForeignKey = true)]
            public Truck Truck { get; set; }

            [EntityProperty(Field = "grossweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double TruckGrossWeight { get; set; }

            [EntityProperty(Field = "tareweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double TrucksTareWeight { get; set; }

            public double NetWeight => TruckGrossWeight - TrucksTareWeight;

            [EntityProperty(Field = "quotercupweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double QuoterCupWeight { get; set; }

            [EntityProperty(Field = "literweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double LiterWeight { get; set; }
            public double CherryDensity => QuoterCupWeight * 4;
            public double EstimatedVolume => CherryDensity == 0 ? 0 : NetWeight / CherryDensity;

            [EntityProperty(Field = "greencount", DbType = DbType.Int32)]
            internal int GreenCount
            {
                get { return LabData.Green.Count; }
                set { LabData.Green.Count = value; }
            }

            [EntityProperty(Field = "greenweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            internal double GreenWeight
            {
                get { return LabData.Green.Weight; }
                set { LabData.Green.Weight = value; }
            }

            [EntityProperty(Field = "ripecount", DbType = DbType.Int32)]
            internal int RipeCount
            {
                get { return LabData.Ripe.Count; }
                set { LabData.Ripe.Count = value; }
            }

            [EntityProperty(Field = "ripeweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            internal double RipeWeight
            {
                get { return LabData.Ripe.Weight; }
                set { LabData.Ripe.Weight = value; }
            }

            [EntityProperty(Field = "floatercount", DbType = DbType.Int32)]
            internal int FloaterCount
            {
                get { return LabData.Floater.Count; }
                set { LabData.Floater.Count = value; }
            }

            [EntityProperty(Field = "floaterweight", DbType = DbType.Double, Size = 10, Precision = 2)]
            internal double FloaterWeight
            {
                get { return LabData.Floater.Weight; }
                set { LabData.Floater.Weight = value; }
            }

            [EntityProperty(Field = "deceasedgreencount", DbType = DbType.Int32)]
            internal int DeceasedGreenCount
            {
                get { return DeceasedData.Green.Count; }
                set { DeceasedData.Green.Count = value; }
            }

            [EntityProperty(Field = "deceasedgreenweight", DbType = DbType.Double, Size = 10, Precision = 3)]
            internal double DeceasedGreenWeight
            {
                get { return DeceasedData.Green.Weight; }
                set { DeceasedData.Green.Weight = value; }
            }

            [EntityProperty(Field = "deceasedripecount", DbType = DbType.Int32)]
            internal int DeceasedRipeCount
            {
                get { return DeceasedData.Ripe.Count; }
                set { DeceasedData.Ripe.Count = value; }
            }

            [EntityProperty(Field = "deceasedripeweight", DbType = DbType.Double, Size = 10, Precision = 3)]
            internal double DeceasedRipeWeight
            {
                get { return DeceasedData.Ripe.Weight; }
                set { DeceasedData.Ripe.Weight = value; }
            }

            [EntityProperty(Field = "deceasedfloatercount", DbType = DbType.Int32)]
            internal int DeceasedFloaterCount
            {
                get { return DeceasedData.Floater.Count; }
                set { DeceasedData.Floater.Count = value; }
            }

            [EntityProperty(Field = "deceasedfloaterweight", DbType = DbType.Double, Size = 10, Precision = 3)]
            internal double DeceasedFloaterWeight
            {
                get { return DeceasedData.Floater.Weight; }
                set { DeceasedData.Floater.Weight = value; }
            }

            public class LabStructureInfo : Entity<LabStructureInfo>
            {
                public class LabInfo : Entity<LabInfo>
                {
                    public int Count { get; set; }
                    public double Weight { get; set; }

                    public double Percent
                    {
                        get
                        {
                            int s = mStructure.Count;
                            if (s == 0)
                                return 0;
                            return Count * 100.0 / s;
                        }
                    }

                    private readonly LabStructureInfo mStructure;

                    internal LabInfo(LabStructureInfo structure)
                    {
                        mStructure = structure;
                    }
                }

                public LabInfo Green { get; }
                public LabInfo Ripe { get; }
                public LabInfo Floater { get; }

                internal LabStructureInfo()
                {
                    Green = new LabInfo(this);
                    Ripe = new LabInfo(this);
                    Floater = new LabInfo(this);
                }

                internal LabStructureInfo(LabStructureInfo other)
                {
                    Green = new LabInfo(other);
                    Ripe = new LabInfo(other);
                    Floater = new LabInfo(other);
                }

                public int Count => Green.Count + Ripe.Count + Floater.Count;
                public double Weight => Green.Weight + Ripe.Weight + Floater.Weight;
            }

            public LabStructureInfo LabData { get; }
            public LabStructureInfo DeceasedData { get; }

            public CherryDeliveryRecord()
            {
                ID = -1;
                LabData = new LabStructureInfo();
                DeceasedData = new LabStructureInfo(LabData);
            }
        }
        [Entity(Scope = "mzc", Table = "trucks")]
        public class Truck : Entity<Truck>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "tag", DbType = DbType.String, Size = 16)]
            public string Tag { get; set; }

            [EntityProperty(Field = "weight", DbType = DbType.Double, Size = 8, Precision = 0)]
            public double Weight { get; set; }

            public Truck()
            {
                ID = -1;
            }

            public Truck(int id)
            {
                ID = id;
            }

            public Truck(int id, string tag, double weight)
            {
                ID = id;
                Tag = tag;
                Weight = weight;
            }
        }
        [Entity(Scope = "mzc", Table = "batchcuppingdata")]
        public class BatchCuppingData
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            /** note: can be multiple records for the same batch with different people/dates. */
            [EntityProperty(Field = "batch", ForeignKey = true)]
            public DryerBatch Batch { get; set; }

            [EntityProperty(Field = "person", ForeignKey = true)]
            public CuppingPerson Person { get; set; }

            [EntityProperty(Field = "cupdate", DbType = DbType.Date, Sorted = true)]
            public DateTime CupDate { get; set; }

            [EntityProperty(Field = "fragrance", DbType = DbType.Int32)]
            public int Fragrance { get; set; }

            [EntityProperty(Field = "aroma", DbType = DbType.Int32)]
            public int Aroma { get; set; }

            [EntityProperty(Field = "body", DbType = DbType.Int32)]
            public int Body { get; set; }

            [EntityProperty(Field = "acidity", DbType = DbType.Int32)]
            public int Acidity { get; set; }

            [EntityProperty(Field = "aftertaste", DbType = DbType.Int32)]
            public int Aftertaste { get; set; }

            [EntityProperty(Field = "comment", DbType = DbType.String, Size = 64, Nullable = true)]
            public string Comment { get; set; }

            public BatchCuppingData(int id)
            {
                ID = id;
            }

            public BatchCuppingData() { }
        }
        [Entity(Scope = "mzc", Table = "bincuppingdata")]
        public class BinCuppingData
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            /** note: can be multiple records for the same batch with different people/dates. */
            [EntityProperty(Field = "bin", ForeignKey = true)]
            public Bin Bin { get; set; }

            [EntityProperty(Field = "person", ForeignKey = true)]
            public CuppingPerson Person { get; set; }

            [EntityProperty(Field = "cupdate", DbType = DbType.Date, Sorted = true)]
            public DateTime CupDate { get; set; }

            [EntityProperty(Field = "fragrance", DbType = DbType.Int32)]
            public int Fragrance { get; set; }

            [EntityProperty(Field = "aroma", DbType = DbType.Int32)]
            public int Aroma { get; set; }

            [EntityProperty(Field = "body", DbType = DbType.Int32)]
            public int Body { get; set; }

            [EntityProperty(Field = "acidity", DbType = DbType.Int32)]
            public int Acidity { get; set; }

            [EntityProperty(Field = "aftertaste", DbType = DbType.Int32)]
            public int Aftertaste { get; set; }

            [EntityProperty(Field = "comment", DbType = DbType.String, Size = 64, Nullable = true)]
            public string Comment { get; set; }

            public BinCuppingData(int id)
            {
                ID = id;
            }

            public BinCuppingData() { }
        }
        [Entity(Scope = "mzc", Table = "bindefectsdata")]
        public class BinDefectsData
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "bin", ForeignKey = true)]
            public Bin Bin { get; set; }

            [EntityProperty(Field = "inspector", ForeignKey = true)]
            public LabInspector Inspector { get; set; }

            [EntityProperty(Field = "inspectiondate", DbType = DbType.Date, Sorted = true)]
            public DateTime InspectionDate { get; set; }

            [EntityProperty(Field = "color", DbType = DbType.String, Size = 32, Nullable = true)]
            public string Color { get; set; }

            [EntityProperty(Field = "cleanliness", DbType = DbType.String, Size = 32, Nullable = true)]
            public string Cleanliness { get; set; }

            [EntityProperty(Field = "weight", DbType = DbType.Int32)]
            public int SampleWeight { get; set; }

            [EntityProperty(Field = "blackct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BlackCt { get; set; }

            [EntityProperty(Field = "blackwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BlackWt { get; set; }

            [EntityProperty(Field = "partlyblackct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyBlackCt { get; set; }

            [EntityProperty(Field = "partlyblackwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyBlackWt { get; set; }

            [EntityProperty(Field = "sourct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SourCt { get; set; }

            [EntityProperty(Field = "sourwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SourWt { get; set; }

            [EntityProperty(Field = "partlysourct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlySourCt { get; set; }

            [EntityProperty(Field = "partlysourwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlySourWt { get; set; }

            [EntityProperty(Field = "moldct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MoldCt { get; set; }

            [EntityProperty(Field = "moldwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MoldWt { get; set; }

            [EntityProperty(Field = "partlymoldct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyMoldCt { get; set; }

            [EntityProperty(Field = "partlymoldwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyMoldWt { get; set; }

            [EntityProperty(Field = "stickct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StickCt { get; set; }

            [EntityProperty(Field = "stickwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StickWt { get; set; }

            [EntityProperty(Field = "stonect", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StoneCt { get; set; }

            [EntityProperty(Field = "stonewt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StoneWt { get; set; }

            [EntityProperty(Field = "insectct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double InsectCt { get; set; }

            [EntityProperty(Field = "insectwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double InsectWt { get; set; }

            [EntityProperty(Field = "podcherryct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PodCherryCt { get; set; }

            [EntityProperty(Field = "podcherrywt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PodCherryWt { get; set; }

            [EntityProperty(Field = "huskhullct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double HuskHullCt { get; set; }

            [EntityProperty(Field = "huskhullwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double HuskHullWt { get; set; }

            [EntityProperty(Field = "motherelephantct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MotherElephantCt { get; set; }

            [EntityProperty(Field = "motherelephantwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MotherElephantWt { get; set; }

            [EntityProperty(Field = "brokencutnicksct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BrokenCutNicksCt { get; set; }

            [EntityProperty(Field = "brokencutnickswt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BrokenCutNicksWt { get; set; }

            [EntityProperty(Field = "ageddiscoloredct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double AgedDiscoloredCt { get; set; }

            [EntityProperty(Field = "ageddiscoloredwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double AgedDiscoloredWt { get; set; }

            [EntityProperty(Field = "shellct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double ShellCt { get; set; }

            [EntityProperty(Field = "shellwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double ShellWt { get; set; }

            [EntityProperty(Field = "miscct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MiscCt { get; set; }

            [EntityProperty(Field = "miscwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MiscWt { get; set; }

            [EntityProperty(Field = "quackerfloaterct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double QuackerFloaterCt { get; set; }

            [EntityProperty(Field = "quackerfloaterwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double QuackerFloaterWt { get; set; }

            [EntityProperty(Field = "dirtyct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double DirtyCt { get; set; }

            [EntityProperty(Field = "dirtywt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double DirtyWt { get; set; }

            [EntityProperty(Field = "silverskinct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SilverskinCt { get; set; }

            [EntityProperty(Field = "silverskinwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SilverskinWt { get; set; }

            [EntityProperty(Field = "othertypect", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double OtherTypeCt { get; set; }

            [EntityProperty(Field = "othertypewt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double OtherTypeWt { get; set; }

            [EntityProperty(Field = "comment", DbType = DbType.String, Size = 32, Nullable = true)]
            public string Comment { get; set; }

            public BinDefectsData()
            {
                ID = -1;
            }

            public BinDefectsData(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "binscientificdata")]
        public class BinScientificData
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "bin", ForeignKey = true)]
            public Bin Bin { get; set; }

            [EntityProperty(Field = "testdate", DbType = DbType.Date, Sorted = true)]
            public DateTime TestDate { get; set; }

            [EntityProperty(Field = "beantemp", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BeanTemp { get; set; }

            [EntityProperty(Field = "watertemp", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double WaterTemp { get; set; }

            [EntityProperty(Field = "waterph", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double WaterPh { get; set; }

            [EntityProperty(Field = "cupph", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double CupPh { get; set; }

            [EntityProperty(Field = "density", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Density { get; set; }

            [EntityProperty(Field = "ppmppt", DbType = DbType.Double, Size = 7, Precision = 0)]
            public double PpmPpt { get; set; }

            [EntityProperty(Field = "moisture", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Moisture { get; set; }

            [EntityProperty(Field = "agton", DbType = DbType.Double, Size = 7, Precision = 0)]
            public double Agton { get; set; }

            [EntityProperty(Field = "recup", DbType = DbType.Boolean)]
            public bool Recup { get; set; }

            public BinScientificData()
            {
                ID = -1;
            }

            public BinScientificData(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "binsizingdata")]
        public class BinSizingData
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "bin", ForeignKey = true)]
            public Bin Bin { get; set; }

            [EntityProperty(Field = "beansizetype", DbType = DbType.Int32, Sorted = true)]
            public BeanSizeType BeanType { get; set; }

            [EntityProperty(Field = "size20", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size20 { get; set; }

            [EntityProperty(Field = "size19", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size19 { get; set; }

            [EntityProperty(Field = "size18", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size18 { get; set; }

            [EntityProperty(Field = "size17", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size17 { get; set; }

            [EntityProperty(Field = "size16", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size16 { get; set; }

            [EntityProperty(Field = "size15", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size15 { get; set; }

            [EntityProperty(Field = "size14", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size14 { get; set; }

            [EntityProperty(Field = "size13", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size13 { get; set; }

            [EntityProperty(Field = "size12", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size12 { get; set; }

            [EntityProperty(Field = "size10", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size10 { get; set; }

            [EntityProperty(Field = "under14", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double UnderSize14 { get; set; }

            [EntityProperty(Field = "undersize", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Undersize { get; set; }

            public BinSizingData()
            {
                ID = -1;
            }

            public BinSizingData(int id)
            {
                ID = id;
            }
        }

        [Entity(Scope = "mzc", Table = "cuppingperson")]
        public class CuppingPerson
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64)]
            public string Name { get; set; }

            public CuppingPerson(int id)
            {
                ID = id;
            }

            public CuppingPerson() { }
        }

        [Entity(Scope = "mzc", Table = "labinspectors")]
        public class LabInspector
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64)]
            public string Name { get; set; }

            public LabInspector(int id)
            {
                ID = id;
            }

            public LabInspector() { }
        }

        [Entity(Scope = "mzc", Table = "statedefectsdata")]
        public class StateDefectsData
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "statelot", ForeignKey = true)]
            public StateSample StateLot { get; set; }

            [EntityProperty(Field = "inspector", ForeignKey = true)]
            public StateInspector Inspector { get; set; }

            [EntityProperty(Field = "inspectiondate", DbType = DbType.Date, Sorted = true)]
            public DateTime InspectionDate { get; set; }

            [EntityProperty(Field = "color", DbType = DbType.String, Size = 32, Nullable = true)]
            public string Color { get; set; }

            [EntityProperty(Field = "cleanliness", DbType = DbType.String, Size = 32, Nullable = true)]
            public string Cleanliness { get; set; }

            [EntityProperty(Field = "weight", DbType = DbType.Int32)]
            public int SampleWeight { get; set; }

            [EntityProperty(Field = "blackct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BlackCt { get; set; }

            [EntityProperty(Field = "blackwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BlackWt { get; set; }

            [EntityProperty(Field = "partlyblackct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyBlackCt { get; set; }

            [EntityProperty(Field = "partlyblackwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyBlackWt { get; set; }

            [EntityProperty(Field = "sourct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SourCt { get; set; }

            [EntityProperty(Field = "sourwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SourWt { get; set; }

            [EntityProperty(Field = "partlysourct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlySourCt { get; set; }

            [EntityProperty(Field = "partlysourwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlySourWt { get; set; }

            [EntityProperty(Field = "moldct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MoldCt { get; set; }

            [EntityProperty(Field = "moldwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MoldWt { get; set; }

            [EntityProperty(Field = "partlymoldct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyMoldCt { get; set; }

            [EntityProperty(Field = "partlymoldwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PartlyMoldWt { get; set; }

            [EntityProperty(Field = "podcherryct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PodCherryCt { get; set; }

            [EntityProperty(Field = "podcherrywt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double PodCherrykWt { get; set; }

            [EntityProperty(Field = "stickct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StickCt { get; set; }

            [EntityProperty(Field = "stickwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StickWt { get; set; }

            [EntityProperty(Field = "stonect", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StoneCt { get; set; }

            [EntityProperty(Field = "stonewt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double StoneWt { get; set; }

            [EntityProperty(Field = "huskhullct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double HuskHullCt { get; set; }

            [EntityProperty(Field = "huskhullwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double HuskHullWt { get; set; }

            [EntityProperty(Field = "fullparchmentct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double FullParchmentCt { get; set; }

            [EntityProperty(Field = "fullparchmentwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double FullParchmentWt { get; set; }

            [EntityProperty(Field = "insectct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double InsectCt { get; set; }

            [EntityProperty(Field = "insectwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double InsectWt { get; set; }

            [EntityProperty(Field = "brokencutnicksct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BrokenCutNicksCt { get; set; }

            [EntityProperty(Field = "brokencutnickswt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double BrokenCutNicksWt { get; set; }

            [EntityProperty(Field = "shellct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double ShellCt { get; set; }

            [EntityProperty(Field = "shellwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double ShellWt { get; set; }

            [EntityProperty(Field = "motherelephantct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MotherElephantCt { get; set; }

            [EntityProperty(Field = "motherelephantwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MotherElephantWt { get; set; }

            [EntityProperty(Field = "ageddiscoloredct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double AgedDiscoloredCt { get; set; }

            [EntityProperty(Field = "ageddiscoloredwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double AgedDiscoloredWt { get; set; }

            [EntityProperty(Field = "miscct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MiscCt { get; set; }

            [EntityProperty(Field = "miscwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double MiscWt { get; set; }

            [EntityProperty(Field = "quackerfloaterct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double QuackerFloaterCt { get; set; }

            [EntityProperty(Field = "quackerfloaterwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double QuackerFloaterWt { get; set; }

            [EntityProperty(Field = "dirtyct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double DirtyCt { get; set; }

            [EntityProperty(Field = "dirtywt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double DirtyWt { get; set; }

            [EntityProperty(Field = "silverskinct", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SilverskinCt { get; set; }

            [EntityProperty(Field = "silverskinwt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double SilverskinWt { get; set; }

            [EntityProperty(Field = "othertypect", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double OtherTypeCt { get; set; }

            [EntityProperty(Field = "othertypewt", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double OtherTypeWt { get; set; }

            [EntityProperty(Field = "comment", DbType = DbType.String, Size = 32, Nullable = true)]
            public string Comment { get; set; }

            public StateDefectsData()
            {
                ID = -1;
            }

            public StateDefectsData(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "stateinspectors")]
        public class StateInspector
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64)]
            public string Name { get; set; }

            public StateInspector(int id)
            {
                ID = id;
            }

            public StateInspector() { }
        }
        public static class StateSampleTool
        {
            public static int ToJdn(DateTime date)
            {
                int year = date.Year % 10;
                return year * 1000 + date.DayOfYear;
            }
        }

        [Entity(Scope = "mzc", Table = "statesamples")]
        public class StateSample : Entity<StateSample>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "bin", ForeignKey = true)]
            public Bin Bin { get; set; }

            [EntityProperty(Field = "sampledate", DbType = DbType.Date, Sorted = true)]
            public DateTime Date { get; set; }

            public int Jdn => StateSampleTool.ToJdn(this.Date);

            [EntityProperty(Field = "tape", DbType = DbType.String, Size = 5, Sorted = true)]
            public string Tape { get; set; }

            [EntityProperty(Field = "statelot", DbType = DbType.String, Size = 16, Sorted = true)]
            public string StateLot { get; set; }

            [EntityProperty(Field = "statecertificate", DbType = DbType.String, Size = 16, Sorted = true, Nullable = true)]
            public string Certificate { get; set; }

            [EntityProperty(Field = "statecertificatedate", DbType = DbType.Date, Sorted = true, Nullable = true)]
            public DateTime? CertificateDate { get; set; }

            [EntityProperty(Field = "statecertificateentrydate", DbType = DbType.Date, Sorted = true, Nullable = true)]
            public DateTime? CertificateEntryDate { get; set; }

            [EntityProperty(Field = "statelotweight", DbType = DbType.Double, Size = 16, Precision = 2, Nullable = true)]
            public double? StateLotWeight { get; set; }

            [EntityProperty(Field = "statemoisture", DbType = DbType.Double, Size = 16, Precision = 2, Nullable = true)]
            public double? StateMoisture { get; set; }

            [EntityProperty(Field = "statedefects", DbType = DbType.Double, Size = 16, Precision = 2, Nullable = true)]
            public double? StateDefects { get; set; }

            [EntityProperty(Field = "stategrade", ForeignKey = true, Nullable = true)]
            public StateGrade Grade { get; set; }

            public StateSample()
            {
                ID = -1;
            }

            public StateSample(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "statesizingdata")]
        public class StateSizingData
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "statelot", ForeignKey = true)]
            public StateSample StateLot { get; set; }

            [EntityProperty(Field = "beansizetype", DbType = DbType.Int32, Sorted = true)]
            public BeanSizeType BeanType { get; set; }

            [EntityProperty(Field = "size20", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size20 { get; set; }

            [EntityProperty(Field = "size19", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size19 { get; set; }

            [EntityProperty(Field = "size18", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size18 { get; set; }

            [EntityProperty(Field = "size17", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size17 { get; set; }

            [EntityProperty(Field = "size16", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size16 { get; set; }

            [EntityProperty(Field = "size15", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size15 { get; set; }

            [EntityProperty(Field = "size14", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size14 { get; set; }

            [EntityProperty(Field = "size13", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size13 { get; set; }

            [EntityProperty(Field = "size12", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size12 { get; set; }

            [EntityProperty(Field = "size10", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Size10 { get; set; }

            [EntityProperty(Field = "under20", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double UnderSize20 { get; set; }

            [EntityProperty(Field = "undersize", DbType = DbType.Double, Size = 7, Precision = 1)]
            public double Undersize { get; set; }

            [EntityProperty(Field = "beansize", DbType = DbType.String, Size = 16)]
            public string Beansize { get; set; }

            public StateSizingData()
            {
                ID = -1;
            }

            public StateSizingData(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "farms")]
        public class Farm : Entity<Farm>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            public Farm()
            {
                ID = -1;
            }

            internal Farm(int id)
            {
                ID = id;
            }

            internal Farm(int id, string name)
            {
                ID = id;
                Name = name;
            }
        }
        [Entity(Scope = "mzc", Table = "fields")]
        public class Field : Entity<Field>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            public Field()
            {
                ID = -1;
            }

            public Field(int id)
            {
                ID = id;
            }

            internal Field(int id, string name)
            {
                ID = id;
                Name = name;
            }
        }
        [Entity(Scope = "mzc", Table = "fieldlots")]
        public class FieldLot : Entity<FieldLot>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            [EntityProperty(Field = "field", ForeignKey = true)]
            public Field Field { get; set; }

            [EntityProperty(Field = "farm", ForeignKey = true)]
            public Farm Farm { get; set; }

            [EntityProperty(Field = "variety", ForeignKey = true)]
            public Variety Variety { get; set; }
            public bool HasPlantDate => PlantDate.Ticks != 0;

            [EntityProperty(Field = "plantdate", DbType = DbType.Date, Nullable = true)]
            public DateTime PlantDate { get; set; }

            [EntityProperty(Field = "prunedate", DbType = DbType.Date, Nullable = true)]
            public DateTime? PruneDate { get; set; }

            [EntityProperty(Field = "hspa_acres", DbType = DbType.Double, Size = 10, Precision = 2, Nullable = true)]
            public double HspaAcres { get; set; }

            [EntityProperty(Field = "net_acres", DbType = DbType.Double, Size = 10, Precision = 2, Nullable = true)]
            public double NetAcres { get; set; }
            public double NetHectares => NetAcres * AcresToHectares;

            public static double AcresToHectares => 0.40468564224;

            [EntityProperty(Field = "trees_per_acre", DbType = DbType.Int32, Nullable = true)]
            public int TreesPerAcre { get; set; }

            public FieldLot()
            {
                ID = -1;
            }

            public FieldLot(int id)
            {
                ID = id;
            }

            internal FieldLot(int id, string name, Field field, Farm farm, Variety variety, DateTime plantDate, double hspaAcres, double netAcres, int treePerAcre)
            {
                ID = id;
                Name = name;
                Field = field;
                Farm = farm;
                Variety = variety;
                PlantDate = plantDate;
                HspaAcres = hspaAcres;
                NetAcres = netAcres;
                TreesPerAcre = treePerAcre;
            }
        }
        [Entity(Scope = "mzc", Table = "branchcount")]
        public class HarvestBranchCount : Entity<HarvestBranchCount>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "no", DbType = DbType.Int32, Sorted = true)]
            public int No { get; set; }

            [EntityProperty(Field = "lot", ForeignKey = true)]
            public FieldLot Lot { get; set; }

            [EntityProperty(Field = "station", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Station { get; set; }

            [EntityProperty(Field = "date", DbType = DbType.Date, Sorted = true)]
            public DateTime Date { get; set; }

            public HarvestBranchCount()
            {
                ID = -1;
            }

            public HarvestBranchCount(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "harvestcrew")]
        public class HarvestCrew : Entity<HarvestCrew>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            public HarvestCrew()
            {
                ID = -1;
            }

            internal HarvestCrew(int id)
            {
                ID = id;
            }

            internal HarvestCrew(int id, string name)
            {
                ID = id;
                Name = name;
            }
        }
        [Entity(Scope = "mzc", Table = "harvestestimate")]
        public class HarvestEstimate
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "season", ForeignKey = true)]
            public HarvestPeriod Period { get; set; }

            [EntityProperty(Field = "lot", ForeignKey = true)]
            public FieldLot Lot { get; set; }

            [EntityProperty(Field = "estimate", DbType = DbType.Double, Size = 10, Precision = 0)]
            public double Estimate { get; set; }

            [EntityProperty(Field = "harvestdate", DbType = DbType.Date, Nullable = true, Sorted = true)]
            public DateTime? HarvestDate { get; set; }

            [EntityProperty(Field = "acres", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double? Acres { get; set; }

            [EntityProperty(Field = "cherryyield", DbType = DbType.Double, Size = 10, Precision = 0)]
            public double? CherryYield { get; set; }

            public HarvestEstimate()
            {
                ID = -1;
            }
        }
        public enum HarvestShiftTime
        {
            AM,
            PM,
        }

        [Entity(Scope = "mzc", Table = "harvestshift")]
        public class HarvestShift : Entity<HarvestShift>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "shift_no", DbType = DbType.Int32, Sorted = true)]
            public int ShiftNo { get; set; }

            [EntityProperty(Field = "shift_time", DbType = DbType.Int32, Sorted = true)]
            public HarvestShiftTime ShiftTime { get; set; }

            [EntityProperty(Field = "crew", ForeignKey = true)]
            public HarvestCrew Crew { get; set; }

            [EntityProperty(Field = "date", DbType = DbType.Date, Sorted = true)]
            public DateTime Date { get; set; }

            [EntityProperty(Field = "rpm", DbType = DbType.Int32)]
            public int Rpm { get; set; }

            [EntityProperty(Field = "mph", DbType = DbType.Double, Size = 8, Precision = 1)]
            public double Mph { get; set; } = 0.3;

            public double TotalAcres { get; internal set; }

            public double TotalVolume { get; internal set; }

            public HarvestShift()
            {
                ID = -1;
            }

            internal HarvestShift(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "harvestshiftacres")]
        public class HarvestShiftAcres : Entity<HarvestShiftAcres>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "shift", ForeignKey = true)]
            public HarvestShift Shift { get; set; }

            [EntityProperty(Field = "lot", ForeignKey = true)]
            public FieldLot FieldLot { get; set; }

            [EntityProperty(Field = "pass", DbType = DbType.Int32, Sorted = true)]
            public int Pass { get; set; }

            [EntityProperty(Field = "acres", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double Acres { get; set; }

            public HarvestShiftAcres()
            {
                ID = -1;
            }

            internal HarvestShiftAcres(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "harvestshiftloads")]
        public class HarvestShiftLoad : Entity<HarvestShiftLoad>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "no", DbType = DbType.Int32, Sorted = true)]
            public int No { get; set; }

            [EntityProperty(Field = "shift", ForeignKey = true)]
            public HarvestShift Shift { get; set; }

            [EntityProperty(Field = "lot", ForeignKey = true)]
            public FieldLot FieldLot { get; set; }

            [EntityProperty(Field = "pass", DbType = DbType.Int32, Sorted = true)]
            public int Pass { get; set; }

            [EntityProperty(Field = "volume", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double Volume { get; set; }

            public HarvestShiftLoad()
            {
                ID = -1;
            }

            internal HarvestShiftLoad(int id)
            {
                ID = id;
            }
        }
        [Entity(Scope = "mzc", Table = "orchard_actions")]
        public class OrchardAction : Entity<OrchardAction>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            public OrchardAction()
            {
                ID = -1;
            }

            internal OrchardAction(int id)
            {
                ID = id;
            }

            internal OrchardAction(int id, string name)
            {
                ID = id;
                Name = name;
            }
        }

        [Entity(Scope = "mzc", Table = "orchard_application")]
        public class OrchardApplication : Entity<OrchardApplication>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            public DateTime mDate;

            [EntityProperty(Field = "applied", DbType = DbType.Date, Sorted = true)]
            public DateTime Date
            {
                get { return mDate; }

                set
                {
                    if (value.Hour != 0 || value.Minute != 0 || value.Second != 0 || value.Millisecond != 0)
                        mDate = new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Unspecified);
                    else
                        mDate = value;
                }
            }

            [EntityProperty(Field = "field", ForeignKey = true)]
            public Field Field { get; set; }

            [EntityProperty(Field = "action", ForeignKey = true)]
            public OrchardAction OrchardAction { get; set; }

            [EntityProperty(Field = "operator", ForeignKey = true)]
            public OrchardOperator OrchardOperator { get; set; }

            [EntityProperty(Field = "volume", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double Volume { get; set; }

            [EntityProperty(Field = "acres", DbType = DbType.Double, Size = 10, Precision = 2)]
            public double Acreage { get; set; }

            [EntityProperty(Field = "comment", DbType = DbType.String, Size = 128)]
            public string Comment { get; set; }

            [Entity(Scope = "mzc", Table = "orchard_application_chemical")]
            public class AppliedChemical : Entity<AppliedChemical>
            {
                private OrchardChemical mChemical;
                private double mAmount;

                [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
                public int ID { get; internal set; }

                [EntityProperty(Field = "chemical", ForeignKey = true)]
                public OrchardChemical Chemical
                {
                    get { return mChemical; }
                    set
                    {
                        mChemical = value;
                        Changed = true;
                    }
                }

                [EntityProperty(Field = "amount", DbType = DbType.Double, Size = 10, Precision = 2)]
                public double Amount
                {
                    get { return mAmount; }
                    set
                    {
                        mAmount = value;
                        Changed = true;
                    }
                }

                [EntityProperty(Field = "application", ForeignKey = true, IgnoreRead = true)]
                public OrchardApplication Application { get; set; }

                public bool Changed { get; internal set; }

                public AppliedChemical()
                {
                }

                public AppliedChemical(OrchardChemical chemical, double amount)
                {
                    ID = -1;
                    mChemical = chemical;
                    mAmount = amount;
                    Changed = true;
                }

                internal AppliedChemical(int id, OrchardChemical chemical, double amount)
                {
                    ID = id;
                    mChemical = chemical;
                    mAmount = amount;
                    Changed = false;
                }
            }

            public OrchardApplication()
            {
                ID = -1;
            }

            internal OrchardApplication(int id, Field field, OrchardAction action, OrchardOperator op, DateTime date, double volume, double acreage, string comment)
            {
                ID = id;
                Date = date;
                Field = field;
                OrchardAction = action;
                OrchardOperator = op;
                Volume = volume;
                Acreage = acreage;
                Comment = comment;
            }
        }

        [Entity(Scope = "mzc", Table = "orchard_chemicals")]
        public class OrchardChemical : Entity<OrchardChemical>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            [EntityProperty(Field = "isdefault", DbType = DbType.Boolean)]
            public bool IsDefault { get; set; }

            public OrchardChemical()
            {
                ID = -1;
            }

            public OrchardChemical(int id)
            {
                ID = id;
            }

            internal OrchardChemical(int id, string name, bool isdefault)
            {
                ID = id;
                Name = name;
                IsDefault = isdefault;
            }
        }

        [Entity(Scope = "mzc", Table = "orchard_operators")]
        public class OrchardOperator : Entity<OrchardOperator>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            public OrchardOperator()
            {
                ID = -1;
            }

            internal OrchardOperator(int id)
            {
                ID = id;
            }

            internal OrchardOperator(int id, string name)
            {
                ID = id;
                Name = name;
            }
        }

        public enum BeanType
        {
            Unknown = 0,
            Ripe = 1000,
            Floater = 3000,
            Green = 5000
        }

        [Entity(Scope = "mzc", Table = "varieties")]
        public class Variety : Entity<Variety>
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; internal set; }

            [EntityProperty(Field = "code", DbType = DbType.String, Size = 16, Sorted = true)]
            public string Code { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            public Variety()
            {
                ID = -1;
            }

            public Variety(int id)
            {
                ID = id;
            }

            internal Variety(int id, string code, string name)
            {
                ID = id;
                Code = code;
                Name = name;
            }
        }

        #endregion

        [Test]
        public void TestFinderAndSorter()
        {
            EntityFinder.EntityTypeInfo[] types = EntityFinder.FindEntities(new Assembly[] { typeof(TestEntityResolver).GetTypeInfo().Assembly }, "mzc", true);
            EntityFinder.ArrageEntities(types);

            int sc = 0;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i].EntityType == typeof(MetalCrateBase))
                {
                    types[i].View.Should().BeTrue();
                    i.Should().Be(types.Length - 1);
                }
                else
                    types[i].View.Should().BeFalse();

                PropertyInfo[] props = types[i].EntityType.GetTypeInfo().GetProperties();
                foreach (PropertyInfo pi in props)
                {
                    EntityPropertyAttribute attr = pi.GetCustomAttribute<EntityPropertyAttribute>();
                    if (attr != null && attr.ForeignKey)
                    {
                        Type toSearch = pi.PropertyType;
                        sc++;
                        int pos = -1;
                        for (int j = 0; j < types.Length && pos == -1; j++)
                        {
                            if (types[j].EntityType == toSearch)
                                pos = j;
                        }
                        ClassicAssert.AreNotEqual(-1, pos);
                        ClassicAssert.IsTrue(pos < i);
                    }
                }
            }
            ClassicAssert.AreNotEqual(0, sc);
        }
    }
}

