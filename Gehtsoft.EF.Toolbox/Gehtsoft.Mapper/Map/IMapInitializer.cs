namespace Gehtsoft.EF.Mapper
{
    public interface IMapInitializer
    {
        void SourceToModel(IMap map);
        void ModelToSource(IMap map);
    }
}