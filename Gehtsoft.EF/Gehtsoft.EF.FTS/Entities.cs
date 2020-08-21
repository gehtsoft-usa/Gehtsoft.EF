using System.Data;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.FTS
{
    [Entity(Scope = "fts", Table = "fts_words")]
    public class FtsWordEntity
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, Autoincrement = true, PrimaryKey = true)]
        public int ID { get; set; } = -1;
        [EntityProperty(Field = "word", DbType = DbType.String, Size = 32, Sorted = true)]
        public string Word { get; set; }

    }

    public class FtsWordEntityCollection : EntityCollection<FtsWordEntity>
    {
        
    }

    [Entity(Scope = "fts", Table = "fts_objects")]
    public class FtsObjectEntity
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, Autoincrement = true, PrimaryKey = true)]
        public int ID { get; set; } = -1;
        [EntityProperty(Field = "object_type", DbType = DbType.String, Size = 32, Sorted = true)]
        public string ObjectType { get; set; }
        [EntityProperty(Field = "object_id", DbType = DbType.String, Size = 32, Sorted = true)]
        public string ObjectID { get; set; }
        [EntityProperty(Field = "sorter", DbType = DbType.String, Size = 32, Sorted = true)]
        public string Sorter { get; set; }

    }

    public class FtsObjectEntityCollection : EntityCollection<FtsObjectEntity>
    {
        
    }

    [Entity(Scope = "fts", Table = "fts_words2objects")]
    public class FtsWord2ObjectEntity
    {
        [EntityProperty(Field = "id", DbType = DbType.Int32, Autoincrement = true, PrimaryKey = true)]
        public int ID { get; set; } = -1;
        [EntityProperty(Field = "word", ForeignKey = true)]
        public FtsWordEntity Word { get; set; }
        [EntityProperty(Field = "object", ForeignKey = true)]
        public FtsObjectEntity Object { get; set; }
    }

}
