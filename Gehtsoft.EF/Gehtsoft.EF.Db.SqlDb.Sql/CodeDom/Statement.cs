﻿using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Base class for all statements
    /// </summary>
    internal abstract class Statement
    {
        internal Expression OnContinue { get; set; } = null;
        internal SqlCodeDomBuilder CodeDomBuilder { get; } = null;
        /// <summary>
        /// The types of the statements
        /// </summary>
        internal protected enum StatementType
        {
            Sql,
            Set,
            Declare,
            Exit,
            If,
            Continue,
            Break,
            Loop,
            Block,
            Switch,
            AddField,
            AddRow,
            DeclareCursor,
            OpenCursor,
            CloseCursor,
            Assign,
            DummyPersist,
            Import
        };
        /// <summary>
        /// Type of the statement
        /// </summary>
        internal StatementType Type { get; }

        internal class EntityEntry
        {
            private readonly string mEntityName;
            private readonly string mAlias;

            internal EntityEntry(string entityName, Type entityType, string alias, EntityDescriptor entityDescriptor)
            {
                mEntityName = entityName;
                EntityType = entityType;
                mAlias = alias;
                EntityDescriptor = entityDescriptor;
            }

            internal Type EntityType { get; }
            internal string ReferenceName
            {
                get
                {
                    return mAlias ?? mEntityName;
                }
            }

            internal EntityDescriptor EntityDescriptor { get; }
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

            internal bool Exists(string name) => mList.SingleOrDefault(t => t.ReferenceName == name) != null;

            internal EntityEntry Find(string name) => mList.SingleOrDefault(t => t.ReferenceName == name);
        }

        internal class AliasEntry
        {
            internal AliasEntry(string aliasName, SqlBaseExpression expression)
            {
                AliasName = aliasName;
                Expression = expression;
            }

            internal SqlBaseExpression Expression { get; }
            internal string AliasName { get; }
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

            internal bool Exists(string aliasName) => mList.SingleOrDefault(t => t.AliasName == aliasName) != null;

            internal AliasEntry Find(string aliasName) => mList.SingleOrDefault(t => t.AliasName.Equals(aliasName, StringComparison.InvariantCultureIgnoreCase));
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

        private protected Statement(SqlCodeDomBuilder builder, StatementType type)
        {
            CodeDomBuilder = builder;
            Type = type;
        }

        internal abstract Expression ToLinqWxpression();

        internal static bool HasAggregateFunctions(SqlBaseExpression expression)
        {
            bool retval = false;

            if (expression is SqlField)
            {
                retval = false;
            }
            else if (expression is SqlAggrFunc)
            {
                retval = true;
            }
            else if (expression is SqlBinaryExpression binaryExpression)
            {
                bool isAggregateLeft = HasAggregateFunctions(binaryExpression.LeftOperand);
                bool isAggregateRight = HasAggregateFunctions(binaryExpression.RightOperand);
                retval = isAggregateLeft || isAggregateRight;
            }
            else if (expression is SqlConstant)
            {
                retval = false;
            }
            else if (expression is SqlUnaryExpression unar)
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

        internal static bool IsCalculable(SqlBaseExpression expression)
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
            else if (expression is AssignExpression)
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
            else if (expression is GetRow getRow)
            {
                retval = IsCalculable(getRow.RowSetParameter) && IsCalculable(getRow.IndexParameter);
            }
            else if (expression is GetField getField)
            {
                retval = IsCalculable(getField.RowParameter) && IsCalculable(getField.NameParameter);
            }
            else if (expression is NewRowSet)
            {
                retval = true;
            }
            else if (expression is NewRow)
            {
                retval = true;
            }
            else if (expression is Fetch fetch)
            {
                retval = IsCalculable(fetch.Parameter);
            }
            else if (expression is SqlSelectExpression)
            {
                retval = true;
            }
            else if (expression is SqlUnaryExpression unar)
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

        internal static ResultTypes GetResultTypeByName(string name)
        {
            switch (name)
            {
                case "STRING":
                    return ResultTypes.String;
                case "INTEGER":
                    return ResultTypes.Integer;
                case "DOUBLE":
                    return ResultTypes.Double;
                case "BOOLEAN":
                    return ResultTypes.Boolean;
                case "DATETIME":
                    return ResultTypes.DateTime;
                case "ROW":
                    return ResultTypes.Row;
                case "ROWSET":
                    return ResultTypes.RowSet;
            }
            return ResultTypes.Unknown;
        }
    }
}
