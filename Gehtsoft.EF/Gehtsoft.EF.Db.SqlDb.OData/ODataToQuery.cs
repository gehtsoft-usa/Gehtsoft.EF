using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.OData
{
    internal class ODataToQuery
    {
        public enum ResultType
        {
            Array,
            OneValue,
            Plain
        }

        public enum ArifOp
        {
            Add,
            Minus,
            Divide,
            Multiply
        }

        private const string DELIMETER = "$$$";
        public static readonly string ARRMARKER = "_$0_$1_$2_";

        private readonly ODataUri mUri;
        private readonly SqlDbConnection mConnection;
        private readonly EdmModelBuilder mModelBuilder;
        private EntityDescriptor mLastEntityDescriptor = null;
        private readonly Dictionary<string, EntityDescriptor> entityDescriptors = new Dictionary<string, EntityDescriptor>();

        internal ResultType ResultMode { get; private set; } = ResultType.Array;
        internal bool OneToMany { get; private set; } = false;
        internal EntityDescriptor MainEntityDescriptor { get { return mLastEntityDescriptor; } }

        internal int? Skip { get; private set; } = null;
        internal int? Top { get; private set; } = null;
        internal Dictionary<string, object> BindParams = new Dictionary<string, object>();

        public ODataToQuery(EdmModelBuilder modelBuilder, ODataUri uriParser, SqlDbConnection connection)
        {
            mModelBuilder = modelBuilder;
            mUri = uriParser;
            mConnection = connection;
        }

        public AQueryBuilder BuildQuery(bool withoutSorting = false)
        {
            SelectQueryBuilder builder = null;
            SelectQueryBuilder mainBuilder = null;
            EntityDescriptor entityDescriptor = null;

            int i = 0;
#pragma warning disable S2259 // Null pointers should not be dereferenced 
            // false positive - they don't know that mUri.Path cannot be empty
            foreach (ODataPathSegment segment in mUri.Path)
            {
                if (i == 0)
                {
                    if (!(segment is EntitySetSegment firstSegment))
                        throw new EfODataException(EfODataExceptionCode.BadPath);
                    mainBuilder = builder = CreateBuilder(firstSegment.EntitySet.Name + "_Type", out entityDescriptor);
                    mLastEntityDescriptor = entityDescriptor;
                    ResultMode = ResultType.Array;
                }
                else
                {
                    if (segment is KeySegment keySegment)
                    {
                        foreach (KeyValuePair<string, object> keyPair in keySegment.Keys)
                        {
                            SingleConditionBuilder condBuilder = builder.Where.And().Property(entityDescriptor.TableDescriptor[keyPair.Key]).Is(CmpOp.Eq);
                            if (keyPair.Value is int intValue)
                            {
                                condBuilder.Value(intValue);
                            }
                            else
                            {
                                condBuilder.Value(keyPair.Value.ToString());
                            }
                        }
                        ResultMode = ResultType.OneValue;
                    }
                    else if (segment is CountSegment)
                    {
                        ResultMode = ResultType.Plain;
                        builder.AddToResultset(AggFn.Count);
                    }
                    else if (segment is NavigationPropertySegment navigationPropertySegment)
                    {
                        if (navigationPropertySegment.EdmType is IEdmEntityType entytyType)
                        {
                            builder.AddToResultset(entityDescriptor.TableDescriptor[navigationPropertySegment.Identifier]);
                            string name = entytyType.Name;

                            SelectQueryBuilder builderOld = builder;
                            SelectQueryBuilder builderNew = CreateBuilder(name, out entityDescriptor);
                            builderNew.Where.And().Property(entityDescriptor.TableDescriptor.PrimaryKey).Is(CmpOp.In).Query(builderOld);

                            mainBuilder = builder = builderNew;
                            mLastEntityDescriptor = entityDescriptor;

                            ResultMode = ResultType.OneValue;
                        }
                        else if (navigationPropertySegment.EdmType is EdmCollectionType collection)
                        {
                            if (collection.ElementType.Definition is IEdmEntityType innerEntytyType)
                            {
                                string referFieldName = entityDescriptor.EntityType.Name;
                                builder.AddToResultset(entityDescriptor.TableDescriptor.PrimaryKey);
                                string name = innerEntytyType.Name;

                                SelectQueryBuilder builderOld = builder;
                                SelectQueryBuilder builderNew = CreateBuilder(name, out entityDescriptor);
                                builderNew.Where.And().Property(entityDescriptor.TableDescriptor[referFieldName]).Is(CmpOp.In).Query(builderOld);

                                mainBuilder = builder = builderNew;
                                mLastEntityDescriptor = entityDescriptor;

                                ResultMode = ResultType.Array;
                            }
                        }
                    }
                    else if (segment is PropertySegment propertySegment)
                    {
                        ResultMode = ResultType.Plain;
                        builder.AddToResultset(entityDescriptor.TableDescriptor[propertySegment.Identifier]);
                    }
                }
                i++;
            }

            if (mUri.SelectAndExpand != null)
            {
                ProcessSelectAndExpand(builder, entityDescriptor, mUri.SelectAndExpand);
                if (!mUri.SelectAndExpand.AllSelected)
                {
                    int pagingLimit = mModelBuilder.EntityPagingLimitByName(entityDescriptor.EntityType.Name + "_Type");
                    if (pagingLimit > 0)
                    {
                        string primaryKeyName = null;
                        foreach (var property in entityDescriptor.TableDescriptor)
                        {
                            if (property.PrimaryKey)
                            {
                                primaryKeyName = property.ID;
                                break;
                            }
                        }
                        if (primaryKeyName != null)
                        {
                            string primaryKeyAlias = mainBuilder.GetAlias(entityDescriptor.TableDescriptor[primaryKeyName], null);
                            bool found = false;
                            foreach (SelectQueryBuilderResultsetItem item in mainBuilder.Resultset)
                            {
                                if (primaryKeyAlias == item.Expression)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                mainBuilder.AddToResultset(entityDescriptor.TableDescriptor[primaryKeyName]);
                            }
                        }
                    }
                }
            }

            if (mUri.Filter != null)
            {
                ProcessFilter(builder, entityDescriptor, mUri.Filter);
            }

            if (mUri.OrderBy != null && !withoutSorting)
            {
                if (this.ResultMode != ResultType.Array)
                {
                    throw new EfODataException(EfODataExceptionCode.QueryOptionsFault);
                }
                ProcessOrderBy(builder, entityDescriptor, mUri.OrderBy);
            }

            if (mUri.Skip.HasValue)
            {
                if (this.ResultMode != ResultType.Array)
                {
                    throw new EfODataException(EfODataExceptionCode.QueryOptionsFault);
                }
                if (OneToMany)
                    this.Skip = (int)mUri.Skip.Value;
                else
                    mainBuilder.Skip = (int)mUri.Skip.Value;
            }
            if (mUri.Top.HasValue || mUri.Skip.HasValue)
            {
                if (this.ResultMode != ResultType.Array)
                {
                    throw new EfODataException(EfODataExceptionCode.QueryOptionsFault);
                }
                int value = mUri.Top.HasValue ? (int)mUri.Top.Value : Int32.MaxValue;
                if (OneToMany)
                    this.Top = value;
                else
                    mainBuilder.Limit = value;
            }
            if (mUri.SkipToken != null)
            {
                if (this.ResultMode != ResultType.Array)
                {
                    throw new EfODataException(EfODataExceptionCode.QueryOptionsFault);
                }
                int pagingLimit = mModelBuilder.EntityPagingLimitByName(MainEntityDescriptor.EntityType.Name + "_Type");
                if (pagingLimit <= 0)
                {
                    throw new EfODataException(EfODataExceptionCode.SkiptokenWitoutPagingLimit);
                }
                string pKey = builder.Where.PropertyName(null, MainEntityDescriptor.TableDescriptor.PrimaryKey);
                string expression = builder.Where.InfoProvider.Specifics.GetOp(CmpOp.Gt, pKey, mUri.SkipToken);
                builder.Where.Add(LogOp.And, expression);
            }
#pragma warning restore S2259 // Null pointers should not be dereferenced 
            return mainBuilder;
        }

        private void ProcessOrderBy(QueryBuilder.SelectQueryBuilder builder, EntityDescriptor entityDescriptor, OrderByClause clause)
        {
            if (clause.Expression is SingleValuePropertyAccessNode node)
            {
                string propertyName = node.Property.Name;
                SortDir sortDir = clause.Direction == OrderByDirection.Ascending ? SortDir.Asc : SortDir.Desc;
                TableDescriptor tableDescriptor = entityDescriptor.TableDescriptor;
                EntityDescriptor localEntityDescriptor = null;

                if (node.Source is SingleNavigationNode navNode)
                {
                    if (navNode.EntityTypeReference.Definition is EdmEntityType edmType)
                    {
                        string name = edmType.Name;
                        CreateBuilder(name, out localEntityDescriptor);
                        tableDescriptor = localEntityDescriptor.TableDescriptor;
                    }
                    else
                    {
                        throw new EfODataException(EfODataExceptionCode.UnknownEdmType);
                    }
                }

                if (!tableDescriptor.TryGetValue(propertyName, out TableDescriptor.ColumnInfo column))
                    column = null;

                if (column == null)
                {
                    try
                    {
                        propertyName = mModelBuilder.NameByField(entityDescriptor.EntityType, node.Property.Name);
                        column = entityDescriptor.TableDescriptor[propertyName];
                    }
                    catch { }
                }
                if (column == null && node.Property.Name.EndsWith("ID"))
                {
                    try
                    {
                        int index = node.Property.Name.LastIndexOf("ID");
                        propertyName = node.Property.Name.Substring(0, index);
                        column = entityDescriptor.TableDescriptor[propertyName];
                    }
                    catch { }
                }
                if (column == null)
                {
                    throw new EfODataException(EfODataExceptionCode.UnknownOperator, node.Property.Name);
                }

                // check whether entity of the column is added to the builder
                QueryBuilderEntity columnEntity = null;
                foreach (QueryBuilderEntity entity in builder.Entities)
                {
                    if (entity.Table == column.Table)
                    {
                        columnEntity = entity;
                        break;
                    }
                }
                // if not added
                if (columnEntity == null && localEntityDescriptor != null)
                {
                    // then just add it without select fields
                    builder.AddTable(localEntityDescriptor.TableDescriptor, true);
                }

                builder.AddOrderBy(column, sortDir);

                if (clause.ThenBy != null)
                    ProcessOrderBy(builder, entityDescriptor, clause.ThenBy);
            }
            else
            {
                throw new EfODataException(EfODataExceptionCode.UnknownEdmType);
            }
        }

        private void ProcessFilter(QueryBuilder.SelectQueryBuilder builder, EntityDescriptor entityDescriptor, FilterClause clause)
        {
            string expression = GetStrExpression(clause.Expression, builder.Where, entityDescriptor);
            if (!builder.Where.IsEmpty)
            {
                expression = $"{expression}";
            }
            builder.Where.Add(LogOp.And, expression);
        }

        private SqlFunctionId? GetFunctionId(string name)
        {
            switch (name)
            {
                case "matchesPattern":
                case "contains":
                case "endswith":
                case "startswith":
                    return SqlFunctionId.Like;
                case "toupper":
                    return SqlFunctionId.Upper;
                case "tolower":
                    return SqlFunctionId.Lower;
                case "trim":
                    return SqlFunctionId.Trim;
                case "trimleft":
                    return SqlFunctionId.TrimLeft;
                case "concat":
                    return SqlFunctionId.Concat;
            }

            return null;
        }

        private string BuildFunctionCall(SingleValueFunctionCallNode func, ConditionBuilder where, EntityDescriptor entityDescriptor)
        {
            string retval = null;
            SqlFunctionId? funcId = GetFunctionId(func.Name);
            if (funcId.HasValue)
            {
                List<string> pars = new List<string>();
                foreach (QueryNode node in func.Parameters)
                {
                    pars.Add(GetStrExpression(node, where, entityDescriptor));
                }
                if (func.Name == "contains")
                {
                    string newParam = mConnection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat,
                        new string[] { "'%'",
                        mConnection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat,
                        new string[] { pars[pars.Count - 1], "'%'" })
                        });

                    if (!newParam.Contains("(")) newParam = $"({newParam})";

                    pars[pars.Count - 1] = newParam;
                }
                else if (func.Name == "endswith")
                {
                    string newParam = mConnection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat,
                        new string[] { "'%'", pars[pars.Count - 1] });

                    if (!newParam.Contains("(")) newParam = $"({newParam})";

                    pars[pars.Count - 1] = newParam;
                }
                else if (func.Name == "startswith")
                {
                    string newParam = mConnection.GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.Concat,
                        new string[] { pars[pars.Count - 1], "'%'" });

                    if (!newParam.Contains("(")) newParam = $"({newParam})";

                    pars[pars.Count - 1] = newParam;
                }

                retval = $"({mConnection.GetLanguageSpecifics().GetSqlFunction(funcId.Value, pars.ToArray())})";
            }

            return retval;
        }

        private string GetStrExpression(QueryNode node, ConditionBuilder where, EntityDescriptor entityDescriptor)
        {
            if (node is SingleValueFunctionCallNode func)
            {
                return BuildFunctionCall(func, where, entityDescriptor);
            }
            else if (node is UnaryOperatorNode unar)
            {
                string start = string.Empty;
                string end = string.Empty;
                switch (unar.OperatorKind)
                {
                    case UnaryOperatorKind.Negate:
                        start = " -(";
                        break;
                    case UnaryOperatorKind.Not:
                        start = where.InfoProvider.Specifics.GetLogOp(LogOp.Not);
                        break;
                }
                if (start.Contains("(")) end = ")";
                return $"{start}{GetStrExpression(unar.Operand, where, entityDescriptor)}{end}";
            }
            else if (node is InNode inNode)
            {
                string leftOperand = GetStrExpression(inNode.Left, where, entityDescriptor);
                string rightOperand = GetStrExpression(inNode.Right, where, entityDescriptor);
                return where.InfoProvider.Specifics.GetOp(CmpOp.In, leftOperand, rightOperand);
            }
            else if (node is BinaryOperatorNode binaryNode)
            {
                string leftOperand = GetStrExpression(binaryNode.Left, where, entityDescriptor);
                string rightOperand = GetStrExpression(binaryNode.Right, where, entityDescriptor);

                CmpOp? op = null;
                LogOp? logOp = null;
                ArifOp? arifOp = null;
                switch (binaryNode.OperatorKind)
                {
                    case BinaryOperatorKind.Equal:
                        op = CmpOp.Eq;
                        break;
                    case BinaryOperatorKind.NotEqual:
                        op = CmpOp.Neq;
                        break;
                    case BinaryOperatorKind.GreaterThan:
                        op = CmpOp.Gt;
                        break;
                    case BinaryOperatorKind.GreaterThanOrEqual:
                        op = CmpOp.Ge;
                        break;
                    case BinaryOperatorKind.LessThan:
                        op = CmpOp.Ls;
                        break;
                    case BinaryOperatorKind.LessThanOrEqual:
                        op = CmpOp.Le;
                        break;
                    case BinaryOperatorKind.Or:
                        logOp = LogOp.Or;
                        break;
                    case BinaryOperatorKind.And:
                        logOp = LogOp.And;
                        break;
                    case BinaryOperatorKind.Add:
                        arifOp = ArifOp.Add;
                        break;
                    case BinaryOperatorKind.Subtract:
                        arifOp = ArifOp.Minus;
                        break;
                    case BinaryOperatorKind.Divide:
                        arifOp = ArifOp.Divide;
                        break;
                    case BinaryOperatorKind.Multiply:
                        arifOp = ArifOp.Multiply;
                        break;
                    default:
                        throw new EfODataException(EfODataExceptionCode.UnknownOperator);
                }

                if (op.HasValue)
                    return $"({where.InfoProvider.Specifics.GetOp(op.Value, leftOperand, rightOperand)})";
                else if (logOp.HasValue)
                    return $"({leftOperand}{where.InfoProvider.Specifics.GetLogOp(logOp.Value)}{rightOperand})";
                else
                    return $"({GetArifOp(arifOp.Value, leftOperand, rightOperand)})";
            }
            else if (node is SingleValuePropertyAccessNode prop)
            {
                string propertyName = prop.Property.Name;
                TableDescriptor.ColumnInfo column = null;
                try
                {
                    column = entityDescriptor.TableDescriptor[propertyName];
                }
                catch { }
                if (column == null)
                {
                    try
                    {
                        propertyName = mModelBuilder.NameByField(entityDescriptor.EntityType, prop.Property.Name);
                        column = entityDescriptor.TableDescriptor[propertyName];
                    }
                    catch { }
                }
                if (column == null && prop.Property.Name.EndsWith("ID"))
                {
                    try
                    {
                        int index = prop.Property.Name.LastIndexOf("ID");
                        propertyName = prop.Property.Name.Substring(0, index);
                        column = entityDescriptor.TableDescriptor[propertyName];
                    }
                    catch { }
                }
                if (column == null)
                {
                    throw new EfODataException(EfODataExceptionCode.UnknownOperator, prop.Property.Name);
                }
                return where.PropertyName(null, column);
            }
            else if (node is ConstantNode constant)
            {
                string paramName = $"$param${BindParams.Count}";
                BindParams.Add(paramName, constant.Value);
                return where.ParameterName(paramName);
            }
            else if (node is CollectionConstantNode constantCollection)
            {
                return constantCollection.LiteralText;
            }
            else if (node is ConvertNode convertNode)
            {
                return GetStrExpression(convertNode.Source, where, entityDescriptor);
            }
            throw new EfODataException(EfODataExceptionCode.UnknownOperator);
        }

        private void ProcessSelectAndExpand(SelectQueryBuilder builder, EntityDescriptor entityDescriptor, SelectExpandClause clause, string prefix = "")
        {
            bool allSelected = clause.AllSelected;
            foreach (SelectItem item in clause.SelectedItems)
            {
                if (item is PathSelectItem pathSelectItem)
                {
                    if (!allSelected)
                    {
                        foreach (ODataPathSegment pathSegment in pathSelectItem.SelectedPath)
                        {
                            if (prefix.Length > 0)
                            {
                                string ident = mModelBuilder.FieldByName(entityDescriptor.EntityType, pathSegment.Identifier);
                                builder.AddToResultset(entityDescriptor.TableDescriptor[pathSegment.Identifier], $"{prefix}{ident}");
                            }
                            else
                                builder.AddToResultset(entityDescriptor.TableDescriptor[pathSegment.Identifier]);
                        }
                    }
                }
                else if (item is ExpandedNavigationSelectItem navigationSelectItem)
                {
                    string identifier = navigationSelectItem.PathToNavigationProperty.FirstSegment.Identifier;

                    if (navigationSelectItem.PathToNavigationProperty.FirstSegment is NavigationPropertySegment navigationPropertySegment)
                    {
                        if (!allSelected)
                        {
                            if (prefix.Length > 0)
                            {
                                string ident = mModelBuilder.FieldByName(entityDescriptor.EntityType, identifier);
                                builder.AddToResultset(entityDescriptor.TableDescriptor[identifier], $"{prefix}{ident}");
                            }
                            else
                                builder.AddToResultset(entityDescriptor.TableDescriptor[identifier]);
                        }
                        else if (prefix.Length == 0) builder.AddToResultset(entityDescriptor.TableDescriptor);

                        if (navigationPropertySegment.EdmType is IEdmEntityType entytyType)
                        {
                            string name = entytyType.Name;
                            CreateBuilder(name, out EntityDescriptor localEntityDescriptor);
                            QueryBuilderEntity found = null;
                            foreach (QueryBuilderEntity qitem in builder.Entities)
                            {
                                if (entityDescriptor.TableDescriptor.Name == qitem.Table.Name)
                                {
                                    found = qitem;
                                    break;
                                }
                            }
                            builder.AddTable(localEntityDescriptor.TableDescriptor, localEntityDescriptor.TableDescriptor.PrimaryKey, TableJoinType.Inner, found, entityDescriptor.TableDescriptor[identifier]);
                            string newPrefix = $"{prefix}{identifier}{DELIMETER}";

                            entityDescriptors.Add(newPrefix, localEntityDescriptor);
                            if (navigationSelectItem.SelectAndExpand.AllSelected)
                                builder.AddToResultset(localEntityDescriptor.TableDescriptor, newPrefix);
                            if (navigationSelectItem.FilterOption != null)
                                ProcessFilter(builder, localEntityDescriptor, navigationSelectItem.FilterOption);
                            ProcessSelectAndExpand(builder, localEntityDescriptor, navigationSelectItem.SelectAndExpand, newPrefix);
                        }
                        else if (navigationPropertySegment.EdmType is EdmCollectionType collection)
                        {
                            if (collection.ElementType.Definition is IEdmEntityType innerEntytyType)
                            {
                                string name = innerEntytyType.Name;
                                CreateBuilder(name, out EntityDescriptor localEntityDescriptor);
                                builder.AddTable(localEntityDescriptor.TableDescriptor, true);
                                string newPrefix = $"{prefix}{ARRMARKER}{identifier}{DELIMETER}";

                                OneToMany = true;

                                entityDescriptors.Add(newPrefix, localEntityDescriptor);
                                if (navigationSelectItem.SelectAndExpand.AllSelected)
                                {
                                    builder.AddToResultset(localEntityDescriptor.TableDescriptor, newPrefix);
                                }

                                if (navigationSelectItem.FilterOption != null)
                                    ProcessFilter(builder, localEntityDescriptor, navigationSelectItem.FilterOption);
                                if (navigationSelectItem.OrderByOption != null)
                                    ProcessOrderBy(builder, localEntityDescriptor, navigationSelectItem.OrderByOption);
                                ProcessSelectAndExpand(builder, localEntityDescriptor, navigationSelectItem.SelectAndExpand, newPrefix);
                            }
                        }
                    }
                }
            }
        }

        private SelectQueryBuilder CreateBuilder(string edmName, out EntityDescriptor entityDescriptor)
        {
            Type entityType = mModelBuilder.EntityTypeByName(edmName);
            if (entityType == null)
                throw new EfODataException(EfODataExceptionCode.NoEntityInBuildQuery);
            entityDescriptor = AllEntities.Inst[entityType];
            return mConnection.GetSelectQueryBuilder(entityDescriptor.TableDescriptor);
        }

        public object Bind(SqlDbQuery query)
        {
            XmlSerializableDictionary result = new XmlSerializableDictionary();
            int fieldCount = query.FieldCount;
            EntityDescriptor entityDescriptor;
            for (int i = 0; i < fieldCount; i++)
            {
                string name = query.Field(i).Name;
                XmlSerializableDictionary placeTo = result;

                if (name.IndexOf(DELIMETER) > 0)
                {
                    string fullPrefix = name.Substring(0, name.LastIndexOf(DELIMETER) + DELIMETER.Length);
                    entityDescriptor = entityDescriptors[fullPrefix];
                    name = name.Substring(name.LastIndexOf(DELIMETER) + DELIMETER.Length);

                    string[] subnames = fullPrefix.Substring(0, fullPrefix.Length - DELIMETER.Length).Split(new string[] { DELIMETER }, StringSplitOptions.None);
                    for (int j = 0; j < subnames.Length; j++)
                    {
                        string subname = subnames[j];
                        bool add = true;
                        XmlSerializableDictionary newPlaceTo = null;

                        if (placeTo.ContainsKey(subname))
                        {
                            if (placeTo[subname] is XmlSerializableDictionary existingDict)
                            {
                                newPlaceTo = existingDict;
                            }
                            else
                            {
                                add = false;
                            }
                        }
                        if (newPlaceTo == null)
                        {
                            newPlaceTo = new XmlSerializableDictionary();
                            if (add)
                                placeTo.Add(subname, newPlaceTo);
                            else
                                placeTo[subname] = newPlaceTo;
                        }
                        placeTo = newPlaceTo;
                    }
                }
                else
                {
                    entityDescriptor = mLastEntityDescriptor;
                }

                object value = query.GetValue(i);
                string nameOrg = name;
                name = mModelBuilder.NameByField(entityDescriptor.EntityType, name);

                if (name != null)
                {
                    if (value != null)
                    {
                        if (value.GetType().FullName == "System.DBNull")
                        {
                            if (mModelBuilder.TypeByName(entityDescriptor.EntityType, name) == null)
                            {
                                //continue; // for foreign keys
                                name = nameOrg;
                            }
                            value = null;
                        }
                        else
                        {
                            Type toType = mModelBuilder.TypeByName(entityDescriptor.EntityType, name);
                            if (toType != null)
                            {
                                value = query.LanguageSpecifics.TranslateValue(value, toType);
                            }
                            else
                            {
                                //continue; // for foreign keys
                                name = nameOrg;
                            }
                        }
                    }
                }
                else
                {
                    name = query.Field(i).Name;
                }

                if (ResultMode == ResultType.Plain) return value;

                if (!placeTo.ContainsKey(name))
                    placeTo.Add(name, value);
            }
            return result;
        }

        public string GetArifOp(ArifOp op, string leftSide, string rightSide)
        {
            switch (op)
            {
                case ArifOp.Add:
                    return $"{leftSide} + {rightSide}";

                case ArifOp.Minus:
                    return $"{leftSide} - {rightSide}";

                case ArifOp.Multiply:
                    return $"{leftSide} * {rightSide}";

                case ArifOp.Divide:
                    return $"{leftSide} / {rightSide}";
                default:
                    throw new EfODataException(EfODataExceptionCode.UnknownOperator);
            }
        }
    }
}