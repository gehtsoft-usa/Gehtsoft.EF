grammar Sql;

/*
 * ANTLR4 grammar for the Gehtsoft.EF SQL scripting DSL.
 * Ported from the Hime grammar (archive/hime-grammars/Sql.gram) as part of the
 * Hime -> ANTLR4 migration (see CLAUDE/ANTLR_MIGRATION.md).
 *
 * Design notes:
 *  - Consumers are rewritten natively against these typed contexts, so the tree
 *    shape need not mimic Hime's promoted/dropped nodes.
 *  - The layered Hime expression-precedence chain is folded into a single
 *    left-recursive `expr` rule; alternatives are ordered tightest -> loosest.
 *  - String tokens are delivered RAW (quotes included, no unescaping) to match
 *    the walker, which only strips the surrounding quotes.
 *  - Keywords are case-sensitive (all uppercase), matching Hime.
 */

// ============================ PARSER ============================

program : statementList EOF ;

statementList : statement+ ;                       // Hime ROOT

statement
    : selectStatement
    | insertStatement
    | updateStatement
    | deleteStatement
    | setStatement
    | declareCursorStatement      // must precede declareStatement (DECLARE x CURSOR ...)
    | declareStatement
    | importStatement
    | exitStatement
    | ifStatement
    | whileStatement
    | forStatement
    | switchStatement
    | continueStatement
    | breakStatement
    | addFieldStatement
    | addRowStatement
    | openCursorStatement
    | closeCursorStatement
    | assignStatement
    | nop
    ;

// ---------------------------- expressions ----------------------------

expr
    : op=('-'|'+') expr                                  # UnarySignExpr
    | expr op=('*'|'/') expr                             # MulExpr
    | expr op=('+'|'-'|'||') expr                        # AddExpr
    // ':=' binds looser than arithmetic but tighter than comparisons/IS NULL, so that
    // `?p := FETCH() IS NOT NULL` parses as `(?p := FETCH()) IS NOT NULL` (matches Hime).
    | expr ':=' expr                                     # AssignExpr
    | expr op=('='|'<>'|'>'|'>='|'<'|'<=') expr          # RelExpr
    | expr not='NOT'? 'LIKE' expr                        # LikeExpr
    | expr not='NOT'? 'IN' inPredicateValue             # InExpr
    | expr 'IS' not='NOT'? 'NULL'                        # NullExpr
    | 'NOT' expr                                         # NotExpr
    | expr 'AND' expr                                    # AndExpr
    | expr 'OR' expr                                     # OrExpr
    | primary                                            # PrimaryExpr
    ;

primary
    : constant
    | field
    | globalParameter
    | funcCall
    | aggrCall
    | selectExpr
    | '(' expr ')'
    ;

inPredicateValue
    : '(' selectStatement ')'    # InSelect
    | inValueList                # InList
    ;

inValueList : '(' inValueArgs ')' ;
inValueArgs : expr (',' expr)* ;

selectExpr : '(' selectStatement ')' ;

// ---------------------------- constants ----------------------------

constant
    : 'NULL'                                # NullConst
    | ('TRUE' | 'FALSE')                    # BoolConst
    | (STRINGDQ | STRINGSQ)                 # StringConst
    | (INT | REAL)                          # NumberConst
    | 'DATETIME' (STRINGDQ | STRINGSQ)      # DatetimeConst
    | 'DATE' (STRINGDQ | STRINGSQ)          # DateConst
    ;

// ---------------------------- fields / params / types ----------------------------

field : IDENTIFIER ('.' IDENTIFIER)? ;

globalParameter : GLOBAL_PARAMETER_NAME ('AS' parameterType)? ;
globalParameterSimple : GLOBAL_PARAMETER_NAME ;

baseType : 'STRING' | 'INTEGER' | 'DOUBLE' | 'BOOLEAN' | 'DATETIME' ;
parameterType : baseType | 'ROW' | 'ROWSET' ;

// ---------------------------- function calls ----------------------------

funcCall
    : mathFuncCall
    | boolStrFuncCall
    | castFuncCall
    | strFuncCall
    | trimCall
    | lastResultCall
    | rowsCountCall
    | getRowCall
    | getFieldCall
    | newRowsetCall
    | newRowCall
    | fetchCall
    ;

mathFuncCall    : name='ABS' '(' expr ')' ;
castFuncCall    : name=('TOSTRING'|'TOINT'|'TODOUBLE'|'TODATE'|'TOTIMESTAMP') '(' expr ')' ;
strFuncCall     : name=('LTRIM'|'RTRIM'|'UPPER'|'LOWER') '(' expr ')' ;
boolStrFuncCall : name=('CONTAINS'|'ENDSWITH'|'STARTSWITH') '(' expr ',' expr ')' ;
trimCall        : 'TRIM' '(' trimSpecification? expr ')' ;
trimSpecification : 'LEADING' | 'TRAILING' | 'BOTH' ;

lastResultCall  : 'LAST_RESULT' '(' ')' ;
rowsCountCall   : 'ROWS_COUNT' '(' expr ')' ;
getRowCall      : 'GET_ROW' '(' expr ',' expr ')' ;
getFieldCall    : 'GET_FIELD' '(' expr ',' expr ',' baseType ')' ;
newRowsetCall   : 'NEW_ROWSET' '(' ')' ;
newRowCall      : 'NEW_ROW' '(' ')' ;
fetchCall       : 'FETCH' '(' globalParameterSimple ')' ;

// ---------------------------- aggregates ----------------------------

aggrCall
    : aggrFunc '(' field ')'    # AggrFuncCall
    | AGGR_COUNT_ALL            # AggrCountAll
    ;
aggrFunc : 'COUNT' | 'MAX' | 'MIN' | 'AVG' | 'SUM' ;

// ---------------------------- SELECT ----------------------------

selectStatement
    : 'SELECT' setQuantifier? selectList tableExpression groupBy? orderBy? limitOffset?
    ;

setQuantifier : 'DISTINCT' | 'ALL' ;

selectList
    : '*'            # SelectAll
    | selectSublist  # SelectItems
    ;
selectSublist : exprAlias (',' exprAlias)* ;
exprAlias : expr ('AS' IDENTIFIER)? ;

tableExpression : fromClause whereClause? ;
fromClause : 'FROM' tableReferenceList ;
tableReferenceList : tableReference (',' tableReference)* ;

tableReference
    : tablePrimary                                                   # PrimaryTableRef
    | tableReference joinType? 'JOIN' tablePrimary joinCondition     # QualifiedJoinRef
    | tableReference 'AUTO' 'JOIN' tablePrimary                      # AutoJoinRef
    ;

tablePrimary : IDENTIFIER ('AS' IDENTIFIER)? ;

joinType : 'INNER' | outerJoinType 'OUTER'? ;
outerJoinType : 'LEFT' | 'RIGHT' | 'FULL' ;
joinCondition : 'ON' expr ;

whereClause : 'WHERE' expr ;

groupBy : GROUP_BY groupSpecificationList ;
groupSpecificationList : groupSpecification (',' groupSpecification)* ;
groupSpecification : expr ;

orderBy : ORDER_BY sortSpecificationList ;
sortSpecificationList : sortSpecification (',' sortSpecification)* ;
sortSpecification : expr orderingSpecification? ;
orderingSpecification : 'ASC' | 'DESC' ;

limitOffset : limit offset? | offset limit? ;
limit : 'LIMIT' INT ;
offset : 'OFFSET' INT ;

// A standalone ';' is a no-op statement (nop); trailing ';' after a SELECT is likewise a nop.

// ---------------------------- INSERT / UPDATE / DELETE ----------------------------

insertStatement : 'INSERT' 'INTO' IDENTIFIER fieldsList toInsert ;
fieldsList : '(' fields ')' ;
fields : field (',' field)* ;
toInsert : valuesList | selectStatement ;
valuesList : 'VALUES' '(' values ')' ;
values : constant (',' constant)* ;

updateStatement : 'UPDATE' IDENTIFIER 'SET' updateList whereClause? ;
updateList : updateAssign (',' updateAssign)* ;
updateAssign : field '=' updateOperand ;
updateOperand : expr ;   // a parenthesized SELECT is already covered by expr -> selectExpr

deleteStatement : 'DELETE' 'FROM' IDENTIFIER whereClause? ;

// ---------------------------- SET / DECLARE / IMPORT ----------------------------

setStatement : 'SET' setList ;
setList : setItem (',' setItem)* ;
setItem : IDENTIFIER ('=' expr)? ;

declareStatement : 'DECLARE' declareList ;
importStatement : 'IMPORT' declareList ;
declareList : declareItem (',' declareItem)* ;
declareItem : IDENTIFIER 'AS' parameterType ;

// ---------------------------- control flow ----------------------------

exitStatement : 'EXIT' ('WITH' expr)* ;

ifStatement
    : 'IF' expr 'THEN' statementList
      ('ELSIF' expr 'THEN' statementList)*
      ('ELSE' statementList)?
      'END' 'IF'
    ;

whileStatement : 'WHILE' expr 'LOOP' statementList 'END' 'LOOP' ;

forStatement
    : 'FOR' statementList 'WHILE' expr 'NEXT' statementList 'LOOP' statementList 'END' 'LOOP'
    ;

switchStatement
    : 'SWITCH' expr
      ('CASE' expr ':' statementList?)*
      ('OTHERWISE' ':' statementList)?
      'END' 'SWITCH'
    ;

breakStatement : 'BREAK' ;
continueStatement : 'CONTINUE' ;

// ---------------------------- rows / cursors ----------------------------

addFieldStatement : 'ADD' 'FIELD' expr 'WITH' expr 'TO' globalParameterSimple ;
addRowStatement : 'ADD' 'ROW' expr 'TO' globalParameterSimple ;

declareCursorStatement : 'DECLARE' IDENTIFIER 'CURSOR' 'FOR' selectStatement ;
openCursorStatement : 'OPEN' 'CURSOR' globalParameterSimple ;
closeCursorStatement : 'CLOSE' 'CURSOR' globalParameterSimple ;

assignStatement : globalParameterSimple ':=' expr ;

nop : ';' ;

// ============================ LEXER ============================

// compound / space-containing tokens (must precede keyword & IDENTIFIER rules)
GROUP_BY       : 'GROUP' [ \t\r\n]+ 'BY' ;
ORDER_BY       : 'ORDER' [ \t\r\n]+ 'BY' ;
AGGR_COUNT_ALL : 'COUNT(*)' ;

ASSIGN : ':=' ;

INT  : [0-9]+ ;
REAL : [0-9]+ '.' [0-9]* | '.' [0-9]+ ;

STRINGDQ
    : '"' ( ~["\\]
          | '\\' ["\\rnbt]
          | '\\' [0-9] [0-9] [0-9]
          | '\\' 'x' [0-9a-fA-F] [0-9a-fA-F]
          )* '"'
    ;

STRINGSQ
    : '\'' ( ~['\\]
           | '\\' ["\\rnbt]
           | '\\' [0-9] [0-9] [0-9]
           | '\\' 'x' [0-9a-fA-F] [0-9a-fA-F]
           )* '\''
    ;

GLOBAL_PARAMETER_NAME : '?' [a-zA-Z_] [a-zA-Z0-9_]* ;

IDENTIFIER : [a-zA-Z_] [a-zA-Z0-9_]* ;

COMMENT_LINE : '--' ~[\r\n  ]* -> skip ;
WS : [ \t\r\n  ]+ -> skip ;
