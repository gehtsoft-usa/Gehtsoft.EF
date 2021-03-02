using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    public class CompositeIndex
    {
        public class Field
        {
            public string Name { get; }
            public SortDir Direction { get; }

            public Field(string name)
            {
                Name = name;
                Direction = SortDir.Asc;
            }
            public Field(string name, SortDir direction)
            {
                Name = name;
                Direction = direction;
            }
        }

        public Type EntityType { get; }

        public string Name { get;  }

        private readonly List<Field> mFields = new List<Field>();


        private EntityDescriptor mEntityInfo;

        public IReadOnlyList<Field> Fields => mFields;

        public CompositeIndex(string name) : this(null, name)
        {

        }

        public CompositeIndex(Type entityType, string name)
        {
            EntityType = entityType;
            if (entityType != null)
                mEntityInfo = AllEntities.Inst[entityType, true];
            Name = name;
        }

        public void Add(string name) => Add(name, SortDir.Asc);

        public void Add(string name, SortDir direction)
        {
            if (mEntityInfo != null)
            {
                for (int i = 0; i < mEntityInfo.TableDescriptor.Count; i++)
                {
                    var column = mEntityInfo.TableDescriptor[i];
                    if (column.ID == name && column.ID != column.Name)
                    {
                        name = column.Name;
                        break;
                    }
                }
            }

            mFields.Add(new Field(name, direction));
        }
    }
}
