using Antlr4.Runtime.Tree;

namespace Gehtsoft.EF.Test.SqlParser
{
    /// <summary>
    /// Builds the compact <see cref="IAstNode"/> tree the test-assertion vocabulary expects
    /// (the shape the old Hime grammar produced via its <c>^</c>/<c>!</c> tree-rewriting
    /// operators) from the verbose ANTLR parse tree. This is the single place that knows the
    /// ANTLR grammar; every navigation/assertion helper and the path engine stay unchanged.
    ///
    /// Conventions reproduced from <c>sql.gram</c>:
    ///   - dropped tokens (<c>'x'!</c>): keywords/punctuation are not emitted as children;
    ///   - promoted symbols (<c>X^</c>): pass-through chains collapse and binary operators
    ///     become the node symbol with their operands as children (e.g. <c>a = b</c> → EQ_OP);
    ///   - unmarked inline literals become a child node whose Symbol and Value are the text.
    /// </summary>
    internal sealed class SqlTestAstBuilder : SqlTestBaseVisitor<IAstNode>
    {
        private static AstNodeImpl Node(string symbol, string value = null) => new AstNodeImpl(symbol, value);

        private static AstNodeImpl Tree(string symbol, params IAstNode[] children)
        {
            var n = new AstNodeImpl(symbol, null);
            foreach (var c in children)
                if (c != null)
                    n.Add(c);
            return n;
        }

        // ---------------------------- root / statements ----------------------------

        public override IAstNode VisitRoot(SqlTestParser.RootContext context)
        {
            var root = Node("ROOT");
            foreach (var s in context.statement())
                root.Add(Visit(s));
            return root;
        }

        public override IAstNode VisitStatement(SqlTestParser.StatementContext context)
        {
            if (context.selectStatement() != null) return Visit(context.selectStatement());
            if (context.unionStatement() != null) return Visit(context.unionStatement());
            if (context.insertStatement() != null) return Visit(context.insertStatement());
            if (context.updateStatement() != null) return Visit(context.updateStatement());
            if (context.deleteStatement() != null) return Visit(context.deleteStatement());
            if (context.createTable() != null) return Visit(context.createTable());
            if (context.createView() != null) return Visit(context.createView());
            if (context.createIndex() != null) return Visit(context.createIndex());
            if (context.alterTable() != null) return Visit(context.alterTable());
            if (context.dropTable() != null) return Visit(context.dropTable());
            if (context.dropIndex() != null) return Visit(context.dropIndex());
            if (context.dropView() != null) return Visit(context.dropView());
            if (context.debugExpr() != null) return Tree("STATEMENT", Visit(context.debugExpr()));
            return Tree("STATEMENT", Node("NOP"));
        }

        public override IAstNode VisitDebugExpr(SqlTestParser.DebugExprContext context)
            => Tree("DEBUG_EXPR", Visit(context.expr()));

        // ---------------------------- expressions ----------------------------

        public override IAstNode VisitPrimaryExpr(SqlTestParser.PrimaryExprContext context)
            => Visit(context.primary());

        public override IAstNode VisitPrimary(SqlTestParser.PrimaryContext context)
        {
            if (context.constant() != null) return Visit(context.constant());
            if (context.field() != null) return Visit(context.field());
            if (context.param() != null) return Visit(context.param());
            if (context.funcCall() != null) return Visit(context.funcCall());
            if (context.aggrCall() != null) return Visit(context.aggrCall());
            if (context.selectExpr() != null) return Visit(context.selectExpr());
            return Visit(context.expr()); // '(' expr ')' — brackets collapse to the inner expression
        }

        public override IAstNode VisitUnarySignExpr(SqlTestParser.UnarySignExprContext context)
            => Tree(context.op.Text == "-" ? "MINUS_OP" : "PLUS_OP", Visit(context.expr()));

        public override IAstNode VisitMulExpr(SqlTestParser.MulExprContext context)
            => Tree(context.op.Text == "*" ? "MUL_OP" : "DIV_OP", Visit(context.expr(0)), Visit(context.expr(1)));

        public override IAstNode VisitAddExpr(SqlTestParser.AddExprContext context)
        {
            var op = context.op.Text == "+" ? "PLUS_OP" : context.op.Text == "-" ? "MINUS_OP" : "CONCAT_OP";
            return Tree(op, Visit(context.expr(0)), Visit(context.expr(1)));
        }

        public override IAstNode VisitRelExpr(SqlTestParser.RelExprContext context)
        {
            string op;
            switch (context.op.Text)
            {
                case "=": op = "EQ_OP"; break;
                case "<>": op = "NEQ_OP"; break;
                case ">": op = "GT_OP"; break;
                case ">=": op = "GE_OP"; break;
                case "<": op = "LT_OP"; break;
                default: op = "LE_OP"; break;
            }
            return Tree(op, Visit(context.expr(0)), Visit(context.expr(1)));
        }

        public override IAstNode VisitLikeExpr(SqlTestParser.LikeExprContext context)
            => Tree(context.not != null ? "NOT_LIKE_OP" : "LIKE_OP", Visit(context.expr(0)), Visit(context.expr(1)));

        public override IAstNode VisitInExpr(SqlTestParser.InExprContext context)
            => Tree(context.not != null ? "NOT_IN_OP" : "IN_OP", Visit(context.expr()), Visit(context.inPredicateValue()));

        public override IAstNode VisitExistsExpr(SqlTestParser.ExistsExprContext context)
            => Tree(context.not != null ? "NOT_EXISTS_OP" : "EXISTS_OP", Visit(context.selectStatement()));

        public override IAstNode VisitNullExpr(SqlTestParser.NullExprContext context)
            => Tree(context.not != null ? "NOT_NULL_OP" : "NULL_OP", Visit(context.expr()));

        public override IAstNode VisitNotExpr(SqlTestParser.NotExprContext context)
            => Tree("NOT_OP", Visit(context.expr()));

        public override IAstNode VisitAndExpr(SqlTestParser.AndExprContext context)
            => Tree("AND_OP", Visit(context.expr(0)), Visit(context.expr(1)));

        public override IAstNode VisitOrExpr(SqlTestParser.OrExprContext context)
            => Tree("OR_OP", Visit(context.expr(0)), Visit(context.expr(1)));

        public override IAstNode VisitInSelect(SqlTestParser.InSelectContext context)
            => Visit(context.selectStatement());

        public override IAstNode VisitInList(SqlTestParser.InListContext context)
            => Visit(context.inValueList());

        public override IAstNode VisitInValueList(SqlTestParser.InValueListContext context)
        {
            var n = Node("IN_VALUE_ARGS");
            foreach (var e in context.inValueArgs().expr())
                n.Add(Visit(e));
            return n;
        }

        // A parenthesized sub-SELECT used as an expression: expose the SELECT directly (as Hime's
        // promoted SELECT), so subquery navigation/assertions (BeSubquery, Table, ...) work uniformly
        // whether the subquery came via EXISTS/IN, an UPDATE operand, or a scalar expression.
        public override IAstNode VisitSelectExpr(SqlTestParser.SelectExprContext context)
            => Visit(context.selectStatement());

        // ---------------------------- constants / fields / params ----------------------------

        public override IAstNode VisitNullConst(SqlTestParser.NullConstContext context) => Node("NULL");

        public override IAstNode VisitBoolConst(SqlTestParser.BoolConstContext context)
            => Node(context.GetText() == "TRUE" ? "BOOLEAN_TRUE" : "BOOLEAN_FALSE");

        public override IAstNode VisitStringConst(SqlTestParser.StringConstContext context)
        {
            var t = context.STRINGDQ() ?? context.STRINGSQ();
            return Node(context.STRINGDQ() != null ? "STRINGDQ" : "STRINGSQ", t.GetText());
        }

        public override IAstNode VisitNumberConst(SqlTestParser.NumberConstContext context)
            => context.INT() != null ? Node("INT", context.INT().GetText()) : Node("REAL", context.REAL().GetText());

        public override IAstNode VisitDateConst(SqlTestParser.DateConstContext context)
        {
            var t = context.STRINGDQ() ?? context.STRINGSQ();
            return Tree("DATE_CONST", Node(context.STRINGDQ() != null ? "STRINGDQ" : "STRINGSQ", t.GetText()));
        }

        public override IAstNode VisitField(SqlTestParser.FieldContext context)
        {
            var n = Node("FIELD");
            foreach (ITerminalNode id in context.IDENTIFIER())
                n.Add(Node("IDENTIFIER", id.GetText()));
            return n;
        }

        public override IAstNode VisitParam(SqlTestParser.ParamContext context)
            => Tree("PARAM", Node("IDENTIFIER", context.IDENTIFIER().GetText()));

        // ---------------------------- function / aggregate calls ----------------------------

        public override IAstNode VisitFuncCall(SqlTestParser.FuncCallContext context)
        {
            if (context.mathFuncCall() != null) return Visit(context.mathFuncCall());
            if (context.twoArgFuncCall() != null) return Visit(context.twoArgFuncCall());
            if (context.dateFuncCall() != null) return Visit(context.dateFuncCall());
            if (context.castFuncCall() != null) return Visit(context.castFuncCall());
            if (context.strFuncCall() != null) return Visit(context.strFuncCall());
            if (context.boolStrFuncCall() != null) return Visit(context.boolStrFuncCall());
            return Visit(context.trimCall());
        }

        private static AstNodeImpl Name(Antlr4.Runtime.IToken t) => Node(t.Text, t.Text);

        public override IAstNode VisitMathFuncCall(SqlTestParser.MathFuncCallContext context)
            => Tree("MATH_FUNC_CALL", Name(context.name), Visit(context.expr()));

        public override IAstNode VisitTwoArgFuncCall(SqlTestParser.TwoArgFuncCallContext context)
            => Tree("TWO_ARG_FUNC_CALL", Name(context.name), Visit(context.expr(0)), Visit(context.expr(1)));

        public override IAstNode VisitDateFuncCall(SqlTestParser.DateFuncCallContext context)
            => Tree("DATE_FUNC_CALL", Name(context.name), Visit(context.expr()));

        public override IAstNode VisitCastFuncCall(SqlTestParser.CastFuncCallContext context)
            => Tree("CAST_FUNC_CALL", Name(context.name), Visit(context.expr()));

        public override IAstNode VisitStrFuncCall(SqlTestParser.StrFuncCallContext context)
            => Tree("STR_FUNC_CALL", Name(context.name), Visit(context.expr()));

        public override IAstNode VisitBoolStrFuncCall(SqlTestParser.BoolStrFuncCallContext context)
            => Tree("BOOL_STR_FUNC_CALL", Name(context.name), Visit(context.expr(0)), Visit(context.expr(1)));

        public override IAstNode VisitTrimCall(SqlTestParser.TrimCallContext context)
        {
            var n = Node("TRIM_CALL");
            if (context.trimSpecification() != null)
                n.Add(Node("TRIM_" + context.trimSpecification().GetText()));
            n.Add(Visit(context.expr()));
            return n;
        }

        public override IAstNode VisitAggrFuncCall(SqlTestParser.AggrFuncCallContext context)
        {
            var name = context.aggrFunc().GetText();
            // The set quantifier (DISTINCT / ALL), if any, is carried on the AGGR_FUNC node's value
            // rather than as a child, so the generic function-argument navigation (name at child 1,
            // arguments from child 2) is unaffected.
            var node = new AstNodeImpl("AGGR_FUNC", context.setQuantifier()?.GetText());
            node.Add(Node(name, name));
            node.Add(Visit(context.field()));
            return node;
        }

        public override IAstNode VisitAggrCountAll(SqlTestParser.AggrCountAllContext context)
            => Node("AGGR_COUNT_ALL");

        // ---------------------------- SELECT ----------------------------

        public override IAstNode VisitSelectStatement(SqlTestParser.SelectStatementContext context)
            => BuildSelect(context.selectCore(), context.orderBy(), context.limitOffset());

        // Assembles a SELECT node from its core plus the (optional) trailing ORDER BY / LIMIT that,
        // in a UNION, live at the union level rather than on the last SELECT.
        private IAstNode BuildSelect(SqlTestParser.SelectCoreContext core,
            SqlTestParser.OrderByContext orderBy, SqlTestParser.LimitOffsetContext limitOffset)
        {
            var n = Node("SELECT");
            if (core.setQuantifier() != null) n.Add(Visit(core.setQuantifier()));
            n.Add(Visit(core.selectList()));
            n.Add(Visit(core.tableExpression()));
            if (core.groupBy() != null) n.Add(Visit(core.groupBy()));
            if (core.havingClause() != null) n.Add(Visit(core.havingClause()));
            if (orderBy != null) n.Add(Visit(orderBy));
            if (limitOffset != null) n.Add(Visit(limitOffset));
            return n;
        }

        public override IAstNode VisitSetQuantifier(SqlTestParser.SetQuantifierContext context)
        {
            var t = context.GetText();
            return Tree("SET_QUANTIFIER", Node(t, t));
        }

        public override IAstNode VisitSelectAll(SqlTestParser.SelectAllContext context)
            => Tree("SELECT_LIST", Node("ASTERISK"));

        public override IAstNode VisitSelectItems(SqlTestParser.SelectItemsContext context)
            => Tree("SELECT_LIST", Visit(context.selectSublist()));

        public override IAstNode VisitSelectSublist(SqlTestParser.SelectSublistContext context)
        {
            var n = Node("SELECT_SUBLIST");
            foreach (var e in context.exprAlias())
                n.Add(Visit(e));
            return n;
        }

        public override IAstNode VisitExprAlias(SqlTestParser.ExprAliasContext context)
        {
            var n = Tree("EXPR_ALIAS", Visit(context.expr()));
            if (context.IDENTIFIER() != null)
                n.Add(Node("IDENTIFIER", context.IDENTIFIER().GetText()));
            return n;
        }

        public override IAstNode VisitTableExpression(SqlTestParser.TableExpressionContext context)
        {
            var n = Tree("TABLE_EXPRESSION", Visit(context.fromClause()));
            if (context.whereClause() != null)
                n.Add(Visit(context.whereClause()));
            return n;
        }

        public override IAstNode VisitFromClause(SqlTestParser.FromClauseContext context)
            => Tree("FROM_CLAUSE", Visit(context.tableReferenceList()));

        public override IAstNode VisitTableReferenceList(SqlTestParser.TableReferenceListContext context)
        {
            var n = Tree("TABLE_REFERENCE_LIST", Visit(context.tablePrimary()));
            foreach (var r in context.tableReference())
                n.Add(Visit(r));
            return n;
        }

        public override IAstNode VisitTablePrimary(SqlTestParser.TablePrimaryContext context)
        {
            var n = Tree("TABLE_PRIMARY", Visit(context.tableName()));
            if (context.IDENTIFIER() != null)
                n.Add(Node("IDENTIFIER", context.IDENTIFIER().GetText()));
            return n;
        }

        public override IAstNode VisitTableName(SqlTestParser.TableNameContext context)
            => Tree("TABLE_NAME", Node("IDENTIFIER", context.IDENTIFIER().GetText()));

        public override IAstNode VisitCrossJoinRef(SqlTestParser.CrossJoinRefContext context)
            => Tree("TABLE_REFERENCE", Visit(context.tablePrimary()));

        public override IAstNode VisitQualifiedJoinRef(SqlTestParser.QualifiedJoinRefContext context)
        {
            var n = Node("TABLE_REFERENCE");
            if (context.joinType() != null) n.Add(Visit(context.joinType()));
            n.Add(Visit(context.tablePrimary()));
            n.Add(Visit(context.joinCondition()));
            return n;
        }

        public override IAstNode VisitJoinType(SqlTestParser.JoinTypeContext context)
        {
            var t = context.GetText();
            string sym =
                t.StartsWith("INNER") ? "JOIN_TYPE_INNER" :
                t.StartsWith("LEFT") ? "JOIN_TYPE_LEFT" :
                t.StartsWith("RIGHT") ? "JOIN_TYPE_RIGHT" :
                "JOIN_TYPE_FULL"; // FULL, FULL OUTER, OUTER
            return Tree("JOIN_TYPE", Node(sym));
        }

        public override IAstNode VisitJoinCondition(SqlTestParser.JoinConditionContext context)
            => Tree("JOIN_CONDITION", Visit(context.expr()));

        public override IAstNode VisitWhereClause(SqlTestParser.WhereClauseContext context)
            => Tree("WHERE_CLAUSE", Visit(context.expr()));

        public override IAstNode VisitHavingClause(SqlTestParser.HavingClauseContext context)
            => Tree("HAVING_CLAUSE", Visit(context.expr()));

        public override IAstNode VisitGroupBy(SqlTestParser.GroupByContext context)
        {
            var n = Node("GROUP_SPECIFICATION_LIST");
            foreach (var g in context.groupSpecificationList().groupSpecification())
                n.Add(Tree("GROUP_SPECIFICATION", Visit(g.expr())));
            return n;
        }

        public override IAstNode VisitOrderBy(SqlTestParser.OrderByContext context)
            => Visit(context.sortSpecificationList());

        public override IAstNode VisitSortSpecificationList(SqlTestParser.SortSpecificationListContext context)
        {
            var n = Node("SORT_SPECIFICATION_LIST");
            foreach (var s in context.sortSpecification())
                n.Add(Visit(s));
            return n;
        }

        public override IAstNode VisitSortSpecification(SqlTestParser.SortSpecificationContext context)
        {
            var n = Tree("SORT_SPECIFICATION", Visit(context.expr()));
            if (context.orderingSpecification() != null)
            {
                var d = context.orderingSpecification().GetText();
                n.Add(Node(d, d));
            }
            return n;
        }

        public override IAstNode VisitLimitOffset(SqlTestParser.LimitOffsetContext context)
        {
            var n = Node("LIMIT_OFFSET");
            foreach (var c in context.children)
            {
                if (c is SqlTestParser.LimitContext lc) n.Add(Visit(lc));
                else if (c is SqlTestParser.OffsetContext oc) n.Add(Visit(oc));
            }
            return n;
        }

        public override IAstNode VisitLimit(SqlTestParser.LimitContext context)
            => Tree("LIMIT", Node("INT", context.INT().GetText()));

        public override IAstNode VisitOffset(SqlTestParser.OffsetContext context)
            => Tree("OFFSET", Node("INT", context.INT().GetText()));

        // ---------------------------- INSERT / UPDATE / DELETE ----------------------------

        public override IAstNode VisitInsertStatement(SqlTestParser.InsertStatementContext context)
            => Tree("INSERT", Visit(context.tableName()), Visit(context.fieldsList()), Visit(context.toInsert()));

        public override IAstNode VisitFieldsList(SqlTestParser.FieldsListContext context)
            => Visit(context.fields());

        public override IAstNode VisitFields(SqlTestParser.FieldsContext context)
        {
            var n = Node("FIELDS");
            foreach (var f in context.field())
                n.Add(Visit(f));
            return n;
        }

        public override IAstNode VisitToInsert(SqlTestParser.ToInsertContext context)
            => context.insertValuesList() != null ? Visit(context.insertValuesList()) : Visit(context.selectStatement());

        public override IAstNode VisitInsertValuesList(SqlTestParser.InsertValuesListContext context)
            => Tree("INSERT_VALUES_LIST", Visit(context.insertValues()));

        public override IAstNode VisitInsertValues(SqlTestParser.InsertValuesContext context)
        {
            var n = Node("INSERT_VALUES");
            foreach (var v in context.insertValue())
                n.Add(Visit(v));
            return n;
        }

        public override IAstNode VisitInsertValue(SqlTestParser.InsertValueContext context)
            => Tree("INSERT_VALUE", context.constant() != null ? Visit(context.constant()) : Visit(context.param()));

        public override IAstNode VisitUpdateStatement(SqlTestParser.UpdateStatementContext context)
        {
            var n = Tree("UPDATE", Visit(context.tableName()), Visit(context.updateList()));
            if (context.whereClause() != null)
                n.Add(Visit(context.whereClause()));
            return n;
        }

        public override IAstNode VisitUpdateList(SqlTestParser.UpdateListContext context)
        {
            var n = Node("UPDATE_LIST");
            foreach (var a in context.updateAssign())
                n.Add(Visit(a));
            return n;
        }

        public override IAstNode VisitUpdateAssign(SqlTestParser.UpdateAssignContext context)
            => Tree("UPDATE_ASSIGN", Visit(context.field()), Visit(context.updateOperand()));

        public override IAstNode VisitUpdateOperand(SqlTestParser.UpdateOperandContext context)
            => context.expr() != null ? Visit(context.expr()) : Visit(context.selectStatement());

        public override IAstNode VisitDeleteStatement(SqlTestParser.DeleteStatementContext context)
        {
            var n = Tree("DELETE", Visit(context.tableName()));
            if (context.whereClause() != null)
                n.Add(Visit(context.whereClause()));
            return n;
        }

        // ---------------------------- DDL ----------------------------

        public override IAstNode VisitCreateTable(SqlTestParser.CreateTableContext context)
            => Tree("CREATE_TABLE", Visit(context.tableName()), Visit(context.createTableClauses()));

        public override IAstNode VisitCreateTableClauses(SqlTestParser.CreateTableClausesContext context)
        {
            var n = Node("CREATE_TABLE_CLAUSES");
            foreach (var c in context.createTableClause())
                n.Add(Visit(c));
            return n;
        }

        public override IAstNode VisitCreateTableClause(SqlTestParser.CreateTableClauseContext context)
            => Tree("CREATE_TABLE_CLAUSE", context.fieldDefinition() != null ? Visit(context.fieldDefinition()) : Visit(context.foreignKeyDefinition()));

        public override IAstNode VisitFieldDefinition(SqlTestParser.FieldDefinitionContext context)
        {
            var n = Tree("FIELD_DEFINITION", Visit(context.fieldDefinitionName()), Visit(context.fieldDefinitionType()));
            if (context.fieldDefinitionFlags() != null)
                n.Add(Visit(context.fieldDefinitionFlags()));
            return n;
        }

        public override IAstNode VisitFieldDefinitionName(SqlTestParser.FieldDefinitionNameContext context)
            => Tree("FIELD_DEFINITION_NAME", Node("IDENTIFIER", context.IDENTIFIER().GetText()));

        public override IAstNode VisitFieldDefinitionType(SqlTestParser.FieldDefinitionTypeContext context)
        {
            var n = Tree("FIELD_DEFINITION_TYPE", Node("IDENTIFIER", context.IDENTIFIER().GetText()));
            if (context.fieldDefinitionSize() != null)
                n.Add(Visit(context.fieldDefinitionSize()));
            return n;
        }

        public override IAstNode VisitFieldDefinitionSize(SqlTestParser.FieldDefinitionSizeContext context)
        {
            var n = Node("FIELD_DEFINITION_SIZE");
            foreach (ITerminalNode i in context.INT())
                n.Add(Node("INT", i.GetText()));
            return n;
        }

        public override IAstNode VisitFieldDefinitionFlags(SqlTestParser.FieldDefinitionFlagsContext context)
        {
            var n = Node("FIELD_DEFINITION_FLAGS");
            foreach (var f in context.fieldDefinitionFlag())
                n.Add(Tree("FIELD_DEFINITION_FLAG", Visit(f)));
            return n;
        }

        public override IAstNode VisitFlagNotNull(SqlTestParser.FlagNotNullContext context) => Node("FIELD_DEFINITION_FLAG_NOT_NULL");
        public override IAstNode VisitFlagUnique(SqlTestParser.FlagUniqueContext context) => Node("FIELD_DEFINITION_FLAG_UNIQUE");
        public override IAstNode VisitFlagPrimaryKey(SqlTestParser.FlagPrimaryKeyContext context) => Node("FIELD_DEFINITION_FLAG_PRIMARY_KEY");
        public override IAstNode VisitFlagAutoincrement(SqlTestParser.FlagAutoincrementContext context) => Node("FIELD_DEFINITION_FLAG_AUTOINCREMENT");

        public override IAstNode VisitFlagForeignKey(SqlTestParser.FlagForeignKeyContext context)
            => Tree("FIELD_DEFINITION_FLAG_FOREIGN_KEY", Visit(context.tableName()), Visit(context.fieldDefinitionName()));

        public override IAstNode VisitFlagDefault(SqlTestParser.FlagDefaultContext context)
            => Tree("FIELD_DEFINITION_FLAG_DEFAULT", Visit(context.expr()));

        public override IAstNode VisitForeignKeyDefinition(SqlTestParser.ForeignKeyDefinitionContext context)
            => Tree("FOREIGN_KEY_DEFINITION",
                Visit(context.fieldDefinitionName(0)),
                Visit(context.tableName()),
                Visit(context.fieldDefinitionName(1)));

        public override IAstNode VisitCreateView(SqlTestParser.CreateViewContext context)
            => Tree("CREATE_VIEW", Visit(context.tableName()), Visit(context.selectStatement()));

        public override IAstNode VisitCreateIndex(SqlTestParser.CreateIndexContext context)
            => Tree("CREATE_INDEX",
                Visit(context.tableName(0)),
                Visit(context.tableName(1)),
                Visit(context.sortSpecificationList()));

        public override IAstNode VisitDropTable(SqlTestParser.DropTableContext context)
        {
            var n = Node("DROP_TABLE");
            if (context.ifExist() != null) n.Add(Node("IF_EXIST"));
            n.Add(Visit(context.tableName()));
            return n;
        }

        public override IAstNode VisitDropView(SqlTestParser.DropViewContext context)
        {
            var n = Node("DROP_VIEW");
            if (context.ifExist() != null) n.Add(Node("IF_EXIST"));
            n.Add(Visit(context.tableName()));
            return n;
        }

        public override IAstNode VisitDropIndex(SqlTestParser.DropIndexContext context)
        {
            var n = Node("DROP_INDEX");
            if (context.ifExist() != null) n.Add(Node("IF_EXIST"));
            n.Add(Visit(context.tableName(0)));
            n.Add(Visit(context.tableName(1)));
            return n;
        }

        public override IAstNode VisitAlterTable(SqlTestParser.AlterTableContext context)
            => Tree("ALTER_TABLE", Visit(context.tableName()), Visit(context.alterTableClause()));

        public override IAstNode VisitAlterTableClause(SqlTestParser.AlterTableClauseContext context)
        {
            IAstNode inner =
                context.addFieldClause() != null ? Visit(context.addFieldClause()) :
                context.dropFieldClause() != null ? Visit(context.dropFieldClause()) :
                Visit(context.addConstraintClause());
            return Tree("ALTER_TABLE_CLAUSE", inner);
        }

        public override IAstNode VisitAddFieldClause(SqlTestParser.AddFieldClauseContext context)
            => Tree("ADD_FIELD_CLAUSE", Visit(context.fieldDefinition()));

        public override IAstNode VisitDropFieldClause(SqlTestParser.DropFieldClauseContext context)
            => Tree("DROP_FIELD_CLAUSE", Visit(context.fieldDefinitionName()));

        public override IAstNode VisitAddConstraintClause(SqlTestParser.AddConstraintClauseContext context)
            => Tree("ADD_CONTRAINT_CLAUSE", Visit(context.foreignKeyDefinition()));

        // ---------------------------- UNION ----------------------------

        public override IAstNode VisitUnionStatement(SqlTestParser.UnionStatementContext context)
        {
            var n = Node("UNION");
            foreach (var c in context.children)
            {
                if (c is SqlTestParser.SelectCoreContext sc) n.Add(BuildSelect(sc, null, null));
                else if (c is SqlTestParser.UnionOpContext uc) n.Add(Visit(uc));
                else if (c is SqlTestParser.OrderByContext oc) n.Add(Visit(oc));
            }
            return n;
        }

        public override IAstNode VisitUnionOp(SqlTestParser.UnionOpContext context)
            => Tree("UNION_OP", Node(context.GetText().Contains("ALL") ? "UNION_ALL" : "UNION_DISTINCT"));
    }
}
