using System;

namespace Gehtsoft.EF.Mapper
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MapPropertyAttribute : Attribute
    {
        public string Name { get; set; }

        public MapFlag MapFlags { get; set; } = MapFlag.None;

        public bool IgnoreToModel { get; set; } = false;

        public bool IgnoreFromModel { get; set; } = false;

        public MapPropertyAttribute()
        {

        }

        public MapPropertyAttribute(string name)
        {
            Name = name;
        }

        public MapPropertyAttribute(string name, MapFlag mapFlags)
        {
            Name = name;
            MapFlags = mapFlags;
        }

        public MapPropertyAttribute(MapFlag mapFlags)
        {
            MapFlags = mapFlags;
        }
    }
}