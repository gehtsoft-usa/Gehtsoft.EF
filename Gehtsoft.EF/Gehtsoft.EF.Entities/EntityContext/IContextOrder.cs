namespace Gehtsoft.EF.Entities.Context
{
    public interface IContextOrder
    {
        IContextOrder Add(string name, SortDir sortDir = SortDir.Asc);
    }
}