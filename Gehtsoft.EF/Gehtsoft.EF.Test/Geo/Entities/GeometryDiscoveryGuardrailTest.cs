using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.Entities
{
    /// <summary>
    /// A [GeometryEntityProperty] on a type that is neither byte[] nor handled by any registered codec
    /// must fail discovery with a clear error. (A string is never a valid geometry object type, so this
    /// holds whether or not the NTS codec happens to be registered.)
    /// </summary>
    public class GeometryDiscoveryGuardrailTest
    {
        [Entity(Scope = "geoguard", Table = "geo_guard")]
        public class GeoGuardOwner
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape")]
            public string Shape { get; set; }
        }

        [Fact]
        public void ObjectGeometry_WithoutHandlingCodec_FailsDiscovery()
        {
            Action act = () => { var _ = AllEntities.Inst[typeof(GeoGuardOwner)].TableDescriptor; };

            act.Should().Throw<EfSqlException>()
               .Which.ErrorCode.Should().Be(EfExceptionCode.GeometryCodecNotFound);
        }
    }
}
