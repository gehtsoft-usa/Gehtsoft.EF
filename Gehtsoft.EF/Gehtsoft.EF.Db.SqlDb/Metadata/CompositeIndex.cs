using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
        /// The list of database drivers (by <see cref="UniversalSqlDbFactory"/> name, e.g.
        /// <see cref="UniversalSqlDbFactory.MSSQL"/>, <see cref="UniversalSqlDbFactory.MYSQL"/>)
        /// for which this index must **not** be created.
        ///
        /// Use it to declare, explicitly and self-documenting, that an index is intentionally
        /// skipped on drivers that cannot build it — most notably a functional index on a driver
        /// where <see cref="SqlDbLanguageSpecifics.SupportFunctionsInIndexes"/> is `false`
        /// (MS SQL Server, MySQL). On an excluded driver the index is silently skipped; on any
        /// other driver that still cannot build it, an <see cref="EfSqlException"/>
        /// (<see cref="EfExceptionCode.FeatureNotSupported"/>) is thrown.
        ///
        /// `null` or empty means the index applies to every driver.
        /// </summary>
        public string[] ExcludeFor { get; set; }

        /// <summary>
        /// Returns `true` when at least one field of the index applies a function (an expression
        /// index).
        /// </summary>
        public bool HasFunction
        {
            get
            {
                for (int i = 0; i < mFields.Count; i++)
                    if (mFields[i].Function != null)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Returns `true` when this index must be skipped for the driver with the specified
        /// identifier (see <see cref="SqlDbLanguageSpecifics.DbName"/>), i.e. when
        /// <see cref="ExcludeFor"/> contains that identifier.
        /// </summary>
        /// <param name="dbName">The driver identifier to test.</param>
        public bool IsExcludedFor(string dbName)
        {
            if (ExcludeFor == null || dbName == null)
                return false;
            for (int i = 0; i < ExcludeFor.Length; i++)
                if (string.Equals(ExcludeFor[i], dbName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

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

        [ExcludeFromCodeCoverage]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mFields).GetEnumerator();
        }
    }
}
