using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Mapper;
using Gehtsoft.EF.Mapper.Validator;
using Gehtsoft.EF.Validator;
using Gehtsoft.Validator;
using NUnit.Framework;

namespace Gehtsoft.EF.Toolbox.Test
{
    [TestFixture]
    public class Example
    {
        public class Source
        {
            public string A { get; set; }
        }

        public class Destination
        {
            public string A { get; }
            public Destination(string a)
            {
                A = a;
            }
        }

        [Explicit]
        [Test]
        public void Debug()
        {
            var map = MapFactory.CreateMap<Source, Destination>();
            map.Factory = src => new Destination(src.A);

            Source[] s = { new Source() { A = "X" } };
            Destination[] d = MapFactory.Map<Source[], Destination[]>(s);

            Console.WriteLine($"{d}");
        }
    }
}
