using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Base class for all statements
    /// </summary>
    public abstract class Statement : IEquatable<Statement>
    {
        public SqlCodeDomBuilder CodeDomBuilder { get; } = null;
        /// <summary>
        /// The types of the statements
        /// </summary>
        public enum StatementType
        {
            Sql,
            Set,
            Declare,
            Exit
        };
        /// <summary>
        /// Type of the statement
        /// </summary>
        public StatementType Type { get; }

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
        /// Collection of entities declared in FROM
        /// </summary>
        internal EntityEntrysCollection EntityEntrys { get; } = new EntityEntrysCollection();

        internal bool IgnoreAlias { get; set; } = false;

        protected Statement(SqlCodeDomBuilder builder, StatementType type)
        {
            CodeDomBuilder = builder;
            Type = type;
        }

        public virtual bool Equals(Statement other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return (this.Type == other.Type);
        }

        public override bool Equals(object obj)
        {
            if (obj is Statement item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool HasAggregateFunctions(SqlBaseExpression expression)
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

        public static bool IsCalculable(SqlBaseExpression expression)
        {
            bool retval = false;

            if (expression is SqlField)
            {
                retval = false;
            }
            else if (expression is SqlAggrFunc)
            {
                retval = false;
            }
            else if (expression is SqlBinaryExpression binaryExpression)
            {
                bool isCalculableLeft = IsCalculable(binaryExpression.LeftOperand);
                bool isCalculableRight = IsCalculable(binaryExpression.RightOperand);
                retval = isCalculableLeft && isCalculableRight;
            }
            else if (expression is SqlConstant)
            {
                retval = true;
            }
            else if (expression is GlobalParameter)
            {
                retval = true;
            }
            else if (expression is GetLastResult)
            {
                retval = true;
            }
            else if (expression is GetRowsCount getRowsCount)
            {
                retval = IsCalculable(getRowsCount.Parameter);
            }
            else if (expression is SqlSelectExpression)
            {
                retval = true;
            }
            else if (expression is SqlUnarExpression unar)
            {
                retval = IsCalculable(unar.Operand);
            }
            else if (expression is SqlCallFuncExpression callFunc)
            {
                retval = false;
                foreach (SqlBaseExpression paramExpression in callFunc.Parameters)
                {
                    if (IsCalculable(paramExpression))
                    {
                        retval = true;
                        break;
                    }
                }
            }
            return retval;
        }

        public static ResultTypes GetResultTypeByName(string name)
        {
            ResultTypes resultType = ResultTypes.Unknown;
            switch (name)
            {
                case "STRING":
                    resultType = ResultTypes.String;
                    break;
                case "INTEGER":
                    resultType = ResultTypes.Integer;
                    break;
                case "DOUBLE":
                    resultType = ResultTypes.Double;
                    break;
                case "BOOLEAN":
                    resultType = ResultTypes.Boolean;
                    break;
                case "DATETIME":
                    resultType = ResultTypes.DateTime;
                    break;
                case "ROW":
                    resultType = ResultTypes.Row;
                    break;
                case "ROWSET":
                    resultType = ResultTypes.RowSet;
                    break;
            }
            return resultType;
        }
    }
}
