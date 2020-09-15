using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Collection of statements with run environment
    /// </summary>
    public class StatementSetEnvironment : IReadOnlyList<Statement>, IEquatable<StatementSetEnvironment>, IStatementSetEnvironment
    {
        private readonly List<Statement> mCollection = new List<Statement>();

        public Statement this[int index] => ((IReadOnlyList<Statement>)mCollection)[index];

        public int Count => ((IReadOnlyCollection<Statement>)mCollection).Count;

        public IEnumerator<Statement> GetEnumerator()
        {
            return ((IEnumerable<Statement>)mCollection).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mCollection).GetEnumerator();
        }

        internal StatementSetEnvironment()
        {

        }

        internal void Add(Statement statement) => mCollection.Add(statement);
        internal void InsertFirst(Statement statement) => mCollection.Insert(0, statement);

        public virtual bool Equals(StatementSetEnvironment other)
        {
            if (other == null)
                return false;
            if (Count != other.Count)
                return false;
            for (int i = 0; i < Count; i++)
                if (!this[i].Equals(other[i]))
                    return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is StatementSetEnvironment item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private Dictionary<string, SqlConstant> globalParameters = new Dictionary<string, SqlConstant>();
        private Dictionary<string, SqlConstant> initialGobalParameters = null;

        internal void FixInitialGobalParameters()
        {
            initialGobalParameters = globalParameters;
        }

        public bool AddGlobalParameter(string name, SqlConstant value)
        {
            if (globalParameters.ContainsKey(name))
                return false;
            globalParameters.Add(name, value);
            return true;
        }

        public void UpdateGlobalParameter(string name, SqlConstant value)
        {
            if (!globalParameters.ContainsKey(name))
                globalParameters.Add(name, value);
            else
                globalParameters[name] = value;
        }

        public SqlConstant FindGlobalParameter(string name)
        {
            if (globalParameters.ContainsKey(name))
                return globalParameters[name];
            return null;
        }

        public IStatementSetEnvironment ParentEnvironment { get; set; } = null;

        public Statement ParentStatement { get; set; } = null;

        public void ClearEnvironment()
        {
            if (initialGobalParameters != null)
            {
                globalParameters = initialGobalParameters;
            }
            else
            {
                globalParameters.Clear();
            }
        }

        public bool ContainsGlobalParameter(string name)
        {
            return globalParameters.ContainsKey(name);
        }

        public bool Continue { get; set; } = false;
        public bool Leave { get; set; } = false;

        private object mLastStatementResult = new List<object>();

        public object LastStatementResult
        {
            get
            {
                return mLastStatementResult;
            }
            set
            {
                mLastStatementResult = value;
            }
        }
    }

    public interface IStatementSetEnvironment
    {
        object LastStatementResult { get; set; }
        bool Continue { get; set; }
        bool Leave { get; set; }
        IStatementSetEnvironment ParentEnvironment { get; set; }
        Statement ParentStatement { get; set; }
        bool AddGlobalParameter(string name, SqlConstant value);
        void UpdateGlobalParameter(string name, SqlConstant value);
        SqlConstant FindGlobalParameter(string name);
        void ClearEnvironment();
        bool ContainsGlobalParameter(string name);
    }
}
