using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class BasicQueryTests : IClassFixture<BasicQueryTests.Fixture>
    {
        #region entities
        public class DictRecord
        {
            public Guid Id { get; set; }
            public string Name { get; set; }

            public DictRecord()
            {
                Id = Guid.NewGuid();
            }

            public DictRecord(Guid id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        public class TableRecord
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Order { get; set; }
            public DictRecord Dict { get; set; }
            public Guid DictId => Dict.Id;
            public double DoubleV { get; set; }
            public bool BoolV { get; set; }
            public decimal DecimalV { get; set; }
            public long LongV { get; set; }
            public DateTime? DateV { get; set; }

            public TableRecord()
            {
            }

            public TableRecord(int id, string name, int order, DictRecord dict, double doubleV, bool boolV, decimal decimalV, long longV, DateTime dateV)
            {
                Id = id;
                Name = name;
                Order = order;
                Dict = dict;
                DoubleV = doubleV;
                BoolV = boolV;
                DecimalV = decimalV;
                LongV = longV;
                DateV = dateV;
            }
        }
        #endregion

        #region fixture
        public class Fixture : ConnectionFixtureBase
        {
            private readonly Dictionary<string, List<DictRecord>> mDictContent = new Dictionary<string, List<DictRecord>>();
            private readonly Dictionary<string, List<TableRecord>> mTableContent = new Dictionary<string, List<TableRecord>>();

            public List<DictRecord> DictContent(string connectionName)
            {
                if (!mDictContent.TryGetValue(connectionName, out var content))
                {
                    content = new List<DictRecord>();
                    for (int i = 0; i < 5; i++)
                    {
                        DictRecord d = new DictRecord()
                        {
                            Name = $"dict{i + 1}"
                        };
                        content.Add(d);
                    }
                    mDictContent[connectionName] = content;
                }
                return content;
            }

            public List<TableRecord> TableContent(string connectionName, SqlDbLanguageSpecifics specifics = null)
            {
                var maxd = specifics?.MaxNumericValue ?? Double.MaxValue;

                var dictContent = DictContent(connectionName);

                if (!mTableContent.TryGetValue(connectionName, out var content))
                {
                    content = new List<TableRecord>();

                    Random r = new Random();

                    for (int i = 0; i < 50; i++)
                    {
                        TableRecord t = new TableRecord()
                        {
                            Name = $"table{i + 1:D2}",
                            Order = i,
                            Dict = dictContent[i % 5],
                            BoolV = i % 2 == 0,
                            DateV = DateTime.Now.AddMinutes(r.Next(5, 10000)),
                            DecimalV = (decimal)(Math.Round(r.NextDouble() * 50_000_000_000_000.0) / 100),
                            DoubleV = r.NextDouble() * maxd,
                            LongV = r.Next(),
                        };
                        content.Add(t);
                    }

                    mTableContent[connectionName] = content;
                }
                return content;
            }

            public static bool DropAtEnd { get; set; } = false;

            public const string TableName = "iu_test";
            public const string DictName = "iu_dict";

            public TableDescriptor Dict { get; }
            public TableDescriptor Table { get; }

            public Fixture()
            {
                Dict = new TableDescriptor()
                {
                    Name = DictName,
                };

                Dict.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "id",
                    PrimaryKey = true,
                    DbType = DbType.Guid
                });

                Dict.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                    Sorted = true,
                });

                Table = new TableDescriptor()
                {
                    Name = TableName,
                };

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "id",
                    PrimaryKey = true,
                    Autoincrement = true,
                    DbType = DbType.Int32
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                    Sorted = true,
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "dict",
                    DbType = DbType.Guid,
                    ForeignTable = Dict
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "dblv",
                    DbType = DbType.Double,
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "dcv",
                    DbType = DbType.Decimal,
                    Size = 20,
                    Precision = 2,
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "boolv",
                    DbType = DbType.Boolean,
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "rorder",
                    DbType = DbType.Int32,
                    Sorted = true,
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "longv",
                    DbType = DbType.Int64,
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "datev",
                    DbType = DbType.DateTime,
                    Nullable = true,
                });
            }

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);

                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(Dict)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(Table)))
                    query.ExecuteNoData();

                base.ConfigureConnection(connection);
            }

            protected override void TearDownConnection(SqlDbConnection connection)
            {
                if (DropAtEnd)
                    Drop(connection);

                base.TearDownConnection(connection);
            }

            private void Drop(SqlDbConnection connection)
            {
                using (var query = connection.GetQuery(connection.GetDropTableBuilder(Table)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(connection.GetDropTableBuilder(Dict)))
                    query.ExecuteNoData();
            }
        }
        #endregion

        public static IEnumerable<object[]> ConnectionNames(string flags = null) => SqlConnectionSources.ConnectionNames(flags);

        private readonly Fixture mFixture;

        public BasicQueryTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [TestOrder(11)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T1_1_Insert(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dict = mFixture.DictContent(connectionName);
            var table = mFixture.TableContent(connectionName, connection.GetLanguageSpecifics());
            using (var query = connection.GetQuery(connection.GetInsertQueryBuilder(mFixture.Dict)))
            {
                for (int i = 0; i < dict.Count; i++)
                {
                    query.BindParam<Guid>("id", dict[i].Id);
                    query.BindParam<string>("name", dict[i].Name);
                    query.ExecuteNoData();
                }
            }

            List<TableRecord> created = new List<TableRecord>();

            using (var query = connection.GetQuery(connection.GetInsertQueryBuilder(mFixture.Table)))
            {
                for (int i = 0; i < 25; i++)
                {
                    var t = table[i];

                    query.BindParam("name", t.Name);
                    query.BindParam("dict", t.Dict.Id);
                    query.BindParam("rorder", t.Order);
                    query.BindParam("dblv", t.DoubleV);
                    query.BindParam("dcv", t.DecimalV);
                    query.BindParam("boolv", t.BoolV);
                    query.BindParam("longv", t.LongV);
                    query.BindParam("datev", t.DateV);

                    var idAs = connection.GetLanguageSpecifics().AutoincrementReturnedAs;

                    if (idAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                        query.BindOutput("id", DbType.Int32);

                    switch (idAs)
                    {
                        case SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter:
                            query.ExecuteNoData();
                            t.Id = query.GetParamValue<int>("id");
                            break;
                        case SqlDbLanguageSpecifics.AutoincrementReturnStyle.FirstResultset:
                            query.ExecuteReader();
                            query.ReadNext();
                            t.Id = query.GetValue<int>(0);
                            break;
                        case SqlDbLanguageSpecifics.AutoincrementReturnStyle.SecondResultset:
                            query.ExecuteReader();
                            query.NextReaderResult();
                            query.ReadNext();
                            t.Id = query.GetValue<int>(0);
                            break;
                    }

                    if (created.Count > 0)
                        t.Id.Should().BeGreaterThan(created.Max(t => t.Id));

                    created.Add(t);
                }

                UpdateQueryToTypeBinder binder = new UpdateQueryToTypeBinder(typeof(TableRecord));
                binder.AddBinding("id", nameof(TableRecord.Id), DbType.Int32, 0, true);
                binder.AddBinding("name", nameof(TableRecord.Name), DbType.String, 0);
                binder.AddBinding("rorder", nameof(TableRecord.Order), DbType.Int32, 0);
                binder.AddBinding("dict", nameof(TableRecord.DictId), DbType.Guid, 0);
                binder.AddBinding("dblv", nameof(TableRecord.DoubleV), DbType.Double, 0);
                binder.AddBinding("dcv", nameof(TableRecord.DecimalV), DbType.Decimal, 0);
                binder.AddBinding("boolv", nameof(TableRecord.BoolV), DbType.Boolean, 0);
                binder.AddBinding("longv", nameof(TableRecord.LongV), DbType.Int64, 0);
                binder.AddBinding("datev", nameof(TableRecord.DateV), DbType.DateTime, 0);

                for (int i = 25; i < table.Count; i++)
                {
                    TableRecord t = table[i];

                    binder.BindAndExecute(query, t, true);

                    if (created.Count > 0)
                        t.Id.Should().BeGreaterThan(created.Max(t => t.Id));

                    created.Add(t);
                }
            }
        }

        [Theory]
        [TestOrder(12)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T1_2_GetCountUsingSelect(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T1_1_Insert(connectionName);

            var connection = mFixture.GetInstance(connectionName);
            var table = mFixture.TableContent(connectionName);

            var builder = connection.GetSelectQueryBuilder(mFixture.Table);
            builder.AddToResultset(AggFn.Count);

            using (var query = connection.GetQuery(builder))
            {
                query.ExecuteReader();
                query.ReadNext();
                query.GetValue<int>(0).Should().Be(table.Count);
            }
        }

        [Theory]
        [TestOrder(13)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T1_3_ReadAllUsingQuery(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T1_1_Insert(connectionName);

            var connection = mFixture.GetInstance(connectionName);
            var table = mFixture.TableContent(connectionName);

            var builder = connection.GetSelectQueryBuilder(mFixture.Table);
            builder.AddTable(mFixture.Dict);

            builder.AddToResultset(mFixture.Table);
            builder.AddToResultset(mFixture.Dict, "dict_");
            builder.AddOrderBy(mFixture.Table["datev"]);

            DateTime? previousDate = null;

            using (var query = connection.GetQuery(builder))
            {
                query.ExecuteReader();
                while (query.ReadNext())
                {
                    var id = query.GetValue<int>("id");
                    var name = query.GetValue<string>("name");
                    var roder = query.GetValue<int>("rorder");

                    var dict_id = query.GetValue<Guid>("dict_id");
                    var dict_name = query.GetValue<string>("dict_name");

                    var dblv = query.GetValue<double>("dblv");
                    var dcv = query.GetValue<decimal>("dcv");
                    var boolv = query.GetValue<bool>("boolv");
                    var longv = query.GetValue<long>("longv");
                    var datev = query.GetValue<DateTime?>("datev");

                    var t = table.Find(t => t.Id == id);
                    t.Should().NotBeNull();
                    t.Name.Should().Be(name);
                    t.Order.Should().Be(roder);
                    t.Dict.Id.Should().Be(dict_id);
                    t.Dict.Name.Should().Be(dict_name);
                    t.DoubleV.Should().BeApproximately(dblv, Math.Pow(10, Math.Log10(dblv) - 14));
                    t.DecimalV.Should().Be(dcv);
                    t.BoolV.Should().Be(boolv);
                    t.LongV.Should().Be(longv);
                    t.DateV.Should().BeCloseTo(datev.Value, TimeSpan.FromSeconds(1));

                    if (previousDate != null)
                        datev.Should().BeOnOrAfter(previousDate.Value);
                    previousDate = datev;
                }
            }
        }

        [Theory]
        [TestOrder(14)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T1_4_ReadAllBinder(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T1_1_Insert(connectionName);

            var connection = mFixture.GetInstance(connectionName);
            var table = mFixture.TableContent(connectionName);

            var dictBinder = new SelectQueryTypeBinder(typeof(DictRecord));
            dictBinder.AddBinding("dict_id", nameof(DictRecord.Id));
            dictBinder.AddBinding("dict_name", nameof(DictRecord.Name));

            var tableBinder = new SelectQueryTypeBinder(typeof(TableRecord));
            tableBinder.AddBinding("id", nameof(TableRecord.Id));
            tableBinder.AddBinding("name", nameof(TableRecord.Name));
            tableBinder.AddBinding(dictBinder, nameof(TableRecord.Dict));
            tableBinder.AddBinding("rorder", nameof(TableRecord.Order));
            tableBinder.AddBinding("dblv", nameof(TableRecord.DoubleV));
            tableBinder.AddBinding("dcv", nameof(TableRecord.DecimalV));
            tableBinder.AddBinding("boolv", nameof(TableRecord.BoolV));
            tableBinder.AddBinding("longv", nameof(TableRecord.LongV));
            tableBinder.AddBinding("datev", nameof(TableRecord.DateV));

            var builder = connection.GetSelectQueryBuilder(mFixture.Table);
            builder.AddTable(mFixture.Dict);

            builder.AddToResultset(mFixture.Table);
            builder.AddToResultset(mFixture.Dict, "dict_");

            using (var query = connection.GetQuery(builder))
            {
                query.ExecuteReader();
                while (query.ReadNext())
                {
                    var t1 = tableBinder.Read<TableRecord>(query);

                    var t = table.Find(t => t.Id == t1.Id);
                    t.Should().NotBeNull();
                    t.Name.Should().Be(t1.Name);
                    t.Order.Should().Be(t1.Order);
                    t.Dict.Id.Should().Be(t1.Dict.Id);
                    t.Dict.Name.Should().Be(t1.Dict.Name);
                    t.DoubleV.Should().BeApproximately(t1.DoubleV, Math.Pow(10, Math.Log10(t1.DoubleV) - 14));
                    t.DecimalV.Should().Be(t1.DecimalV);
                    t.BoolV.Should().Be(t1.BoolV);
                    t.LongV.Should().Be(t1.LongV);
                    t.DateV.Should().BeCloseTo(t1.DateV.Value, TimeSpan.FromSeconds(1));
                }
            }
        }

        [Theory]
        [TestOrder(21)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T2_1_Insert_IgnoringAutoincrement(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T1_1_Insert(connectionName);

            var connection = mFixture.GetInstance(connectionName);
            var dict = mFixture.DictContent(connectionName);

            var t = new TableRecord()
            {
                Id = 500,
                Name = "temporary",
                Dict = dict[0],
                BoolV = false,
                DateV = null,
                Order = 500,
                DecimalV = 1.0m,
                DoubleV = 2.0,
                LongV = 3
            };

            using (var query = connection.GetQuery(connection.GetInsertQueryBuilder(mFixture.Table, true)))
            {
                query.BindParam("id", t.Id);
                query.BindParam("name", t.Name);
                query.BindParam("dict", t.Dict.Id);
                query.BindParam("rorder", t.Order);
                query.BindParam("dblv", t.DoubleV);
                query.BindParam("dcv", t.DecimalV);
                query.BindParam("boolv", t.BoolV);
                query.BindParam("longv", t.LongV);
                query.BindParam("datev", t.DateV);

                query.ExecuteNoData();
            }

            if (connection is OracleDbConnection oracle)
                oracle.UpdateSequence(mFixture.Table);

            var select = connection.GetSelectQueryBuilder(mFixture.Table);
            select.AddToResultset(mFixture.Table);
            select.Where.Property(mFixture.Table["id"]).Eq().Parameter("id");
            using (var query = connection.GetQuery(select))
            {
                query.BindParam("id", t.Id);
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>("id").Should().Be(t.Id);
            }

            var t1 = new TableRecord()
            {
                Name = "temporary",
                Dict = dict[0],
                BoolV = false,
                DateV = null,
                Order = 500,
                DecimalV = 1.0m,
                DoubleV = 2.0,
                LongV = 3
            };

            using (var query = connection.GetQuery(connection.GetInsertQueryBuilder(mFixture.Table, false)))
            {
                query.BindParam("name", t1.Name);
                query.BindParam("dict", t1.Dict.Id);
                query.BindParam("rorder", t1.Order);
                query.BindParam("dblv", t1.DoubleV);
                query.BindParam("dcv", t1.DecimalV);
                query.BindParam("boolv", t1.BoolV);
                query.BindParam("longv", t1.LongV);
                query.BindParam("datev", t1.DateV);

                var idAs = connection.GetLanguageSpecifics().AutoincrementReturnedAs;

                if (idAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                    query.BindOutput("id", DbType.Int32);

                switch (idAs)
                {
                    case SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter:
                        query.ExecuteNoData();
                        t1.Id = query.GetParamValue<int>("id");
                        break;
                    case SqlDbLanguageSpecifics.AutoincrementReturnStyle.FirstResultset:
                        query.ExecuteReader();
                        query.ReadNext();
                        t1.Id = query.GetValue<int>(0);
                        break;
                    case SqlDbLanguageSpecifics.AutoincrementReturnStyle.SecondResultset:
                        query.ExecuteReader();
                        query.NextReaderResult();
                        query.ReadNext();
                        t1.Id = query.GetValue<int>(0);
                        break;
                }
            }

            t1.Id.Should().BeGreaterThan(t.Id);

            using (var query = connection.GetQuery(select))
            {
                query.BindParam("id", t1.Id);
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>("id").Should().Be(t1.Id);
            }
        }

        [Theory]
        [TestOrder(22)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T2_2_UpdateRecord(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T2_1_Insert_IgnoringAutoincrement(connectionName);

            var connection = mFixture.GetInstance(connectionName);
            var dict = mFixture.DictContent(connectionName);

            var t1 = new TableRecord()
            {
                Id = 500,
                Name = "newname",
                Dict = dict[1],
                BoolV = true,
                DateV = new DateTime(2021, 11, 28),
                Order = 657,
                DecimalV = 11.0m,
                DoubleV = 12.0,
                LongV = 13
            };

            var update = connection.GetUpdateQueryBuilder(mFixture.Table);
            update.AddUpdateAllColumns();
            update.UpdateById();
            using (var query = connection.GetQuery(update))
            {
                query.BindParam("id", t1.Id);
                query.BindParam("name", t1.Name);
                query.BindParam("dict", t1.Dict.Id);
                query.BindParam("rorder", t1.Order);
                query.BindParam("dblv", t1.DoubleV);
                query.BindParam("dcv", t1.DecimalV);
                query.BindParam("boolv", t1.BoolV);
                query.BindParam("longv", t1.LongV);
                query.BindParam("datev", t1.DateV);

                query.ExecuteNoData().Should().Be(1);
            }

            var select = connection.GetSelectQueryBuilder(mFixture.Table);
            select.AddToResultset(mFixture.Table);
            select.Where.Property(mFixture.Table["id"]).Eq().Value(500);
            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();

                var id = query.GetValue<int>("id");
                var name = query.GetValue<string>("name");
                var roder = query.GetValue<int>("rorder");

                var dict_id = query.GetValue<Guid>("dict");

                var dblv = query.GetValue<double>("dblv");
                var dcv = query.GetValue<decimal>("dcv");
                var boolv = query.GetValue<bool>("boolv");
                var longv = query.GetValue<long>("longv");
                var datev = query.GetValue<DateTime?>("datev");

                t1.Id.Should().Be(id);
                t1.Name.Should().Be(name);
                t1.Order.Should().Be(roder);
                t1.Dict.Id.Should().Be(dict_id);
                t1.DoubleV.Should().BeApproximately(dblv, Math.Pow(10, Math.Log10(dblv) - 14));
                t1.DecimalV.Should().Be(dcv);
                t1.BoolV.Should().Be(boolv);
                t1.LongV.Should().Be(longv);
                t1.DateV.Should().BeCloseTo(datev.Value, TimeSpan.FromSeconds(1));
            }
        }

        [Theory]
        [TestOrder(23)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T2_3_UpdateMultipleRecords(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T2_1_Insert_IgnoringAutoincrement(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var select = connection.GetSelectQueryBuilder(mFixture.Table);
            select.AddToResultset(mFixture.Table["id"]);
            select.AddToResultset(mFixture.Table["longv"]);
            select.Where.Property(mFixture.Table["id"]).Ge().Value(500);

            Dictionary<int, long> longs = new Dictionary<int, long>();
            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                while (query.ReadNext())
                    longs[query.GetValue<int>(0)] = query.GetValue<long>(1);
            }

            longs.Should().HaveCount(2);

            var update = connection.GetUpdateQueryBuilder(mFixture.Table);
            update.AddUpdateColumn(mFixture.Table["name"]);
            update.AddUpdateColumnExpression(mFixture.Table["longv"],
                update.GetAlias(mFixture.Table["longv"], update.Entities[0]) + " * 2");
            update.Where.Property(mFixture.Table["id"]).Ge().Parameter("id");

            using (var query = connection.GetQuery(update))
            {
                query.BindParam("id", 500);
                query.BindParam("name", "newname");
                query.ExecuteNoData().Should().Be(2);
            }

            var select1 = connection.GetSelectQueryBuilder(mFixture.Table);
            select1.AddToResultset(mFixture.Table);
            select1.Where.Property(mFixture.Table["id"]).Ge().Value(500);
            using (var query = connection.GetQuery(select1))
            {
                query.ExecuteReader();
                var rows = 0;
                while (query.ReadNext())
                {
                    rows++;
                    var id = query.GetValue<int>("id");
                    var oldLong = longs[id];
                    query.GetValue<long>("longv").Should().Be(oldLong * 2);
                    query.GetValue<string>("name").Should().Be("newname");
                }
                rows.Should().Be(2);
            }
        }

        [Theory]
        [TestOrder(24)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T2_4_UpdateUsingSelect(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T2_1_Insert_IgnoringAutoincrement(connectionName);

            var connection = mFixture.GetInstance(connectionName);
            var table = mFixture.TableContent(connectionName);

            var update = connection.GetUpdateQueryBuilder(mFixture.Table);

            var select = connection.GetSelectQueryBuilder(mFixture.Table);
            select.AddToResultset(AggFn.Max, mFixture.Table["id"]);
            select.Where.Property(mFixture.Table["id"])
                .Ls()
                .Reference(update.GetReference(mFixture.Table["id"]));

            update.AddUpdateColumnSubquery(mFixture.Table["longv"], select);
            update.Where.Property(mFixture.Table["id"]).Ge().Parameter("id");

            using (var query = connection.GetQuery(update))
            {
                query.BindParam("id", 500);
                query.ExecuteNoData().Should().Be(2);
            }

            var select1 = connection.GetSelectQueryBuilder(mFixture.Table);
            select1.AddToResultset(mFixture.Table);
            select1.Where.Property(mFixture.Table["id"]).Ge().Value(500);
            select1.AddOrderBy(mFixture.Table["id"]);
            using (var query = connection.GetQuery(select1))
            {
                query.ExecuteReader();
                var rows = 0;
                while (query.ReadNext())
                {
                    rows++;
                    var id = query.GetValue<int>("id");
                    var longv = query.GetValue<long>("longv");
                    longv.Should().BeLessThan(id);
                    if (rows == 1)
                        longv.Should().Be(table.Max(t => t.Id));
                    else if (rows == 2)
                        longv.Should().Be(500);
                }
                rows.Should().Be(2);
            }
        }

        [Theory]
        [TestOrder(25)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T2_5_Delete(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T2_1_Insert_IgnoringAutoincrement(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var select = connection.GetSelectQueryBuilder(mFixture.Table);
            select.AddToResultset(mFixture.Table["id"]);
            select.Where.Property(mFixture.Table["id"]).Ge().Parameter("id");

            using (var query = connection.GetQuery(select))
            {
                query.BindParam("id", 500);
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.ReadNext().Should().BeTrue();
                query.ReadNext().Should().BeFalse();
            }

            var delete = connection.GetDeleteQueryBuilder(mFixture.Table);
            delete.Where.Property(mFixture.Table["id"]).Ge().Parameter("id");
            using (var query = connection.GetQuery(delete))
            {
                query.BindParam("id", 500);
                query.ExecuteNoData().Should().Be(2);
            }

            using (var query = connection.GetQuery(select))
            {
                query.BindParam("id", 500);
                query.ExecuteReader();
                query.ReadNext().Should().BeFalse();
            }
        }
    }
}
