using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    public class CompositeIndex : IEnumerable<CompositeIndex.Field>
    {
        public class Field
        {
            public SqlFunctionId? Function { get; }
            public string Name { get; }
            public SortDir Direction { get; }

            public Field(string name) : this(null, name, SortDir.Asc)
            {
            }

            public Field(string name, SortDir direction) : this(null, name, direction)
            {
            }

            public Field(SqlFunctionId? function, string name) : this(function, name, SortDir.Asc)
            {
            }

            public Field(SqlFunctionId? function, string name, SortDir direction)
            {
                Function = function;
                Name = name;
                Direction = direction;
            }
        }

        public Type EntityType { get; }

        public string Name { get; }

        private readonly List<Field> mFields = new List<Field>();

        private readonly EntityDescriptor mEntityInfo;

        public IReadOnlyList<Field> Fields => mFields;

        public bool FailIfUnsupported { get; set; }

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

        public void Add(string name) => Add(null, name, SortDir.Asc);

        public void Add(SqlFunctionId function, string name) => Add(function, name, SortDir.Asc);

        public void Add(string name, SortDir direction) => Add(null, name, direction);

        public void Add(SqlFunctionId? function, string name, SortDir direction)
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

            mFields.Add(new Field(function, name, direction));
        }

        public IEnumerator<Field> GetEnumerator()
        {
            return ((IEnumerable<Field>)mFields).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mFields).GetEnumerator();
        }
    }
}
