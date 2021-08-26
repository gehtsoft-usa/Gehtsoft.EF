using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    /// <summary>
    /// The information about a composite index.
    /// </summary>
    public class CompositeIndex : IEnumerable<CompositeIndex.Field>
    {
        /// <summary>
        /// An indexed field
        /// </summary>
        public class Field
        {
            /// <summary>
            /// The function or `null` if not function is applied
            /// </summary>
            public SqlFunctionId? Function { get; }
            /// <summary>
            /// The name of the field
            /// </summary>
            public string Name { get; }
            /// <summary>
            /// The sorting direction
            /// </summary>
            public SortDir Direction { get; }

            internal Field(string name) : this(null, name, SortDir.Asc)
            {
            }

            internal Field(string name, SortDir direction) : this(null, name, direction)
            {
            }

            internal Field(SqlFunctionId? function, string name) : this(function, name, SortDir.Asc)
            {
            }

            internal Field(SqlFunctionId? function, string name, SortDir direction)
            {
                Function = function;
                Name = name;
                Direction = direction;
            }
        }

        /// <summary>
        /// The entity type for which the index is created
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// The name of the index.
        ///
        /// The associated database object will be named as //entity name//_//index name//.
        /// </summary>
        public string Name { get; }

        private readonly List<Field> mFields = new List<Field>();

        private readonly EntityDescriptor mEntityInfo;

        /// <summary>
        /// The list of index fields
        /// </summary>
        public IReadOnlyList<Field> Fields => mFields;

        /// <summary>
        /// The flag defining the behavior in case the target database
        /// does not support functions in the index.
        ///
        /// If flag is `true` the exception will be thrown in case functions aren't supported.
        ///
        /// If flag is `false` the index won't be created.
        ///
        /// To check whether the database supports functions in the indexes use
        /// <see cref="SqlDbLanguageSpecifics.SupportFunctionsInIndexes">SqlDbLanguageSpecifics.SupportFunctionsInIndexes</see> flag.
        /// </summary>
        public bool FailIfUnsupported { get; set; }

        /// <summary>
        /// Constructor for an index not associated with the entity.
        /// </summary>
        /// <param name="name"></param>
        public CompositeIndex(string name) : this(null, name)
        {
        }

        /// <summary>
        /// Constructor for an index associated with the entity.
        /// </summary>
        /// <param name="entityType"></param>
        /// <param name="name"></param>
        public CompositeIndex(Type entityType, string name)
        {
            EntityType = entityType;
            if (entityType != null)
                mEntityInfo = AllEntities.Inst[entityType, true];
            Name = name;
        }

        /// <summary>
        /// Adds a column to the index
        /// </summary>
        /// <param name="name"></param>
        public void Add(string name) => Add(null, name, SortDir.Asc);

        /// <summary>
        /// Adds a function applied to a column to the index.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="name"></param>
        public void Add(SqlFunctionId function, string name) => Add(function, name, SortDir.Asc);

        /// <summary>
        /// Adds a column with the specified sorting direction.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="direction"></param>
        public void Add(string name, SortDir direction) => Add(null, name, direction);

        /// <summary>
        /// Adds a function applied to a column with the specified sorting direction.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="name"></param>
        /// <param name="direction"></param>
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

        [DocgenIgnore]
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
