using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Base class for all Sql statements
    /// </summary>
    public abstract class SqlStatement : IEquatable<SqlStatement>
    {
        public SqlCodeDomBuilder CodeDomBuilder { get; } = null;
        /// <summary>
        /// The types of the statements
        /// </summary>
        public enum StatementId
        {
            Select,
            Insert,
        };

        internal class EntityEntry
        {
            private string mEntityName;
            private string mAlias;
            private EntityDescriptor mEntityDescriptor;

            internal EntityEntry(string entityName, Type entityType, string alias, EntityDescriptor entityDescriptor)
            {
                mEntityName = entityName;
                EntityType = entityType;
                mAlias = alias;
                mEntityDescriptor = entityDescriptor;
            }

            public Type EntityType { get; }
            public string ReferenceName
            {
                get
                {
                    return mAlias ?? mEntityName;
                }
            }

            public EntityDescriptor EntityDescriptor
            {
                get
                {
                    return mEntityDescriptor;
                }
            }
        }

        internal class EntityEntrysCollection : IReadOnlyList<EntityEntry>
        {
            private readonly List<EntityEntry> mList = new List<EntityEntry>();

            internal EntityEntrysCollection()
            {

            }

            /// <summary>
            /// Returns the EntityEntry by its index
            /// </summary>
            /// <param name="index"></param>
            /// <returns></returns>
            public EntityEntry this[int index] => ((IReadOnlyList<EntityEntry>)mList)[index];

            /// <summary>
            /// Returns the number of EntityEntry
            /// </summary>
            public int Count => ((IReadOnlyCollection<EntityEntry>)mList).Count;

            public IEnumerator<EntityEntry> GetEnumerator()
            {
                return ((IEnumerable<EntityEntry>)mList).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)mList).GetEnumerator();
            }

            internal void Add(EntityEntry entity) => mList.Add(entity);

            internal bool Exists(string name) => mList.Where(t => t.ReferenceName == name).SingleOrDefault() != null;

            internal EntityEntry Find(string name) => mList.Where(t => t.ReferenceName == name).SingleOrDefault();
        }

        internal class AliasEntry
        {
            private string mAliasName;

            internal AliasEntry(string aliasName, SqlBaseExpression expression)
            {
                mAliasName = aliasName;
                Expression = expression;
            }

            public SqlBaseExpression Expression { get; }
            public string AliasName
            {
                get
                {
                    return mAliasName;
                }
            }
        }

        internal class AliasEntrysCollection : IReadOnlyList<AliasEntry>
        {
            private readonly List<AliasEntry> mList = new List<AliasEntry>();

            internal AliasEntrysCollection()
            {

            }

            /// <summary>
            /// Returns the AliasEntry by its index
            /// </summary>
            /// <param name="index"></param>
            /// <returns></returns>
            public AliasEntry this[int index] => ((IReadOnlyList<AliasEntry>)mList)[index];

            /// <summary>
            /// Returns the number of AliasEntry
            /// </summary>
            public int Count => ((IReadOnlyCollection<AliasEntry>)mList).Count;

            public IEnumerator<AliasEntry> GetEnumerator()
            {
                return ((IEnumerable<AliasEntry>)mList).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)mList).GetEnumerator();
            }

            internal void Add(AliasEntry alias) => mList.Add(alias);

            internal bool Exists(string aliasName) => mList.Where(t => t.AliasName == aliasName).SingleOrDefault() != null;

            internal AliasEntry Find(string aliasName) => mList.Where(t => t.AliasName == aliasName).SingleOrDefault();
        }

        /// <summary>
        /// Collection of aliases
        /// </summary>
        internal AliasEntrysCollection AliasEntrys { get; } = new AliasEntrysCollection();

        /// <summary>
        /// Add alias
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="expression"></param>
        internal string AddAliasEntry(string alias, SqlBaseExpression expression)
        {
            if (alias == null)
            {
                if (expression is SqlField field)
                {
                    alias = field.Name;
                }
                else
                {
                    alias = ($"autocolumn{AliasEntrys.Count + 1}");
                }
            }
            if (AliasEntrys.Exists(alias)) throw new Exception();
            AliasEntrys.Add(new AliasEntry(alias, expression));
            return alias;
        }

        /// <summary>
        /// Add entity
        /// </summary>
        /// <param name="name"></param>
        /// <param name="alias"></param>
        internal void AddEntityEntry(string name, string alias)
        {
            Type entityType = CodeDomBuilder.EntityByName(name);
            if (entityType == null) throw new Exception();
            EntityEntrys.Add(new EntityEntry(name, entityType, alias, AllEntities.Inst[entityType]));
        }

        /// <summary>
        /// Collection of entities declared in FROM
        /// </summary>
        internal EntityEntrysCollection EntityEntrys { get; } = new EntityEntrysCollection();

        internal bool IgnoreAlias { get; set; } = false;

        /// <summary>
        /// The source where the statement is located
        /// </summary>
        public string Source { get; set; }
        /// <summary>
        /// The first line of the statement
        /// </summary>
        public int Line { get; set; }
        /// <summary>
        /// Position of the statement start in the line
        /// </summary>
        public int Position { get; set; }
        /// <summary>
        /// Type of the statement
        /// </summary>
        public StatementId Id { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id"></param>
        /// <param name="currentSource"></param>
        /// <param name="line"></param>
        /// <param name="position"></param>
        protected SqlStatement(SqlCodeDomBuilder builder, StatementId id, string currentSource, int line, int position)
        {
            CodeDomBuilder = builder;
            Id = id;
            Source = currentSource;
            Line = line;
            Position = position;
        }

        public virtual bool Equals(SqlStatement other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.Id == other.Id);
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlStatement item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public bool HasAggregateFunctions(SqlBaseExpression expression)
        {
            bool retval = false;

            if (expression is SqlField field)
            {
                retval = false;
            }
            else if (expression is SqlAggrFunc aggrFunc)
            {
                retval = true;
            }
            else if (expression is SqlBinaryExpression binaryExpression)
            {
                bool isAggregateLeft = HasAggregateFunctions(binaryExpression.LeftOperand);
                bool isAggregateRight = HasAggregateFunctions(binaryExpression.RightOperand);
                retval = isAggregateLeft || isAggregateRight;
            }
            else if (expression is SqlConstant constant)
            {
                retval = false;
            }
            else if (expression is SqlUnarExpression unar)
            {
                retval = HasAggregateFunctions(unar.Operand);
            }
            else if (expression is SqlCallFuncExpression callFunc)
            {
                retval = false;
                foreach (SqlBaseExpression paramExpression in callFunc.Parameters)
                {
                    if (HasAggregateFunctions(paramExpression))
                    {
                        retval = true;
                        break;
                    }
                }
            }
            return retval;
        }
    }
}
