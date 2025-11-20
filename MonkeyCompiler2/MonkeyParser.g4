
parser grammar MonkeyParser;
options { tokenVocab=MonkeyLexer; }
//  program
program
    : (functionDeclaration | statement)* mainFunction EOF
    ;

//  mainFunction
mainFunction
    : FN MAIN LPAREN RPAREN COLON TYPE_VOID blockStatement
    ;

//  functionDeclaration
functionDeclaration
    : FN IDENTIFIER LPAREN functionParameters? RPAREN COLON type blockStatement
    ;

//  functionParameters
functionParameters
    : parameter (COMMA parameter)*
    ;

//  parameter
parameter
    : IDENTIFIER COLON type
    ;

//  type definition
type
    : TYPE_INT
    | TYPE_STRING
    | TYPE_BOOL
    | TYPE_CHAR
    | TYPE_VOID
    | arrayType
    | hashType
    | functionType
    ;

arrayType: ARRAY LT type GT; // 
hashType: HASH LT type COMMA type GT; // 
functionType: FN LPAREN functionParameterTypes? RPAREN COLON type; // 
functionParameterTypes: type (COMMA type)*; // 

//  statement
statement
    : letStatement
    | returnStatement
    | expressionStatement
    | ifStatement
    | blockStatement
    | printStatement
    ;

//  letStatement
letStatement
    : LET CONST? IDENTIFIER COLON type ASSIGN expression SEMICOLON?
    ;

//  returnStatement
returnStatement
    : RETURN expression? SEMICOLON?
    ;

//  expressionStatement
expressionStatement
    : expression SEMICOLON?
    ;

//  ifStatement
ifStatement
    : IF expression blockStatement (ELSE blockStatement)?
    ;

//  blockStatement
blockStatement
    : LBRACE statement* RBRACE
    ;

//  printStatement
printStatement
    : PRINT LPAREN expression RPAREN SEMICOLON?
    ;

//  expression hierarchy (Precedence)
expression
    : additionExpression comparison?
    ;

//  comparison
comparison
    : ((LT | GT | LTEQ | GTEQ | EQ | NOT_EQ) additionExpression)+
    ;

//  additionExpression
additionExpression
    : multiplicationExpression ((PLUS | MINUS) multiplicationExpression)*
    ;

//  multiplicationExpression
multiplicationExpression
    : elementExpression ((ASTERISK | SLASH) elementExpression)*
    ;

//  elementExpression
elementExpression
    : primitiveExpression (elementAccess | callExpression)?
    ;

//  elementAccess
elementAccess
    : LBRACKET expression RBRACKET
    ;

//  callExpression
callExpression
    : LPAREN expressionList? RPAREN
    ;

//  primitiveExpression
primitiveExpression
    : INTEGER
    | STRING
    | CHAR
    | IDENTIFIER
    | TRUE
    | FALSE
    | LPAREN expression RPAREN
    | arrayLiteral
    | functionLiteral
    | hashLiteral
    ;

//  arrayLiteral
arrayLiteral
    : LBRACKET expressionList? RBRACKET
    ;

//  functionLiteral
functionLiteral
    : FN LPAREN functionParameters? RPAREN COLON type blockStatement
    ;

//  hashLiteral
hashLiteral
    : LBRACE hashContent (COMMA hashContent)* RBRACE
    ;

//  hashContent
hashContent
    : expression COLON expression
    ;

//  expressionList
expressionList
    : expression (COMMA expression)*
    ;