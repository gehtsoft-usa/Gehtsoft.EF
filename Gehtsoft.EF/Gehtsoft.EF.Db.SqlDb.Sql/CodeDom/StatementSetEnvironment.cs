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

        internal StatementSetEnvironment BeforeContinue { get; set; } = null;

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

        internal StatementSetEnvironment(SqlCodeDomBuilder builder)
        {

        }

        internal void Add(Statement statement) => mCollection.Add(statement);
        internal void Add(StatementSetEnvironment statements)
        {
            foreach (Statement statement in statements)
                mCollection.Add(statement);
            if (statements.InitialGobalParameters != null)
            {
                if (InitialGobalParameters == null)
                    InitialGobalParameters = new Dictionary<string, SqlConstant>();
                foreach (KeyValuePair<string, SqlConstant> item in statements.InitialGobalParameters)
                {
                    if (!InitialGobalParameters.ContainsKey(item.Key))
                        InitialGobalParameters.Add(item.Key, item.Value);
                }
                ClearEnvironment();
            }
        }
        internal void InsertFirst(Statement statement) => mCollection.Insert(0, statement);
        internal void InsertFirst(StatementSetEnvironment statements)
        {
            for (int i = statements.Count - 1; i >= 0; i--)
                mCollection.Insert(0, statements[i]);
            if (statements.InitialGobalParameters != null)
            {
                if (InitialGobalParameters == null)
                    InitialGobalParameters = new Dictionary<string, SqlConstant>();
                foreach (KeyValuePair<string, SqlConstant> item in statements.InitialGobalParameters)
                {
                    if (!InitialGobalParameters.ContainsKey(item.Key))
                        InitialGobalParameters.Add(item.Key, item.Value);
                }
                ClearEnvironment();
            }
        }

        public virtual bool Equals(StatementSetEnvironment other)
        {
            if (other == null)
                return false;
            if (Count != other.Count)
                return false;
            for (int i = 0; i < Count; i++)
                if (!this[i].Equals(other[i]))
                    return false;
            if (BeforeContinue == null && other.BeforeContinue != null)
                return false;
            if (BeforeContinue != null && !BeforeContinue.Equals(other.BeforeContinue))
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
        internal Dictionary<string, SqlConstant> InitialGobalParameters { get; set; } = null;

        internal void FixInitialGobalParameters()
        {
            InitialGobalParameters = globalParameters;
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
            if (InitialGobalParameters != null)
            {
                globalParameters = InitialGobalParameters;
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

    public interface IStatementSetEnvironment : IParametersHolder
    {
        bool Continue { get; set; }
        bool Leave { get; set; }
        IStatementSetEnvironment ParentEnvironment { get; set; }
        Statement ParentStatement { get; set; }
        void ClearEnvironment();
    }

    public interface IParametersHolder
    {
        object LastStatementResult { get; set; }
        bool AddGlobalParameter(string name, SqlConstant value);
        void UpdateGlobalParameter(string name, SqlConstant value);
        SqlConstant FindGlobalParameter(string name);
        bool ContainsGlobalParameter(string name);
    }
}
