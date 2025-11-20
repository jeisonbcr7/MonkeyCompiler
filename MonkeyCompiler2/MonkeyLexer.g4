lexer grammar MonkeyLexer;

// --- LEXER (Tokens) ---

// Palabras Reservadas 
FN: 'fn';
LET: 'let';
IF: 'if';
ELSE: 'else';
RETURN: 'return';
TRUE: 'true';
FALSE: 'false';
PRINT: 'print';
CONST: 'const';
MAIN: 'main';

// Tipos de Datos 
TYPE_INT: 'int';
TYPE_STRING: 'string';
TYPE_BOOL: 'bool';
TYPE_CHAR: 'char';
TYPE_VOID: 'void';
ARRAY: 'array';
HASH: 'hash';

// Operadores y Puntuación 
ASSIGN: '=';
PLUS: '+';
MINUS: '-';
ASTERISK: '*';
SLASH: '/';
BANG: '!';
LT: '<';
GT: '>';
LTEQ: '<=';
GTEQ: '>=';
EQ: '==';
NOT_EQ: '!=';

LPAREN: '(';
RPAREN: ')';
LBRACE: '{';
RBRACE: '}';
LBRACKET: '[';
RBRACKET: ']';
COMMA: ',';
COLON: ':';
SEMICOLON: ';'; 

// Literales y Identificadores
INTEGER: [0-9]+; // 
STRING: '"' .*? '"'; //  "Cadena sin restricción"
CHAR: '\'' . '\''; //  Caracter simple
IDENTIFIER: [a-zA-Z_] [a-zA-Z0-9_]*; //  Permite _ y letras

// Comentarios y Espacios (Ignorados)
COMMENT: '//' ~[\r\n]* -> skip;
//BLOCK_COMMENT: '/*' .*? '*/' -> skip; 
WS: [ \t\r\n]+ -> skip;
// 7. Comentarios Anidados (Bloques Multilínea)
// Cuando encontramos '/*', entramos al modo ISOLATED_COMMENT y empujamos el modo a la pila.
OPEN_COMMENT : '/*' -> pushMode(ISOLATED_COMMENT), skip;
// Definición del modo exclusivo para comentarios
mode ISOLATED_COMMENT;

    // Si encontramos OTRO '/*', empujamos OTRO modo encima (anidamiento)
    NESTED_OPEN : '/*' -> pushMode(ISOLATED_COMMENT), skip;

    // Si encontramos '*/', sacamos el modo actual de la pila (pop)
    NESTED_CLOSE : '*/' -> popMode, skip;

    // Cualquier otro carácter dentro del comentario se ignora (skip).
    // El punto '.' en el lexer coincide con cualquier carácter.
    COMMENT_CONTENT : . -> skip;
// --- PARSER (Reglas Gramaticales) ---
