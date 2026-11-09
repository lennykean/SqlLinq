enum SymbolConstants : int
{
    SYMBOL_EOF = 0, // (EOF)
    SYMBOL_ERROR = 1, // (Error)
    SYMBOL_COMMENT = 2, // Comment
    SYMBOL_NEWLINE = 3, // NewLine
    SYMBOL_WHITESPACE = 4, // Whitespace
    SYMBOL_TIMESDIV = 5, // '*/'
    SYMBOL_MINUSMINUS = 6, // '--'
    SYMBOL_DIVTIMES = 7, // '/*'
    SYMBOL_EXCLAMEQ = 8, // '!='
    SYMBOL_LPAREN = 9, // '('
    SYMBOL_RPAREN = 10, // ')'
    SYMBOL_TIMES = 11, // '*'
    SYMBOL_PLUS = 12, // '+'
    SYMBOL_COMMA = 13, // ','
    SYMBOL_MINUS = 14, // '-'
    SYMBOL_DIV = 15, // '/'
    SYMBOL_LT = 16, // '<'
    SYMBOL_LTEQ = 17, // '<='
    SYMBOL_LTGT = 18, // '<>'
    SYMBOL_EQ = 19, // '='
    SYMBOL_GT = 20, // '>'
    SYMBOL_GTEQ = 21, // '>='
    SYMBOL_ALL = 22, // ALL
    SYMBOL_AND = 23, // AND
    SYMBOL_ANY = 24, // ANY
    SYMBOL_AS = 25, // AS
    SYMBOL_ASC = 26, // ASC
    SYMBOL_AVG = 27, // Avg
    SYMBOL_BETWEEN = 28, // BETWEEN
    SYMBOL_BOOLEANLITERAL = 29, // BooleanLiteral
    SYMBOL_BY = 30, // BY
    SYMBOL_COUNT = 31, // Count
    SYMBOL_DATELITERAL = 32, // DateLiteral
    SYMBOL_DESC = 33, // DESC
    SYMBOL_DISTINCT = 34, // DISTINCT
    SYMBOL_ESCAPE = 35, // ESCAPE
    SYMBOL_EXISTS = 36, // EXISTS
    SYMBOL_FROM = 37, // FROM
    SYMBOL_FUNCTION = 38, // Function
    SYMBOL_GROUP = 39, // GROUP
    SYMBOL_HAVING = 40, // HAVING
    SYMBOL_ID = 41, // Id
    SYMBOL_IN = 42, // IN
    SYMBOL_INNER = 43, // INNER
    SYMBOL_INTEGERLITERAL = 44, // IntegerLiteral
    SYMBOL_IS = 45, // IS
    SYMBOL_JOIN = 46, // JOIN
    SYMBOL_LEFT = 47, // LEFT
    SYMBOL_LIKE = 48, // LIKE
    SYMBOL_MAX = 49, // Max
    SYMBOL_MIN = 50, // Min
    SYMBOL_NOT = 51, // NOT
    SYMBOL_NULL = 52, // NULL
    SYMBOL_ON = 53, // ON
    SYMBOL_OR = 54, // OR
    SYMBOL_ORDER = 55, // ORDER
    SYMBOL_REALLITERAL = 56, // RealLiteral
    SYMBOL_RIGHT = 57, // RIGHT
    SYMBOL_SELECT = 58, // SELECT
    SYMBOL_SOME = 59, // SOME
    SYMBOL_STDEV = 60, // StDev
    SYMBOL_STDEVP = 61, // StDevP
    SYMBOL_STRINGLITERAL = 62, // StringLiteral
    SYMBOL_SUM = 63, // Sum
    SYMBOL_UNION = 64, // UNION
    SYMBOL_VAR = 65, // Var
    SYMBOL_VARP = 66, // VarP
    SYMBOL_WHERE = 67, // WHERE
    SYMBOL_ADDEXP = 68, // <Add Exp>
    SYMBOL_AGGREGATE = 69, // <Aggregate>
    SYMBOL_ANDEXP = 70, // <And Exp>
    SYMBOL_COLUMNALIAS = 71, // <Column Alias>
    SYMBOL_COLUMNITEM = 72, // <Column Item>
    SYMBOL_COLUMNLIST = 73, // <Column List>
    SYMBOL_COLUMNSOURCE = 74, // <Column Source>
    SYMBOL_COLUMNS = 75, // <Columns>
    SYMBOL_EXPRLIST = 76, // <Expr List>
    SYMBOL_EXPRESSION = 77, // <Expression>
    SYMBOL_FROMCLAUSE = 78, // <From Clause>
    SYMBOL_GROUPCLAUSE = 79, // <Group Clause>
    SYMBOL_HAVINGCLAUSE = 80, // <Having Clause>
    SYMBOL_IDLIST = 81, // <Id List>
    SYMBOL_IDMEMBER = 82, // <Id Member>
    SYMBOL_INLIST = 83, // <In List>
    SYMBOL_JOIN2 = 84, // <Join>
    SYMBOL_JOINCHAIN = 85, // <Join Chain>
    SYMBOL_MULTEXP = 86, // <Mult Exp>
    SYMBOL_NEGATEEXP = 87, // <Negate Exp>
    SYMBOL_NOTEXP = 88, // <Not Exp>
    SYMBOL_ORDERCLAUSE = 89, // <Order Clause>
    SYMBOL_ORDERLIST = 90, // <Order List>
    SYMBOL_ORDERTYPE = 91, // <Order Type>
    SYMBOL_PREDEXP = 92, // <Pred Exp>
    SYMBOL_QUANTIFIER = 93, // <Quantifier>
    SYMBOL_QUERY = 94, // <Query>
    SYMBOL_RESTRICTION = 95, // <Restriction>
    SYMBOL_SELECTSTM = 96, // <Select Stm>
    SYMBOL_TUPLE = 97, // <Tuple>
    SYMBOL_VALUE = 98, // <Value>
    SYMBOL_VALUELIST = 99, // <Value List>
    SYMBOL_WHERECLAUSE = 100  // <Where Clause>
};

enum RuleConstants : int
{
    RULE_QUERY = 0, // <Query> ::= <Select Stm>
    RULE_QUERY_UNION = 1, // <Query> ::= <Query> UNION <Select Stm>
    RULE_QUERY_UNION_ALL = 2, // <Query> ::= <Query> UNION ALL <Select Stm>
    RULE_SELECTSTM_SELECT = 3, // <Select Stm> ::= SELECT <Columns> <From Clause> <Where Clause> <Group Clause> <Having Clause> <Order Clause>
    RULE_COLUMNS_TIMES = 4, // <Columns> ::= <Restriction> '*'
    RULE_COLUMNS = 5, // <Columns> ::= <Restriction> <Column List>
    RULE_COLUMNLIST_COMMA = 6, // <Column List> ::= <Column Item> ',' <Column List>
    RULE_COLUMNLIST = 7, // <Column List> ::= <Column Item>
    RULE_COLUMNITEM = 8, // <Column Item> ::= <Column Source>
    RULE_COLUMNSOURCE = 9, // <Column Source> ::= <Aggregate>
    RULE_COLUMNSOURCE_ID = 10, // <Column Source> ::= Id
    RULE_COLUMNSOURCE2 = 11, // <Column Source> ::= <Aggregate> <Column Alias>
    RULE_COLUMNSOURCE_ID2 = 12, // <Column Source> ::= Id <Column Alias>
    RULE_COLUMNSOURCE3 = 13, // <Column Source> ::= <Expression> <Column Alias>
    RULE_COLUMNALIAS_AS_ID = 14, // <Column Alias> ::= AS Id
    RULE_RESTRICTION_ALL = 15, // <Restriction> ::= ALL
    RULE_RESTRICTION_DISTINCT = 16, // <Restriction> ::= DISTINCT
    RULE_RESTRICTION = 17, // <Restriction> ::= 
    RULE_AGGREGATE_COUNT_LPAREN_TIMES_RPAREN = 18, // <Aggregate> ::= Count '(' '*' ')'
    RULE_AGGREGATE_COUNT_LPAREN_RPAREN = 19, // <Aggregate> ::= Count '(' <Expression> ')'
    RULE_AGGREGATE_COUNT_LPAREN_DISTINCT_RPAREN = 20, // <Aggregate> ::= Count '(' DISTINCT <Expression> ')'
    RULE_AGGREGATE_COUNT_LPAREN_ALL_RPAREN = 21, // <Aggregate> ::= Count '(' ALL <Expression> ')'
    RULE_AGGREGATE_AVG_LPAREN_RPAREN = 22, // <Aggregate> ::= Avg '(' <Expression> ')'
    RULE_AGGREGATE_MIN_LPAREN_RPAREN = 23, // <Aggregate> ::= Min '(' <Expression> ')'
    RULE_AGGREGATE_MAX_LPAREN_RPAREN = 24, // <Aggregate> ::= Max '(' <Expression> ')'
    RULE_AGGREGATE_STDEV_LPAREN_RPAREN = 25, // <Aggregate> ::= StDev '(' <Expression> ')'
    RULE_AGGREGATE_STDEVP_LPAREN_RPAREN = 26, // <Aggregate> ::= StDevP '(' <Expression> ')'
    RULE_AGGREGATE_SUM_LPAREN_RPAREN = 27, // <Aggregate> ::= Sum '(' <Expression> ')'
    RULE_AGGREGATE_VAR_LPAREN_RPAREN = 28, // <Aggregate> ::= Var '(' <Expression> ')'
    RULE_AGGREGATE_VARP_LPAREN_RPAREN = 29, // <Aggregate> ::= VarP '(' <Expression> ')'
    RULE_AGGREGATE_AVG_LPAREN_DISTINCT_RPAREN = 30, // <Aggregate> ::= Avg '(' DISTINCT <Expression> ')'
    RULE_AGGREGATE_MIN_LPAREN_DISTINCT_RPAREN = 31, // <Aggregate> ::= Min '(' DISTINCT <Expression> ')'
    RULE_AGGREGATE_MAX_LPAREN_DISTINCT_RPAREN = 32, // <Aggregate> ::= Max '(' DISTINCT <Expression> ')'
    RULE_AGGREGATE_SUM_LPAREN_DISTINCT_RPAREN = 33, // <Aggregate> ::= Sum '(' DISTINCT <Expression> ')'
    RULE_FROMCLAUSE_FROM = 34, // <From Clause> ::= FROM <Id List> <Join Chain>
    RULE_JOINCHAIN = 35, // <Join Chain> ::= <Join> <Join Chain>
    RULE_JOINCHAIN2 = 36, // <Join Chain> ::= 
    RULE_JOIN_INNER_JOIN_ON_ID_EQ_ID = 37, // <Join> ::= INNER JOIN <Id List> ON Id '=' Id
    RULE_JOIN_LEFT_JOIN_ON_ID_EQ_ID = 38, // <Join> ::= LEFT JOIN <Id List> ON Id '=' Id
    RULE_JOIN_RIGHT_JOIN_ON_ID_EQ_ID = 39, // <Join> ::= RIGHT JOIN <Id List> ON Id '=' Id
    RULE_JOIN_JOIN_ON_ID_EQ_ID = 40, // <Join> ::= JOIN <Id List> ON Id '=' Id
    RULE_WHERECLAUSE_WHERE = 41, // <Where Clause> ::= WHERE <Expression>
    RULE_WHERECLAUSE = 42, // <Where Clause> ::= 
    RULE_GROUPCLAUSE_GROUP_BY = 43, // <Group Clause> ::= GROUP BY <Id List>
    RULE_GROUPCLAUSE = 44, // <Group Clause> ::= 
    RULE_ORDERCLAUSE_ORDER_BY = 45, // <Order Clause> ::= ORDER BY <Order List>
    RULE_ORDERCLAUSE = 46, // <Order Clause> ::= 
    RULE_ORDERLIST_ID_COMMA = 47, // <Order List> ::= Id <Order Type> ',' <Order List>
    RULE_ORDERLIST_ID = 48, // <Order List> ::= Id <Order Type>
    RULE_ORDERLIST_FUNCTION = 49, // <Order List> ::= Function <Order Type>
    RULE_ORDERLIST_INTEGERLITERAL_COMMA = 50, // <Order List> ::= IntegerLiteral <Order Type> ',' <Order List>
    RULE_ORDERLIST_INTEGERLITERAL = 51, // <Order List> ::= IntegerLiteral <Order Type>
    RULE_ORDERTYPE_ASC = 52, // <Order Type> ::= ASC
    RULE_ORDERTYPE_DESC = 53, // <Order Type> ::= DESC
    RULE_ORDERTYPE = 54, // <Order Type> ::= 
    RULE_HAVINGCLAUSE_HAVING = 55, // <Having Clause> ::= HAVING <Expression>
    RULE_HAVINGCLAUSE = 56, // <Having Clause> ::= 
    RULE_EXPRESSION_OR = 57, // <Expression> ::= <And Exp> OR <Expression>
    RULE_EXPRESSION = 58, // <Expression> ::= <And Exp>
    RULE_ANDEXP_AND = 59, // <And Exp> ::= <Not Exp> AND <And Exp>
    RULE_ANDEXP = 60, // <And Exp> ::= <Not Exp>
    RULE_NOTEXP_NOT = 61, // <Not Exp> ::= NOT <Pred Exp>
    RULE_NOTEXP = 62, // <Not Exp> ::= <Pred Exp>
    RULE_PREDEXP_BETWEEN_AND = 63, // <Pred Exp> ::= <Add Exp> BETWEEN <Add Exp> AND <Add Exp>
    RULE_PREDEXP_NOT_BETWEEN_AND = 64, // <Pred Exp> ::= <Add Exp> NOT BETWEEN <Add Exp> AND <Add Exp>
    RULE_PREDEXP_IS_NOT_NULL = 65, // <Pred Exp> ::= <Value> IS NOT NULL
    RULE_PREDEXP_IS_NULL = 66, // <Pred Exp> ::= <Value> IS NULL
    RULE_PREDEXP_LIKE_STRINGLITERAL = 67, // <Pred Exp> ::= <Add Exp> LIKE StringLiteral
    RULE_PREDEXP_NOT_LIKE_STRINGLITERAL = 68, // <Pred Exp> ::= <Add Exp> NOT LIKE StringLiteral
    RULE_PREDEXP_IN = 69, // <Pred Exp> ::= <Add Exp> IN <In List>
    RULE_PREDEXP_NOT_IN = 70, // <Pred Exp> ::= <Add Exp> NOT IN <In List>
    RULE_PREDEXP_EQ = 71, // <Pred Exp> ::= <Add Exp> '=' <Add Exp>
    RULE_PREDEXP_LTGT = 72, // <Pred Exp> ::= <Add Exp> '<>' <Add Exp>
    RULE_PREDEXP_EXCLAMEQ = 73, // <Pred Exp> ::= <Add Exp> '!=' <Add Exp>
    RULE_PREDEXP_GT = 74, // <Pred Exp> ::= <Add Exp> '>' <Add Exp>
    RULE_PREDEXP_GTEQ = 75, // <Pred Exp> ::= <Add Exp> '>=' <Add Exp>
    RULE_PREDEXP_LT = 76, // <Pred Exp> ::= <Add Exp> '<' <Add Exp>
    RULE_PREDEXP_LTEQ = 77, // <Pred Exp> ::= <Add Exp> '<=' <Add Exp>
    RULE_PREDEXP = 78, // <Pred Exp> ::= <Add Exp>
    RULE_PREDEXP_LIKE_STRINGLITERAL_ESCAPE_STRINGLITERAL = 79, // <Pred Exp> ::= <Add Exp> LIKE StringLiteral ESCAPE StringLiteral
    RULE_PREDEXP_NOT_LIKE_STRINGLITERAL_ESCAPE_STRINGLITERAL = 80, // <Pred Exp> ::= <Add Exp> NOT LIKE StringLiteral ESCAPE StringLiteral
    RULE_PREDEXP_IN_LPAREN_RPAREN = 81, // <Pred Exp> ::= <Add Exp> IN '(' <Select Stm> ')'
    RULE_PREDEXP_NOT_IN_LPAREN_RPAREN = 82, // <Pred Exp> ::= <Add Exp> NOT IN '(' <Select Stm> ')'
    RULE_PREDEXP_EXISTS_LPAREN_RPAREN = 83, // <Pred Exp> ::= EXISTS '(' <Select Stm> ')'
    RULE_PREDEXP_EQ_LPAREN_RPAREN = 84, // <Pred Exp> ::= <Add Exp> '=' <Quantifier> '(' <Select Stm> ')'
    RULE_PREDEXP_LTGT_LPAREN_RPAREN = 85, // <Pred Exp> ::= <Add Exp> '<>' <Quantifier> '(' <Select Stm> ')'
    RULE_PREDEXP_EXCLAMEQ_LPAREN_RPAREN = 86, // <Pred Exp> ::= <Add Exp> '!=' <Quantifier> '(' <Select Stm> ')'
    RULE_PREDEXP_GT_LPAREN_RPAREN = 87, // <Pred Exp> ::= <Add Exp> '>' <Quantifier> '(' <Select Stm> ')'
    RULE_PREDEXP_GTEQ_LPAREN_RPAREN = 88, // <Pred Exp> ::= <Add Exp> '>=' <Quantifier> '(' <Select Stm> ')'
    RULE_PREDEXP_LT_LPAREN_RPAREN = 89, // <Pred Exp> ::= <Add Exp> '<' <Quantifier> '(' <Select Stm> ')'
    RULE_PREDEXP_LTEQ_LPAREN_RPAREN = 90, // <Pred Exp> ::= <Add Exp> '<=' <Quantifier> '(' <Select Stm> ')'
    RULE_QUANTIFIER_ANY = 91, // <Quantifier> ::= ANY
    RULE_QUANTIFIER_SOME = 92, // <Quantifier> ::= SOME
    RULE_QUANTIFIER_ALL = 93, // <Quantifier> ::= ALL
    RULE_ADDEXP_PLUS = 94, // <Add Exp> ::= <Add Exp> '+' <Mult Exp>
    RULE_ADDEXP_MINUS = 95, // <Add Exp> ::= <Add Exp> '-' <Mult Exp>
    RULE_ADDEXP = 96, // <Add Exp> ::= <Mult Exp>
    RULE_MULTEXP_TIMES = 97, // <Mult Exp> ::= <Mult Exp> '*' <Negate Exp>
    RULE_MULTEXP_DIV = 98, // <Mult Exp> ::= <Mult Exp> '/' <Negate Exp>
    RULE_MULTEXP = 99, // <Mult Exp> ::= <Negate Exp>
    RULE_NEGATEEXP_MINUS = 100, // <Negate Exp> ::= '-' <Value>
    RULE_NEGATEEXP = 101, // <Negate Exp> ::= <Value>
    RULE_VALUE = 102, // <Value> ::= <Tuple>
    RULE_VALUE_ID = 103, // <Value> ::= Id
    RULE_VALUE_INTEGERLITERAL = 104, // <Value> ::= IntegerLiteral
    RULE_VALUE_REALLITERAL = 105, // <Value> ::= RealLiteral
    RULE_VALUE_STRINGLITERAL = 106, // <Value> ::= StringLiteral
    RULE_VALUE_NULL = 107, // <Value> ::= NULL
    RULE_VALUE_BOOLEANLITERAL = 108, // <Value> ::= BooleanLiteral
    RULE_VALUE_DATELITERAL = 109, // <Value> ::= DateLiteral
    RULE_VALUE_FUNCTION = 110, // <Value> ::= Function
    RULE_VALUE2 = 111, // <Value> ::= <Aggregate>
    RULE_TUPLE_LPAREN_RPAREN = 112, // <Tuple> ::= '(' <Select Stm> ')'
    RULE_TUPLE_LPAREN_RPAREN2 = 113, // <Tuple> ::= '(' <Expr List> ')'
    RULE_INLIST_LPAREN_RPAREN = 114, // <In List> ::= '(' <Value List> ')'
    RULE_VALUELIST_COMMA = 115, // <Value List> ::= <Value> ',' <Value List>
    RULE_VALUELIST = 116, // <Value List> ::= <Value>
    RULE_EXPRLIST_COMMA = 117, // <Expr List> ::= <Expression> ',' <Expr List>
    RULE_EXPRLIST = 118, // <Expr List> ::= <Expression>
    RULE_IDLIST_COMMA = 119, // <Id List> ::= <Id Member> ',' <Id List>
    RULE_IDLIST = 120, // <Id List> ::= <Id Member>
    RULE_IDMEMBER_ID = 121, // <Id Member> ::= Id
    RULE_IDMEMBER_ID_ID = 122, // <Id Member> ::= Id Id
    RULE_IDMEMBER_ID_AS_ID = 123  // <Id Member> ::= Id AS Id
};
