using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor
{
    public class GenericEntitySortOrder
    {
        public string Path { get; }
        public SortDir Direction { get; }

        public SortDir ReversDirection => Direction == SortDir.Asc ? SortDir.Desc : SortDir.Asc;

        public SortDir GetDirection(bool reverse = false) => reverse ? ReversDirection : Direction;

        public GenericEntitySortOrder(string path)
        {
            Path = path;
            Direction = SortDir.Asc;
        }

        public GenericEntitySortOrder(string path, SortDir direction)
        {
            Path = path;
            Direction = direction;
        }
    }
}
