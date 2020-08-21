namespace Gehtsoft.EF.Mapper
{
    public interface IPropertyMapping
    {
        IMappingTarget Target { get; set; }
        IMappingSource Source { get; set; }
        IMappingPredicate WhenPredicate { get; set; }
        MapFlag MapFlag { get; set; }

        void Map(object source, object destination);
        void Map(object source, object destination, bool ignoreNull);
    }
}