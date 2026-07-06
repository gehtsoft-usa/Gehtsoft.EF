grammar SqlTest;

/*
 * ANTLR4 grammar for the Gehtsoft.EF test SQL dialect (parsing generated SQL to
 * assert its structure). Ported from the Hime grammar (Gehtsoft.EF.Test/sql.gram)
 * as part of the Hime -> ANTLR4 migration.
 *
 * Consumers (the test assertion helpers) are rewritten natively against these
 * typed contexts, so the tree shape need not mimic Hime.
 */

// ============================ PARSER ============================

root : statement+ EOF ;

statement
    : selectStatement
    | unionStatement
    | insertStatement
    | updateStatement
    | deleteStatement
    | createTable
    | createView
    | createIndex
    | alterTable
    | dropTable
    | dropIndex
    | dropView
    | debugExpr
    | nop
    ;

// ---------------------------- expressions ----------------------------

expr
    : op=('-'|'+') expr                                    # unarySignExpr
    | expr op=('*'|'/') expr                               # mulExpr
    | expr op=('+'|'-'|'||') expr                          # addExpr
    | expr op=('='|'<>'|'>'|'>='|'<'|'<=') expr            # relExpr
    | expr not='NOT'? 'LIKE' expr                          # likeExpr
    | expr not='NOT'? 'IN' inPredicateValue                # inExpr
    | not='NOT'? 'EXISTS' '(' selectStatement ')'          # existsExpr
    | expr 'IS' not='NOT'? 'NULL'                          # nullExpr
    | 'NOT' expr                                           # notExpr
    | expr 'AND' expr                                      # andExpr
    | expr 'OR' expr                                       # orExpr
    | primary                                              # primaryExpr
    ;

primary
    : constant
    | field
    | param
    | funcCall
    | aggrCall
    | selectExpr
    | '(' expr ')'
    ;

inPredicateValue
    : '(' selectStatement ')'    # inSelect
    | inValueList                # inList
    ;
inValueList : '(' inValueArgs ')' ;
inValueArgs : expr (',' expr)* ;

selectExpr : '(' selectStatement ')' ;

// ---------------------------- constants / fields / params ----------------------------

constant
    : 'NULL'                                # nullConst
    | ('TRUE' | 'FALSE')                    # boolConst
    | (STRINGDQ | STRINGSQ)                 # stringConst
    | (INT | REAL)                          # numberConst
    | 'D' (STRINGDQ | STRINGSQ)             # dateConst
    ;

field : IDENTIFIER ('.' IDENTIFIER)? ;
param : (':' | '?' | '@') IDENTIFIER ;

// ---------------------------- function calls ----------------------------

funcCall
    : mathFuncCall
    | twoArgFuncCall
    | dateFuncCall
    | castFuncCall
    | strFuncCall
    | boolStrFuncCall
    | trimCall
    ;

mathFuncCall    : name=('ABS'|'ROUND') '(' expr ')' ;
twoArgFuncCall  : name=('ROUND'|'LEFT') '(' expr ',' expr ')' ;
dateFuncCall    : name=('YEAR'|'MONTH'|'DAY'|'HOUR'|'MINUTE'|'SECOND') '(' expr ')' ;
castFuncCall    : name=('TOSTRING'|'TOINT'|'TODOUBLE'|'TODATE'|'TOTIMESTAMP') '(' expr ')' ;
strFuncCall     : name=('TRIM'|'LTRIM'|'RTRIM'|'UPPER'|'LOWER'|'LENGTH') '(' expr ')' ;
boolStrFuncCall : name=('CONTAINS'|'ENDSWITH'|'STARTSWITH') '(' expr ',' expr ')' ;
trimCall        : 'TRIM' '(' trimSpecification? expr ')' ;
trimSpecification : 'LEADING' | 'TRAILING' | 'BOTH' ;

// ---------------------------- aggregates ----------------------------

aggrCall
    : aggrFunc '(' setQuantifier? field ')'    # aggrFuncCall
    | AGGR_COUNT_ALL                           # aggrCountAll
    ;
aggrFunc : 'COUNT' | 'MAX' | 'MIN' | 'AVG' | 'SUM' ;

// ---------------------------- SELECT ----------------------------

selectStatement
    : selectCore orderBy? limitOffset? eos
    ;

// The SELECT body without a trailing ORDER BY / LIMIT. Used inside UNION so that a UNION's
// trailing ORDER BY binds to the whole union rather than being greedily eaten by the last SELECT.
selectCore
    : 'SELECT' setQuantifier? selectList tableExpression groupBy? havingClause?
    ;

setQuantifier : 'DISTINCT' | 'ALL' ;

selectList
    : '*'            # selectAll
    | selectSublist  # selectItems
    ;
selectSublist : exprAlias (',' exprAlias)* ;
exprAlias : expr ('AS' IDENTIFIER)? ;

tableExpression : fromClause whereClause? ;
fromClause : 'FROM' tableReferenceList ;
tableReferenceList : tablePrimary tableReference* ;

tableReference
    : ',' tablePrimary                            # crossJoinRef
    | joinType? 'JOIN' tablePrimary joinCondition # qualifiedJoinRef
    ;

tablePrimary : tableName ('AS' IDENTIFIER)? ;
tableName : IDENTIFIER ;

joinType : 'INNER' | 'LEFT' 'OUTER'? | 'RIGHT' 'OUTER'? | ('FULL' 'OUTER'?) | 'OUTER' ;
joinCondition : 'ON' expr ;

whereClause : 'WHERE' expr ;
havingClause : 'HAVING' expr ;

groupBy : 'GROUP' 'BY' groupSpecificationList ;
groupSpecificationList : groupSpecification (',' groupSpecification)* ;
groupSpecification : expr ;

orderBy : 'ORDER' 'BY' sortSpecificationList ;
sortSpecificationList : sortSpecification (',' sortSpecification)* ;
sortSpecification : expr orderingSpecification? ;
orderingSpecification : 'ASC' | 'DESC' ;

limitOffset : limit offset? | offset limit? ;
limit : 'LIMIT' INT ;
offset : 'OFFSET' INT ;

eos : ';'* ;

// ---------------------------- INSERT / UPDATE / DELETE ----------------------------

insertStatement : 'INSERT' 'INTO' tableName fieldsList toInsert ;
fieldsList : '(' fields ')' ;
fields : field (',' field)* ;
toInsert : insertValuesList | selectStatement ;
insertValuesList : 'VALUES' '(' insertValues ')' ;
insertValues : insertValue (',' insertValue)* ;
insertValue : constant | param ;

updateStatement : 'UPDATE' tableName 'SET' updateList whereClause? ;
updateList : updateAssign (',' updateAssign)* ;
updateAssign : field '=' updateOperand ;
updateOperand : expr | '(' selectStatement ')' ;

deleteStatement : 'DELETE' 'FROM' tableName whereClause? ;

// ---------------------------- DDL ----------------------------

createTable : 'CREATE' 'TABLE' tableName '(' createTableClauses ')' ;
createTableClauses : createTableClause (',' createTableClause)* ;
createTableClause : fieldDefinition | foreignKeyDefinition ;

createView : 'CREATE' 'VIEW' tableName 'AS' selectStatement ;
createIndex : 'CREATE' 'INDEX' tableName 'ON' tableName '(' sortSpecificationList ')' ;

ifExist : 'IF' 'EXISTS' ;
dropTable : 'DROP' 'TABLE' ifExist? tableName ;
dropView : 'DROP' 'VIEW' ifExist? tableName ;
dropIndex : 'DROP' 'INDEX' ifExist? tableName 'ON' tableName ;

alterTable : 'ALTER' 'TABLE' tableName alterTableClause ;
alterTableClause : addFieldClause | dropFieldClause | addConstraintClause ;
addFieldClause : 'ADD' 'COLUMN' fieldDefinition ;
dropFieldClause : 'DROP' 'COLUMN' fieldDefinitionName ;
addConstraintClause : 'ADD' foreignKeyDefinition ;

fieldDefinition : fieldDefinitionName fieldDefinitionType fieldDefinitionFlags? ;
fieldDefinitionName : IDENTIFIER ;
fieldDefinitionType : IDENTIFIER fieldDefinitionSize? ;
fieldDefinitionSize : '(' INT (',' INT)? ')' ;
fieldDefinitionFlags : fieldDefinitionFlag+ ;
fieldDefinitionFlag
    : 'NOT' 'NULL'                                                    # flagNotNull
    | 'UNIQUE'                                                        # flagUnique
    | 'PRIMARY' 'KEY'                                                 # flagPrimaryKey
    | 'FOREIGN' 'KEY' 'REFERENCES' tableName '(' fieldDefinitionName ')'  # flagForeignKey
    | 'DEFAULT' expr                                                  # flagDefault
    | 'AUTOINCREMENT'                                                 # flagAutoincrement
    ;
foreignKeyDefinition
    : 'FOREIGN' 'KEY' '(' fieldDefinitionName ')' 'REFERENCES' tableName '(' fieldDefinitionName ')'
    ;

// ---------------------------- DEBUG / UNION ----------------------------

debugExpr : 'DEBUG' expr ;

unionStatement : selectCore unionOp selectCore (unionOp selectCore)* orderBy? eos ;
unionOp : 'UNION' 'ALL'? ;

nop : ';' ;

// ============================ LEXER ============================

AGGR_COUNT_ALL : 'COUNT(*)' ;

INT  : [0-9]+ ;
REAL : [0-9]+ '.' [0-9]* | '.' [0-9]+ ;

STRINGDQ
    : '"' ( ~["\\] | '\\' ["\\rnbt] | '\\' [0-9][0-9][0-9] | '\\' 'x' [0-9a-fA-F][0-9a-fA-F] )* '"'
    ;
STRINGSQ
    : '\'' ( ~['\\] | '\\' ["\\rnbt] | '\\' [0-9][0-9][0-9] | '\\' 'x' [0-9a-fA-F][0-9a-fA-F] )* '\''
    ;

IDENTIFIER : [a-zA-Z_] [a-zA-Z0-9_]* ;

COMMENT_LINE : '--' ~[\r\n]* -> skip ;
WS : [ \t\r\n]+ -> skip ;
