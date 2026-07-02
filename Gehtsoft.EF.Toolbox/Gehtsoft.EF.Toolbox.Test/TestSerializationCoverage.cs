using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Serialization.IO;
using Gehtsoft.EF.Serialization.IO.Binary;
using Gehtsoft.EF.Serialization.IO.Db;
using Gehtsoft.EF.Serialization.IO.Json;
using Gehtsoft.EF.Serialization.IO.Xml;
using AwesomeAssertions;
using Xunit;

namespace Gehtsoft.EF.Toolbox.Test
{
    /// <summary>
    /// Targeted coverage for the parts of Gehtsoft.EF.Serialization that the happy-path
    /// round-trip suite (<see cref="TestSerializationRoundTrip"/>) does not reach: the
    /// file-based blob accessor, the Stream / StringWriter / FileStream constructors, the
    /// defensive guard clauses on every writer, the binary/text codec error codes, entity
    /// type resolution failures, cancellation, DB frame-paging and the XML default-value /
    /// max-properties handling.
    /// </summary>
    public class TestSerializationCoverage
    {
        private const string CovScope = "ser_cov";
        private const string BadScope = "ser_cov_bad";

        // self-referencing tree (exercises ProcessSelfReferencedEntity)
        [Entity(Scope = CovScope, Table = "cov_group")]
        public class CovGroup
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Size = 64)]
            public string Name { get; set; }

            [EntityProperty(ForeignKey = true, Nullable = true)]
            public CovGroup Parent { get; set; }
        }

        // regular entity (exercises ProcessRegularEntity + frame paging); carries a blob and
        // a defaulted column so the materializer's default-value branch is exercised.
        [Entity(Scope = CovScope, Table = "cov_item")]
        public class CovItem
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true, Nullable = true)]
            public CovGroup Owner { get; set; }

            [EntityProperty(Size = 64)]
            public string Name { get; set; }

            [EntityProperty(Size = 64, Nullable = true, DefaultValue = "pending")]
            public string Status { get; set; }

            [EntityProperty(DbType = DbType.Binary, Size = 128, Nullable = true)]
            public byte[] Data { get; set; }

            [EntityProperty]
            public int Count { get; set; }
        }

        // deliberately invalid: two self references. DbEntityReader must refuse it.
        [Entity(Scope = BadScope, Table = "cov_bad")]
        public class CovBad
        {
            [EntityProperty(AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true, Nullable = true)]
            public CovBad ParentA { get; set; }

            [EntityProperty(ForeignKey = true, Nullable = true)]
            public CovBad ParentB { get; set; }
        }

        private static readonly Assembly[] Asm = { typeof(TestSerializationCoverage).Assembly };

        private static EntityFinder.EntityTypeInfo[] Cov() => EntityFinder.FindEntities(Asm, CovScope, false);
        private static EntityFinder.EntityTypeInfo[] Bad() => EntityFinder.FindEntities(Asm, BadScope, false);

        private static SqlDbConnection BuildSource()
        {
            var connection = SqliteDbConnectionFactory.CreateMemory();

            using (var q = connection.GetCreateEntityQuery<CovGroup>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<CovItem>())
                q.Execute();

            var root = new CovGroup { Name = "root", Parent = null };
            using (var q = connection.GetInsertEntityQuery<CovGroup>())
                q.Execute(root);
            var child = new CovGroup { Name = "child", Parent = root };
            using (var q = connection.GetInsertEntityQuery<CovGroup>())
                q.Execute(child);

            var items = new[]
            {
                new CovItem { Owner = root, Name = "alpha", Status = "active", Data = new byte[] { 0x0A, 0x0B, 0x0C }, Count = 10 },
                new CovItem { Owner = child, Name = "beta", Status = null, Data = null, Count = 20 },
            };
            using (var q = connection.GetInsertEntityQuery<CovItem>())
                foreach (var it in items)
                    q.Execute(it);

            return connection;
        }

        // ---- serialization helpers that capture the materialized entities into a list ----

        private static List<object> DrainXml(byte[] xml, EntityFinder.EntityTypeInfo[] types, IBlobAccessor blob = null)
        {
            var list = new List<object>();
            using var ms = new MemoryStream(xml);
            var reader = new XmlEntityReader(types, ms); // XmlEntityReader(Stream) ctor
            if (blob != null)
                reader.BlobAccessor = blob;
            reader.OnEntity += e => list.Add(e);
            reader.Scan();
            return list;
        }

        // Produces a UTF-8 encoded XML document so it can be re-read from a byte stream
        // (the StringWriter/StringBuilder ctors emit a UTF-16 declaration).
        private static byte[] WriteXml(SqlDbConnection source, EntityFinder.EntityTypeInfo[] types, IBlobAccessor blob = null)
        {
            using var ms = new MemoryStream();
            var settings = new System.Xml.XmlWriterSettings { Encoding = new UTF8Encoding(false) };
            var xw = System.Xml.XmlWriter.Create(ms, settings);
            using (var writer = new XmlEntityWriter(xw, true))
            {
                if (blob != null)
                    writer.BlobAccessor = blob;
                var reader = new DbEntityReader(types, source, null);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }
            return ms.ToArray();
        }

        private static byte[] WriteBinary(SqlDbConnection source, EntityFinder.EntityTypeInfo[] types)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryEntityWriter(stream))
            {
                var reader = new DbEntityReader(types, source, null);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }
            return stream.ToArray();
        }

        // =====================================================================
        // FileBlobAccessor
        // =====================================================================

        [Fact]
        public void FileBlobAccessor_Creates_Directory_And_RoundTrips()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ser_cov_blob_" + Guid.NewGuid().ToString("N"));
            try
            {
                // directory does not exist yet -> ctor must create it
                Directory.Exists(dir).Should().BeFalse();
                var accessor = new FileBlobAccessor(dir);
                Directory.Exists(dir).Should().BeTrue();

                var blob = new byte[] { 1, 2, 3, 250, 251, 252 };
                string name = accessor.Save(blob);
                name.Should().NotBeNullOrEmpty();
                File.Exists(Path.Combine(dir, name)).Should().BeTrue();

                accessor.Load(name).Should().Equal(blob);

                // second accessor over the now-existing directory -> ctor's else branch
                var accessor2 = new FileBlobAccessor(dir);
                accessor2.Load(name).Should().Equal(blob);
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void Xml_RoundTrip_Through_Stream_And_FileBlobAccessor()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ser_cov_xmlblob_" + Guid.NewGuid().ToString("N"));
            try
            {
                var blob = new FileBlobAccessor(dir);
                using var source = BuildSource();

                // UTF-8 XML writer + FileBlobAccessor.Save
                byte[] xml = WriteXml(source, Cov(), blob);

                // XmlEntityReader(Stream) ctor + FileBlobAccessor.Load
                var entities = DrainXml(xml, Cov(), blob);

                var alpha = entities.OfType<CovItem>().Single(x => x.Name == "alpha");
                alpha.Data.Should().Equal(new byte[] { 0x0A, 0x0B, 0x0C });
                // defaulted column overwritten by the serialized value
                alpha.Status.Should().Be("active");

                var beta = entities.OfType<CovItem>().Single(x => x.Name == "beta");
                beta.Data.Should().BeNull();
                beta.Status.Should().BeNull();
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        // =====================================================================
        // JSON reader stream constructors (MemoryStream fast-path and CopyTo path)
        // =====================================================================

        [Fact]
        public void Json_Reader_Reads_From_MemoryStream()
        {
            using var source = BuildSource();
            byte[] json;
            using (var ms = new MemoryStream())
            {
                using (var writer = new JsonEntityWriter(ms))
                {
                    var reader = new DbEntityReader(Cov(), source, null);
                    reader.OnTypeStarted += t => writer.Start(t);
                    reader.OnEntity += e => writer.Write(e);
                    reader.Scan();
                }
                json = ms.ToArray();
            }

            var entities = new List<object>();
            using (var ms = new MemoryStream(json))
            {
                var reader = new JsonEntityReader(Cov(), ms);
                reader.OnEntity += e => entities.Add(e);
                reader.Scan();
            }
            entities.OfType<CovItem>().Should().HaveCount(2);
        }

        [Fact]
        public void Json_Reader_Reads_From_NonMemory_Stream()
        {
            string path = Path.Combine(Path.GetTempPath(), "ser_cov_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                using var source = BuildSource();
                using (var fs = File.Create(path))
                using (var writer = new JsonEntityWriter(fs))
                {
                    var reader = new DbEntityReader(Cov(), source, null);
                    reader.OnTypeStarted += t => writer.Start(t);
                    reader.OnEntity += e => writer.Write(e);
                    reader.Scan();
                }

                var entities = new List<object>();
                using (var fs = File.OpenRead(path)) // FileStream -> ReadAll's CopyTo branch
                {
                    var reader = new JsonEntityReader(Cov(), fs);
                    reader.OnEntity += e => entities.Add(e);
                    reader.Scan();
                }
                entities.OfType<CovItem>().Should().HaveCount(2);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        // =====================================================================
        // TextFormatter edge codes
        // =====================================================================

        [Fact]
        public void TextFormatter_Formats_And_Parses_ByteArray_Via_Object()
        {
            var blob = new byte[] { 0x00, 0x10, 0x20, 0xFF };
            TextFormatter.Format((object)blob, out string formatted, out string type).Should().BeTrue();
            type.Should().Be("l");
            ((byte[])TextFormatter.Parse("l", formatted)).Should().Equal(blob);
        }

        [Fact]
        public void TextFormatter_Format_Throws_On_Unsupported_Type()
        {
            Action act = () => TextFormatter.Format((object)TimeSpan.FromMinutes(1), out _, out _);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void TextFormatter_Parse_Throws_On_Unknown_Code()
        {
            Action act = () => TextFormatter.Parse("z", "value");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void TextFormatter_ParseAndConvert_Widens_Int_To_Long()
        {
            // Parse yields Int32; the target type differs, forcing Convert.ChangeType.
            object result = TextFormatter.ParseAndConvert("i", "5", typeof(long));
            result.Should().BeOfType<long>().And.Be(5L);
        }

        // =====================================================================
        // BinaryFormatter error paths
        // =====================================================================

        [Fact]
        public void BinaryFormatter_Write_Throws_On_Null_Writer()
        {
            Action act = () => BinaryFormatter.Write(null, 5);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void BinaryFormatter_Write_Throws_On_Unsupported_Type()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);
            Action act = () => BinaryFormatter.Write(bw, TimeSpan.FromMinutes(1));
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void BinaryFormatter_Read_Throws_On_Null_Reader()
        {
            Action act = () => BinaryFormatter.Read(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void BinaryFormatter_Read_Throws_On_Unknown_Code()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, true))
                    bw.Write((byte)'z');
                payload = ms.ToArray();
            }

            using var reader = new MemoryStream(payload);
            using var br = new BinaryReader(reader, Encoding.UTF8, true);
            Action act = () => BinaryFormatter.Read(br);
            act.Should().Throw<ArgumentException>();
        }

        // =====================================================================
        // Writer guard clauses
        // =====================================================================

        [Fact]
        public void XmlWriter_Guards()
        {
            var sb = new StringBuilder();
            var writer = new XmlEntityWriter(sb);

            writer.Invoking(w => w.Start(null)).Should().Throw<ArgumentNullException>();
            writer.Invoking(w => w.Write(null)).Should().Throw<ArgumentNullException>();
            writer.Invoking(w => w.Write(new CovGroup())).Should().Throw<InvalidOperationException>(); // type not started

            writer.Start(typeof(CovGroup));
            writer.Invoking(w => w.Write(new CovItem())).Should().Throw<ArgumentException>(); // wrong entity type

            writer.Dispose();
            writer.Invoking(w => w.Start(typeof(CovGroup))).Should().Throw<InvalidOperationException>();
            writer.Invoking(w => w.Write(new CovGroup())).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void JsonWriter_Guards()
        {
            using var stream = new MemoryStream();
            var writer = new JsonEntityWriter(stream);

            writer.Invoking(w => w.Start(null)).Should().Throw<ArgumentNullException>();
            writer.Invoking(w => w.Write(null)).Should().Throw<ArgumentNullException>();
            writer.Invoking(w => w.Write(new CovGroup())).Should().Throw<InvalidOperationException>();

            writer.Start(typeof(CovGroup));
            writer.Invoking(w => w.Write(new CovItem())).Should().Throw<ArgumentException>();

            writer.Dispose();
            writer.Invoking(w => w.Start(typeof(CovGroup))).Should().Throw<InvalidOperationException>();
            writer.Invoking(w => w.Write(new CovGroup())).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void BinaryWriter_Guards()
        {
            using var stream = new MemoryStream();
            var writer = new BinaryEntityWriter(stream);

            writer.Invoking(w => w.Start(null)).Should().Throw<ArgumentNullException>();
            writer.Invoking(w => w.Write(null)).Should().Throw<ArgumentNullException>();
            writer.Invoking(w => w.Write(new CovGroup())).Should().Throw<InvalidOperationException>();

            writer.Start(typeof(CovGroup));
            writer.Invoking(w => w.Write(new CovItem())).Should().Throw<ArgumentException>();

            writer.Dispose();
            writer.Invoking(w => w.Start(typeof(CovGroup))).Should().Throw<InvalidOperationException>();
            writer.Invoking(w => w.Write(new CovGroup())).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Constructors_Throw_On_Null_Stream_Or_Buffer()
        {
            ((Action)(() => new BinaryEntityWriter((Stream)null))).Should().Throw<ArgumentNullException>();
            ((Action)(() => new JsonEntityWriter((Stream)null))).Should().Throw<ArgumentNullException>();
            ((Action)(() => new JsonEntityReader(Cov(), (Stream)null))).Should().Throw<ArgumentNullException>();
            ((Action)(() => new JsonEntityReader(Cov(), (string)null))).Should().Throw<ArgumentNullException>();
            ((Action)(() => new BinaryEntityReader(Cov(), (Stream)null))).Should().Throw<ArgumentNullException>();
        }

        // =====================================================================
        // EntityTypeResolver failures
        // =====================================================================

        [Fact]
        public void Reader_Throws_When_Type_Set_Is_Null()
        {
            using var stream = new MemoryStream(new byte[] { 0 });
            ((Action)(() => new BinaryEntityReader(null, stream))).Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Reader_Throws_When_Serialized_Type_Not_In_Set()
        {
            using var source = BuildSource();
            byte[] xml = WriteXml(source, Cov());

            // an empty type set cannot resolve the scope/name in the document
            var empty = new EntityFinder.EntityTypeInfo[0];
            using var ms = new MemoryStream(xml);
            var reader = new XmlEntityReader(empty, ms);
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        // =====================================================================
        // Cancellation
        // =====================================================================

        [Fact]
        public void DbEntityReader_Honours_Cancellation()
        {
            using var source = BuildSource();
            var canceled = new CancellationToken(true);

            var seen = new List<object>();
            var reader = new DbEntityReader(Cov(), source, canceled);
            reader.OnEntity += e => seen.Add(e);
            reader.Invoking(r => r.Scan()).Should().NotThrow();
            // with a pre-canceled token the scan bails out early, never emitting the whole graph
            seen.OfType<CovItem>().Should().HaveCountLessThan(2);
        }

        [Fact]
        public void XmlReader_Honours_Cancellation()
        {
            using var source = BuildSource();
            byte[] xml = WriteXml(source, Cov());

            var seen = new List<object>();
            using var ms = new MemoryStream(xml);
            var reader = new XmlEntityReader(Cov(), ms, null, new CancellationToken(true));
            reader.OnEntity += e => seen.Add(e);
            reader.Scan();
            seen.Should().BeEmpty();
        }

        [Fact]
        public void BinaryReader_Honours_Cancellation()
        {
            using var source = BuildSource();
            byte[] payload = WriteBinary(source, Cov());

            var seen = new List<object>();
            using var ms = new MemoryStream(payload);
            var reader = new BinaryEntityReader(Cov(), ms, new CancellationToken(true));
            reader.OnEntity += e => seen.Add(e);
            reader.Scan();
            seen.Should().BeEmpty();
        }

        [Fact]
        public void JsonReader_Honours_Cancellation()
        {
            using var source = BuildSource();
            byte[] json;
            using (var stream = new MemoryStream())
            {
                using (var writer = new JsonEntityWriter(stream))
                {
                    var reader = new DbEntityReader(Cov(), source, null);
                    reader.OnTypeStarted += t => writer.Start(t);
                    reader.OnEntity += e => writer.Write(e);
                    reader.Scan();
                }
                json = stream.ToArray();
            }

            var seen = new List<object>();
            var jreader = new JsonEntityReader(Cov(), json, new CancellationToken(true));
            jreader.OnEntity += e => seen.Add(e);
            jreader.Scan();
            // cancellation is checked after the first entity of the first type is raised
            seen.OfType<CovItem>().Should().HaveCountLessThan(2);
        }

        // =====================================================================
        // DbEntityReader frame paging and invalid self-reference
        // =====================================================================

        [Fact]
        public void DbEntityReader_Pages_Through_Small_Frames()
        {
            using var source = BuildSource();

            var seen = new List<object>();
            var reader = new DbEntityReader(Cov(), source, null) { FrameSize = 1 };
            reader.OnEntity += e => seen.Add(e);
            reader.Scan();

            // 2 groups + 2 items must all be emitted even with a frame size of one
            seen.OfType<CovGroup>().Should().HaveCount(2);
            seen.OfType<CovItem>().Should().HaveCount(2);
        }

        [Fact]
        public void DbEntityReader_Rejects_Multiple_Self_References()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var reader = new DbEntityReader(Bad(), connection, null);
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        // =====================================================================
        // XML reader specifics
        // =====================================================================

        [Fact]
        public void XmlReader_Throws_When_Property_Id_Exceeds_Maximum()
        {
            using var source = BuildSource();
            byte[] xml = WriteXml(source, Cov());

            using var ms = new MemoryStream(xml);
            var reader = new XmlEntityReader(Cov(), ms) { MaximumPropertiesPerEntity = 1 };
            reader.Invoking(r => r.Scan()).Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Xml_StringWriter_Constructor_Produces_Readable_Document()
        {
            using var source = BuildSource();

            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            using (var writer = new XmlEntityWriter(sw)) // XmlEntityWriter(StringWriter) ctor
            {
                var reader = new DbEntityReader(Cov(), source, null);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }

            var list = new List<object>();
            var xmlReader = new XmlEntityReader(Cov(), sb.ToString()); // string ctor matches UTF-16 output
            xmlReader.OnEntity += e => list.Add(e);
            xmlReader.Scan();
            list.OfType<CovItem>().Should().HaveCount(2);
        }

        [Fact]
        public void XmlReader_Applies_Default_Value_Before_Column_Values()
        {
            // "beta" stores Status = null; the writer emits a null, but the materializer must
            // first apply the "pending" default and then overwrite it with the null value.
            using var source = BuildSource();
            byte[] xml = WriteXml(source, Cov());

            var entities = DrainXml(xml, Cov());
            entities.OfType<CovItem>().Single(x => x.Name == "beta").Status.Should().BeNull();
        }

        [Fact]
        public void XmlReader_Tolerates_Indented_Document()
        {
            // pretty-printed XML introduces whitespace text nodes between elements;
            // the reader must accumulate/ignore them without corrupting the entities.
            using var source = BuildSource();

            using var ms = new MemoryStream();
            var settings = new System.Xml.XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
            var xw = System.Xml.XmlWriter.Create(ms, settings);
            using (var writer = new XmlEntityWriter(xw, true))
            {
                var reader = new DbEntityReader(Cov(), source, null);
                reader.OnTypeStarted += t => writer.Start(t);
                reader.OnEntity += e => writer.Write(e);
                reader.Scan();
            }

            var entities = DrainXml(ms.ToArray(), Cov());
            entities.OfType<CovItem>().Should().HaveCount(2);
            entities.OfType<CovGroup>().Should().HaveCount(2);
        }

        // =====================================================================
        // JSON reader guards and malformed input
        // =====================================================================

        [Fact]
        public void JsonReader_Scan_Throws_After_Dispose()
        {
            var reader = new JsonEntityReader(Cov(), "{}");
            reader.Dispose();
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void JsonReader_Throws_On_Malformed_Root()
        {
            var reader = new JsonEntityReader(Cov(), "[]"); // root is not an object
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void JsonReader_Skips_Unknown_Leading_Property()
        {
            // a property before the types array must be skipped, not choke the reader
            string json = "{\"junk\":123,\"" + JsonEntityWriter.TypesProperty + "\":[]}";
            var reader = new JsonEntityReader(Cov(), json);
            var seen = new List<object>();
            reader.OnEntity += e => seen.Add(e);
            reader.Invoking(r => r.Scan()).Should().NotThrow();
            seen.Should().BeEmpty();
        }

        [Fact]
        public void JsonReader_Throws_When_Types_Property_Is_Not_An_Array()
        {
            string json = "{\"" + JsonEntityWriter.TypesProperty + "\":5}";
            var reader = new JsonEntityReader(Cov(), json);
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void JsonReader_Skips_Unknown_Property_Within_Type()
        {
            // an unknown property inside a type object must be skipped
            string json =
                "{\"" + JsonEntityWriter.TypesProperty + "\":[{" +
                "\"" + JsonEntityWriter.ScopeProperty + "\":\"" + CovScope + "\"," +
                "\"junk\":true," +
                "\"" + JsonEntityWriter.NameProperty + "\":\"cov_group\"," +
                "\"" + JsonEntityWriter.EntitiesProperty + "\":[]}]}";

            var started = new List<Type>();
            var reader = new JsonEntityReader(Cov(), json);
            reader.OnTypeStarted += t => started.Add(t);
            reader.Invoking(r => r.Scan()).Should().NotThrow();
            started.Should().ContainSingle().Which.Should().Be(typeof(CovGroup));
        }

        // =====================================================================
        // Binary reader guards and malformed input
        // =====================================================================

        [Fact]
        public void BinaryReader_Scan_Throws_After_Dispose()
        {
            using var stream = new MemoryStream(new byte[] { 0 });
            var reader = new BinaryEntityReader(Cov(), stream);
            reader.Dispose();
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void BinaryReader_Throws_On_Entity_Before_Type()
        {
            using var stream = new MemoryStream(new byte[] { 2 }); // 2 == EntityMarker, no preceding type
            var reader = new BinaryEntityReader(Cov(), stream);
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void BinaryReader_Throws_On_Unknown_Marker()
        {
            using var stream = new MemoryStream(new byte[] { 99 }); // not a valid marker
            var reader = new BinaryEntityReader(Cov(), stream);
            reader.Invoking(r => r.Scan()).Should().Throw<InvalidOperationException>();
        }

        // =====================================================================
        // DbEntityReader regular-entity cancellation and DbEntityWriter guards
        // =====================================================================

        [Fact]
        public void DbEntityReader_Cancellation_Stops_Regular_Entities()
        {
            using var source = BuildSource();
            using var cts = new CancellationTokenSource();

            var seen = new List<object>();
            var reader = new DbEntityReader(Cov(), source, cts.Token);
            reader.OnEntity += e =>
            {
                seen.Add(e);
                if (e is CovItem)   // once a regular entity is reached, cancel mid-stream
                    cts.Cancel();
            };
            reader.Scan();

            // the self-referenced groups complete, then the item stream stops after the first row
            seen.OfType<CovItem>().Should().ContainSingle();
        }

        [Fact]
        public void DbEntityWriter_Guards()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            using var writer = new DbEntityWriter(connection);

            writer.Invoking(w => w.Start(null)).Should().Throw<ArgumentNullException>();
            writer.Invoking(w => w.Write(null)).Should().Throw<ArgumentNullException>();
        }
    }
}
