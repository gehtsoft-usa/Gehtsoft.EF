using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple =  true)]
    public class MongoIndexAttribute : Attribute
    {
        public string Key { get; set; }

        public MongoIndexAttribute()
        {
            
        }

        public MongoIndexAttribute(string key)
        {
            Key = key;
        }
    }
}
