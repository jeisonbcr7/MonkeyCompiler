grammar Monkey;

// =======================
// PARSER RULES
// =======================

program
    : (functionDeclaration | statement)* mainFunction EOF
    ;

mainFunction
    : FN MAIN LPAREN RPAREN COLON VOID blockStatement
    ;

// fn identifier ( functionParameters? ) : type blockStatement
functionDeclaration
    : FN identifier LPAREN functionParameters? RPAREN COLON type blockStatement
    ;

// parameter (, parameter)*
functionParameters
    : parameter (COMMA parameter)*
    ;

// identifier : type
parameter
    : identifier COLON type
    ;

// ---------- Tipos ----------

type
    : INT
    | STRING
    | BOOL
    | CHAR
    | VOID
    | arrayType
    | hashType
    | functionType
    ;

// array<type>
arrayType
    : ARRAY LT type GT
    ;

// hash<type, type>
hashType
    : HASH LT type COMMA type GT
    ;

// fn ( functionParameterTypes? ) : type
functionType
    : FN LPAREN functionParameterTypes? RPAREN COLON type
    ;

// type (, type)*
functionParameterTypes
    : type (COMMA type)*
    ;

// =======================
// Sentencias
// =======================

statement
    : letStatement
    | returnStatement
    | expressionStatement
    | ifStatement
    | blockStatement
    | printStatement
    ;

// let const? identifier : type = expression
letStatement
    : LET CONST? identifier COLON type ASSIGN expression
    ;

// return expression?
returnStatement
    : RETURN expression?
    ;

// expression
expressionStatement
    : expression
    ;

// if expression blockStatement (else blockStatement)?
ifStatement
    : IF expression blockStatement (ELSE blockStatement)?
    ;

// { statement* }
blockStatement
    : LBRACE statement* RBRACE
    ;

// print ( expression )
printStatement
    : PRINT LPAREN expression RPAREN
    ;

// =======================
// Expresiones
// =======================

// expression : additionExpression comparison
// comparison : ((< | > | <= | >= | == | !=) additionExpression)*
expression
    : additionExpression ( (LT | GT | LE | GE | EQEQ | NEQ) additionExpression )*
    ;

// additionExpression : multiplicationExpression ((+ | -) multiplicationExpression)*
additionExpression
    : multiplicationExpression ( (PLUS | MINUS) multiplicationExpression )*
    ;

// multiplicationExpression : elementExpression ((* | /) elementExpression)*
multiplicationExpression
    : elementExpression ( (STAR | SLASH) elementExpression )*
    ;

// elementExpression: primitiveExpression (elementAccess | callExpression)?
elementExpression
    : primitiveExpression ( elementAccess | callExpression )?
    ;

// elementAccess : [ expression ]
elementAccess
    : LBRACK expression RBRACK
    ;

// callExpression : ( expressionList? )
callExpression
    : LPAREN expressionList? RPAREN
    ;

// primitiveExpression
//   : numericLiteral
//   | stringLiteral
//   | charLiteral
//   | booleanLiteral
//   | identifier
//   | ( expression )
//   | arrayLiteral
//   | functionLiteral
//   | hashLiteral
primitiveExpression
    : numericLiteral
    | stringLiteral
    | charLiteral
    | booleanLiteral
    | identifier
    | LPAREN expression RPAREN
    | arrayLiteral
    | functionLiteral
    | hashLiteral
    ;

// arrayLiteral : [ expressionList? ]
arrayLiteral
    : LBRACK expressionList? RBRACK
    ;

// functionLiteral : fn ( functionParameters? ) : type blockStatement
functionLiteral
    : FN LPAREN functionParameters? RPAREN COLON type blockStatement
    ;

// hashLiteral : { hashContent (, hashContent)* }
hashLiteral
    : LBRACE hashContent (COMMA hashContent)* RBRACE
    ;

// hashContent : expression : expression
hashContent
    : expression COLON expression
    ;

// expressionList : expression (, expression)*
expressionList
    : expression (COMMA expression)*
    ;

// =======================
// Reglas auxiliares
// =======================

numericLiteral
    : INTEGER_LITERAL
    ;

stringLiteral
    : STRING_LITERAL
    ;

charLiteral
    : CHAR_LITERAL
    ;

booleanLiteral
    : TRUE
    | FALSE
    ;

identifier
    : IDENTIFIER
    ;

// =======================
// LEXER RULES
// =======================

// Palabras reservadas
FN      : 'fn';
LET     : 'let';
CONST   : 'const';
RETURN  : 'return';
IF      : 'if';
ELSE    : 'else';
PRINT   : 'print';
TRUE    : 'true';
FALSE   : 'false';

INT     : 'int';
STRING  : 'string';
BOOL    : 'bool';
CHAR    : 'char';
VOID    : 'void';

ARRAY   : 'array';
HASH    : 'hash';

MAIN    : 'main';

// Operadores y símbolos

// Operadores de comparación (más largos primero)
LE      : '<=';
GE      : '>=';
EQEQ    : '==';
NEQ     : '!=';

// Operadores simples
LT      : '<';
GT      : '>';
ASSIGN  : '=';
PLUS    : '+';
MINUS   : '-';
STAR    : '*';
SLASH   : '/';

// Delimitadores
LPAREN  : '(';
RPAREN  : ')';
LBRACE  : '{';
RBRACE  : '}';
LBRACK  : '[';
RBRACK  : ']';
COMMA   : ',';
COLON   : ':';
SEMICOLON : ';';

// Literales

INTEGER_LITERAL
    : '0'
    | [1-9] [0-9]*
    ;

// 'a'  '\\n'  '\\''
CHAR_LITERAL
    : '\'' ( ESC_SEQ | ~['\\] ) '\''
    ;

// "hola", con escapes básicos
STRING_LITERAL
    : '"' ( ESC_SEQ | ~["\\] )* '"'
    ;

fragment ESC_SEQ
    : '\\' [btnr"'\\]
    ;

// Identificadores (case-sensitive, permite _)
IDENTIFIER
    : [a-zA-Z_] [a-zA-Z0-9_]*
    ;

// =======================
// Espacios en blanco y comentarios
// =======================

WS
    : [ \t\r\n]+ -> skip
    ;

// Comentario de línea: //
LINE_COMMENT
    : '//' ~[\r\n]* -> skip
    ;

// Comentarios de bloque anidados: /* ... */
BLOCK_COMMENT_START
    : '/*' -> pushMode(COMMENT_MODE), skip
    ;

mode COMMENT_MODE;

BLOCK_COMMENT_OPEN
    : '/*' -> pushMode(COMMENT_MODE), skip
    ;

BLOCK_COMMENT_END
    : '*/' -> popMode, skip
    ;

BLOCK_COMMENT_CONTENT
    : . -> skip
    ;
