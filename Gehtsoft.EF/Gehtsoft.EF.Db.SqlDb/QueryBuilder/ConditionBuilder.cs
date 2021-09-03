using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    [DocgenIgnore]
    public interface IConditionBuilderInfoProvider
    {
        SqlDbLanguageSpecifics Specifics { get; }

        string GetAlias(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity queryEntity);
    }

    /// <summary>
    /// Builder for a single condition.
    ///
    /// The builder for a single condition is returned
    /// by <see cref="ConditionBuilder"/>.
    ///
    /// Define a condition as a following:
    /// * Set the left side
    /// * Set the operation
    /// * Set the right side
    ///
    /// ```cs
    /// query.Where.Property(table["column"]).Is(CmpOp.Eq).Parameter("c1");
    /// ```
    ///
    /// Use this class to define one comparison or check within a
    /// complex condition. See <see cref="SingleConditionBuilderExtension"/>
    /// to easier and more readable definition of the condition.
    /// </summary>
    public class SingleConditionBuilder
    {
        internal ConditionBuilder Builder { get; }

        private readonly LogOp mLogOp;
        private CmpOp? mCmpOp = null;

        internal string Right { get; set; }

        internal string Left { get; set; }

        internal bool HasOp => mCmpOp != null;

        internal SingleConditionBuilder(ConditionBuilder builder, LogOp logOp)
        {
            Builder = builder;
            mLogOp = logOp;
            builder.SetCurrentSignleConditionBuilder(this);
        }

        /// <summary>
        /// Adds a raw SQL code.
        /// </summary>
        /// <param name="rawExpression"></param>
        /// <returns></returns>
        public SingleConditionBuilder Raw(string rawExpression)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (rawExpression.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(rawExpression));

            if (mCmpOp == null)
                Left = rawExpression;
            else
                Right = rawExpression;
            return this;
        }

        /// <summary>
        /// Adds a property of the specified table in the query aggregated using the specified function.
        /// </summary>
        /// <param name="aggFn"></param>
        /// <param name="columnInfo"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public SingleConditionBuilder Property(AggFn aggFn, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity) => Raw(Builder.PropertyName(aggFn, entity, columnInfo));

        /// <summary>
        /// Adds a property of the first table in the query aggregated using the specified function.
        /// </summary>
        /// <param name="aggFn"></param>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        public SingleConditionBuilder Property(AggFn aggFn, TableDescriptor.ColumnInfo columnInfo) => Raw(Builder.PropertyName(aggFn, columnInfo));

        /// <summary>
        /// Adds a property of the specified table in the query.
        /// </summary>
        /// <param name="columnInfo"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public SingleConditionBuilder Property(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity) => Raw(Builder.PropertyName(entity, columnInfo));

        /// <summary>
        /// Adds a property of the first table in the query.
        /// </summary>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        public SingleConditionBuilder Property(TableDescriptor.ColumnInfo columnInfo) => Raw(Builder.PropertyName(columnInfo));

        /// <summary>
        /// Adds a parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public SingleConditionBuilder Parameter(string name) => Raw(Builder.ParameterName(name));

        /// <summary>
        /// Adds a list of the parameters.
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public SingleConditionBuilder Parameters(params string[] names) => Raw(Builder.ParameterList(names));

        /// <summary>
        /// Adds a subquery.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public SingleConditionBuilder Query(AQueryBuilder builder) => Raw(Builder.Query(builder));

        /// <summary>
        /// Adds a comparison.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public SingleConditionBuilder Is(CmpOp op)
        {
            mCmpOp = op;
            return this;
        }

        /// <summary>
        /// Adds the next condition and connect it by the specified logical operator.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public SingleConditionBuilder Add(LogOp op) => Builder.Add(op);

        internal void Push()
        {
            if (mCmpOp == null)
                Builder.Add(mLogOp, Left);
            else
                Builder.Add(mLogOp, Left, (CmpOp)mCmpOp, Right);
        }
    }

    /// <summary>
    /// The interface to provide information about a column involved into a query.
    ///
    /// The interface is used when a column from a main query needs to be used
    /// in a subquery condition.
    ///
    /// Use [clink=QueryWithWhereBuilder.GetReference]GetReference[/clink] to get a reference
    /// to a field.
    /// </summary>
    public interface IInQueryFieldReference
    {
        string Alias { get; }
    }

    internal class InQueryFieldReference : IInQueryFieldReference
    {
        public string Alias { get; }
        public InQueryFieldReference(string alias)
        {
            Alias = alias;
        }
    }

    /// <summary>
    /// The extension class to simpler building of the condition.
    /// </summary>
    public static class SingleConditionBuilderExtension
    {
        /// <summary>
        /// Sets "equals to" comparison.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Eq(this SingleConditionBuilder builder) => builder.Is(CmpOp.Eq);

        /// <summary>
        /// Sets "not equals to" comparison
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Neq(this SingleConditionBuilder builder) => builder.Is(CmpOp.Neq);

        /// <summary>
        /// Sets "less than or equals to" comparison.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Le(this SingleConditionBuilder builder) => builder.Is(CmpOp.Le);

        /// <summary>
        /// Sets "less than" comparison.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Ls(this SingleConditionBuilder builder) => builder.Is(CmpOp.Ls);

        /// <summary>
        /// Sets "greater than or equals to" comparison.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Ge(this SingleConditionBuilder builder) => builder.Is(CmpOp.Ge);

        /// <summary>
        /// Sets "greater than" comparison
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Gt(this SingleConditionBuilder builder) => builder.Is(CmpOp.Gt);

        /// <summary>
        /// Sets "is like" comparison.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Like(this SingleConditionBuilder builder) => builder.Is(CmpOp.Like);

        /// <summary>
        /// Sets "in" comparison.
        ///
        /// Use only parameter group or subquery as a right side argument.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder In(this SingleConditionBuilder builder) => builder.Is(CmpOp.In);

        /// <summary>
        /// Sets "is null" check.
        ///
        /// The right-side argument will be ignored.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder IsNull(this SingleConditionBuilder builder) => builder.Is(CmpOp.IsNull);

        /// <summary>
        /// Sets "is not null" check.
        ///
        /// The right-side argument will be ignored.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder NotNull(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotNull);

        /// <summary>
        /// Sets "not in" comparison.
        ///
        /// Use only parameter group or sub-query as a right side argument.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder NotIn(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotIn);

        /// <summary>
        /// Sets "exists" comparison.
        ///
        /// The left-side argument is ignored, use subquery as a right-side argument.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Exists(this SingleConditionBuilder builder) => builder.Is(CmpOp.Exists);

        /// <summary>
        /// Sets "not exists" comparison.
        ///
        /// The left-side argument is ignored, use subquery as a right-side argument.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder NotExists(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotExists);

        /// <summary>
        /// Adds an integer constant.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, int value) => builder.Raw(value.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Adds a double constant.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, double value) => builder.Raw(value.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// Adds a string value.
        ///
        /// Note: By default, literals in queries are disabled to prevent SQL injection attack. Use <see cref="SqlInjectionProtectionPolicy"/> to enable or disable SQL injection protection.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, string value) => builder.Raw($"\"{value}\"");

        /// <summary>
        /// Adds reference as argument.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Reference(this SingleConditionBuilder builder, IInQueryFieldReference reference) => builder.Raw(reference.Alias);

        /// <summary>
        /// Wraps previous argument in a function specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        internal static SingleConditionBuilder Wrap(this SingleConditionBuilder builder, SqlFunctionId function)
        {
            bool done = false;
            if (builder.HasOp)
            {
                if (builder.Right != null)
                {
                    builder.Right = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Right });
                    done = true;
                }
                else if (function == SqlFunctionId.Count)
                {
                    builder.Right = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, Array.Empty<string>());
                    done = true;
                }
            }
            else
            {
                if (builder.Left != null)
                {
                    builder.Left = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Left });
                    done = true;
                }
                else if (function == SqlFunctionId.Count)
                {
                    builder.Left = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, Array.Empty<string>());
                    done = true;
                }
            }
            if (!done)
                throw new InvalidOperationException("Nothing to process with a function");

            return builder;
        }

        /// <summary>
        /// Wraps argument into `Sum` function.
        ///
        /// Use this function for `HAVING` conditions only.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Sum(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Sum);

        /// <summary>
        /// Wraps argument into `Min` function.
        ///
        /// Use this function for `HAVING` conditions only.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>public static SingleConditionBuilder Avg(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Avg);
        public static SingleConditionBuilder Min(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Min);

        /// <summary>
        /// Wraps argument into `Max` function.
        ///
        /// Use this function for `HAVING` conditions only.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        ///
        public static SingleConditionBuilder Max(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Max);

        /// <summary>
        /// Wraps argument into `Avg` function.
        ///
        /// Use this function for `HAVING` conditions only.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        ///
        public static SingleConditionBuilder Avg(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Avg);

        /// <summary>
        /// Wraps argument into `Count` function.
        ///
        /// Use this function for `HAVING` conditions only.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Count(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Count);

        /// <summary>
        /// Wraps argument into Trim function
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Trim(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Trim);

        /// <summary>
        /// Wraps argument into Upper function
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Upper(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Upper);

        /// <summary>
        /// Wraps argument into Lower function
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Lower(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Lower);

        /// <summary>
        /// Wraps argument into Abs function
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Abs(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Abs);

        /// <summary>
        /// Adds another condition and connect it using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder And(this SingleConditionBuilder builder) => builder.Add(LogOp.And);

        /// <summary>
        /// Adds another condition and connect it using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Or(this SingleConditionBuilder builder) => builder.Add(LogOp.Or);

        /// <summary>
        /// Adds another negative condition and connect it using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder AndNot(this SingleConditionBuilder builder) => builder.Add(LogOp.And | LogOp.Not);

        /// <summary>
        /// Adds another condition and connect it using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder OrNot(this SingleConditionBuilder builder) => builder.Add(LogOp.Or | LogOp.Not);

        /// <summary>
        /// Adds another complex condition in brackets and connect it using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static ConditionBuilder And(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.And(action);
            return builder.Builder;
        }

        /// <summary>
        /// Adds another complex condition in brackets and connect it using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static ConditionBuilder Or(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.Or(action);
            return builder.Builder;
        }

        /// <summary>
        /// Adds another negative complex condition in brackets and connect it using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static ConditionBuilder AndNot(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.AndNot(action);
            return builder.Builder;
        }

        /// <summary>
        /// Adds another negative complex condition in brackets and connect it using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static ConditionBuilder OrNot(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.OrNot(action);
            return builder.Builder;
        }
    }

    /// <summary>
    /// The build of the condition.
    ///
    /// The most convenient way to use the condition builder is to
    /// add <see cref="SingleConditionBuilder"/> for each individual
    /// condition using <see cref="ConditionBuilder.Add(LogOp)"/> or
    /// <see cref="ConditionBuilder.AddGroup(LogOp)"/> methods.
    ///
    /// The rest of methods are designed for EF infrastructure or may be
    /// used to construct complex queries that aren't supported by EF.
    ///
    /// Use also <see cref="ConditionBuilderExtension"/> to easier and more readable
    /// definition of the conditions.
    ///
    /// The condition builder is used to build `where`, `on` and `having` clauses of SQL queries.
    /// </summary>
    public class ConditionBuilder : IOpBracketAcceptor
    {
        private readonly StringBuilder mWhere = new StringBuilder();

        private bool mConditionStarted = false;

        [DocgenIgnore]
        public IConditionBuilderInfoProvider InfoProvider { get; protected set; }

        [DocgenIgnore]
        public bool IsEmpty
        {
            get
            {
                SetCurrentSignleConditionBuilder(null);
                return mWhere.Length == 0;
            }
        }

        [DocgenIgnore]
        public ConditionBuilder(IConditionBuilderInfoProvider infoProvider)
        {
            InfoProvider = infoProvider;
        }

        private SingleConditionBuilder mCurrentSingleConditionBuilder = null;

        internal void SetCurrentSignleConditionBuilder(SingleConditionBuilder singleConditionBuilder)
        {
            if (mCurrentSingleConditionBuilder != null)
            {
                var c = mCurrentSingleConditionBuilder;
                mCurrentSingleConditionBuilder = null;
                c.Push();
            }
            mCurrentSingleConditionBuilder = singleConditionBuilder;
        }

        /// <summary>
        /// Adds raw SQL expression and connects it to previous condition with the specified logical operator.
        /// </summary>
        /// <param name="logOp"></param>
        /// <param name="rawExpression"></param>
        public virtual void Add(LogOp logOp, string rawExpression)
        {
            SetCurrentSignleConditionBuilder(null);

            if (string.IsNullOrWhiteSpace(rawExpression))
                return;

            if (!mConditionStarted)
            {
                if ((logOp & LogOp.Not) == LogOp.Not)
                    mWhere.Append(InfoProvider.Specifics.GetLogOp(LogOp.Not));
            }
            else
                mWhere.Append(InfoProvider.Specifics.GetLogOp(logOp));

            mWhere.Append(rawExpression);
            mWhere.Append(InfoProvider.Specifics.CloseLogOp(logOp));

            mConditionStarted = true;
        }

        /// <summary>
        /// Adds an expression defined by two raw SQL argument and comparison operation connects it to previous condition with the specified logical operator.
        /// </summary>
        /// <param name="logOp"></param>
        /// <param name="leftSide"></param>
        /// <param name="cmpOp"></param>
        /// <param name="rightSide"></param>
        public virtual void Add(LogOp logOp, string leftSide, CmpOp cmpOp, string rightSide) => Add(logOp, InfoProvider.Specifics.GetOp(cmpOp, leftSide, rightSide));

        /// <summary>
        /// Adds an expression defined by two raw SQL argument and comparison operation connects it to previous condition with the logical and operator.
        /// </summary>
        /// <param name="leftSide"></param>
        /// <param name="cmpOp"></param>
        /// <param name="rightSide"></param>
        public virtual void Add(string leftSide, CmpOp cmpOp, string rightSide) => Add(LogOp.And, InfoProvider.Specifics.GetOp(cmpOp, leftSide, rightSide));

        [DocgenIgnore]
        public virtual string PropertyName(QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnDescriptor) => columnDescriptor != null ? InfoProvider.GetAlias(columnDescriptor, entity) : null;

        [DocgenIgnore]
        public virtual string PropertyName(AggFn aggFn, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnDescriptor) => InfoProvider.Specifics.GetAggFn(aggFn, InfoProvider.GetAlias(columnDescriptor, entity));

        [DocgenIgnore]
        public virtual string PropertyName(TableDescriptor.ColumnInfo columnDescriptor) => InfoProvider.GetAlias(columnDescriptor, null);

        [DocgenIgnore]
        public virtual string PropertyName(AggFn aggFn, TableDescriptor.ColumnInfo columnDescriptor) => InfoProvider.Specifics.GetAggFn(aggFn, InfoProvider.GetAlias(columnDescriptor, null));

        [DocgenIgnore]
        public virtual string ParameterName(string parameterName)
        {
            if (parameterName == null)
                return null;

            string prefix = InfoProvider.Specifics.ParameterInQueryPrefix;
            if (!string.IsNullOrEmpty(prefix) && !parameterName.StartsWith(prefix))
                parameterName = prefix + parameterName;
            return parameterName;
        }

        [DocgenIgnore]
        public virtual string ParameterList(string[] parameterNames)
        {
            var builder = InfoProvider.Specifics.GetParameterGroupBuilder();
            for (int i = 0; i < parameterNames.Length; i++)
                builder.AddParameter(parameterNames[i]);
            builder.PrepareQuery();
            return builder.Query;
        }

        [DocgenIgnore]
        public virtual string Query(AQueryBuilder queryBuilder)
        {
            if (queryBuilder.Query == null)
                queryBuilder.PrepareQuery();
            return $"({queryBuilder.Query})";
        }

        /// <summary>
        /// Adds a group of conditions enclosed into brackets and connects it with the previous expression using the specified logical operator.
        ///
        /// The group of conditions is considered finished when the object returned is disposed.
        /// </summary>
        /// <param name="logOp"></param>
        /// <returns></returns>
        public virtual OpBracket AddGroup(LogOp logOp = LogOp.And)
        {
            SetCurrentSignleConditionBuilder(null);
            if (!mConditionStarted)
            {
                if ((logOp & LogOp.Not) == LogOp.Not)
                    mWhere.Append(InfoProvider.Specifics.GetLogOp(LogOp.Not));
            }
            else
                mWhere.Append(InfoProvider.Specifics.GetLogOp(logOp));

            mConditionStarted = false;
            mWhere.Append("(");
            return new OpBracket(this, logOp);
        }

        [DocgenIgnore]
        public virtual void BracketClosed(OpBracket op)
        {
            SetCurrentSignleConditionBuilder(null);
            if (!mConditionStarted)
                throw new EfSqlException(EfExceptionCode.WhereBracketIsEmpty);
            mWhere.Append(")");
            mWhere.Append(InfoProvider.Specifics.CloseLogOp(op.LogOp));
        }

        [DocgenIgnore]
        public override string ToString()
        {
            SetCurrentSignleConditionBuilder(null);
            return mWhere.ToString();
        }

        /// <summary>
        /// Adds a single condition and connects it using the specified logical operator.
        /// </summary>
        /// <param name="logOp"></param>
        /// <returns></returns>
        public SingleConditionBuilder Add(LogOp logOp) => new SingleConditionBuilder(this, logOp);

        /// <summary>
        /// Adds a group of conditions enclosed into brackets and connects it with the previous expression using the specified logical operator.
        ///
        /// Pass an action to define the content of the condition group.
        /// </summary>
        /// <param name="logOp"></param>
        /// <param name="builder"></param>
        public void Add(LogOp logOp, Action<ConditionBuilder> builder)
        {
            using (var bracket = AddGroup(logOp))
                builder.Invoke(this);
        }
    }

    /// <summary>
    /// The extensions for the `ConditionBuilder` class.
    /// </summary>
    public static class ConditionBuilderExtension
    {
        /// <summary>
        /// Adds a single condition and connects it to the previous condition using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder And(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.And);

        /// <summary>
        /// Adds a single condition and connects it to the previous condition using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Or(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.Or);

        /// <summary>
        /// Adds a single negative condition and connects it to the previous condition using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Not(this ConditionBuilder builder) => AndNot(builder);

        /// <summary>
        /// Adds a single condition and connects it to the previous condition using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder OrNot(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.Or | LogOp.Not);

        /// <summary>
        /// Adds a single condition and connects it to the previous condition using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder AndNot(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.And | LogOp.Not);

        /// <summary>
        /// Adds a complex condition in brackets and connects it to the previous condition using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        public static void And(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.And, action);

        /// <summary>
        /// Adds a complex condition in brackets and connects it to the previous condition using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        public static void Or(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.Or, action);

        /// <summary>
        /// Adds a negative complex condition in brackets and connects it to the previous condition using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        public static void AndNot(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.And | LogOp.Not, action);

        /// <summary>
        /// Adds a complex condition in brackets and connects it to the previous condition using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="action"></param>
        public static void OrNot(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.Or | LogOp.Not, action);

        /// <summary>
        /// Starts a new single condition, sets the a column of the specified table in the query as the first argument and connect it to the previous condition with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="columnInfo"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Property(this ConditionBuilder builder, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity) => builder.Raw(builder.PropertyName(entity, columnInfo));

        /// <summary>
        /// Starts a new single condition, sets the a column of the first table in the query as the first argument and connect it to the previous condition with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Property(this ConditionBuilder builder, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(columnInfo));

        /// <summary>
        /// Starts a new single condition, sets the a column of the specified table in the query, aggregated with the specified function, as the first argument and connect it to the previous condition with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="fn"></param>
        /// <param name="entity"></param>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Property(this ConditionBuilder builder, AggFn fn, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(fn, entity, columnInfo));

        /// <summary>
        /// Starts a new single condition, sets the a column of the first table in the query, aggregated with the specified function as the first argument and connect it to the previous condition with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="fn"></param>
        /// <param name="columnInfo"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Property(this ConditionBuilder builder, AggFn fn, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(fn, columnInfo));

        /// <summary>
        /// Starts a new single condition, sets the parameter specified as the first argument and connect it to the previous condition with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Parameter(this ConditionBuilder builder, string parameter)
        {
            var b = new SingleConditionBuilder(builder, LogOp.And);
            return b.Parameter(parameter);
        }

        /// <summary>
        /// Starts a new single condition, sets column reference as the first argument and connect it to the previous condition with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Reference(this ConditionBuilder builder, IInQueryFieldReference reference) => builder.Raw(reference.Alias);

        /// <summary>
        /// Starts a new single condition, connect it to the previous expression using logical and, and set the operator to exists.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Exists(this ConditionBuilder builder)
        {
            SingleConditionBuilder singleBuilder = new SingleConditionBuilder(builder, LogOp.And);
            return singleBuilder.Exists();
        }

        /// <summary>
        /// Starts a new single condition, connect it to the previous expression using logical and, and set the operator to not exists.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleConditionBuilder NotExists(this ConditionBuilder builder)
        {
            SingleConditionBuilder singleBuilder = new SingleConditionBuilder(builder, LogOp.And);
            return singleBuilder.NotExists();
        }

        /// <summary>
        /// Starts a new single condition, sets the raw SQL expression as the first argument and connect it to the previous condition with logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="rawExpression"></param>
        /// <returns></returns>
        public static SingleConditionBuilder Raw(this ConditionBuilder builder, string rawExpression)
        {
            SingleConditionBuilder singleBuilder = new SingleConditionBuilder(builder, LogOp.And);
            singleBuilder.Raw(rawExpression);
            return singleBuilder;
        }
    }
}