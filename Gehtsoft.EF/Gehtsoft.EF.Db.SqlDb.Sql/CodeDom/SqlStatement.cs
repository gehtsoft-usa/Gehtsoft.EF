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
    public abstract class SqlStatement : Statement
    {
        /// <summary>
        /// The types of the statements
        /// </summary>
        public enum StatementId
        {
            Select,
            Insert,
            Update,
            Delete,
        };

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
            : base(builder, StatementType.Sql)
        {
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

        public override bool Equals(Statement obj)
        {
            if (obj is SqlStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}
