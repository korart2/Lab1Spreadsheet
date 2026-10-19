// Variant 67: unary +/-, inc/dec, <=, >=, <>, not, or/and
// Precedence (high -> low): () ; inc/dec ; unary +/- ; <= >= <> ; not ; and ; or
grammar Expression;

options {
    caseInsensitive = true;
}

/*
 * Parser rules
 */
compileUnit : expression EOF ;

expression
    : LPAREN expression RPAREN                              #ParenthesizedExpr
    | operatorToken=(INC | DEC) expression                  #IncDecExpr
    | operatorToken=(PLUS | MINUS) expression               #UnaryExpr
    | expression operatorToken=(LE | GE | NE) expression    #ComparisonExpr
    | NOT expression                                        #NotExpr
    | expression AND expression                             #AndExpr
    | expression OR expression                              #OrExpr
    | (TRUE | FALSE)                                        #BooleanExpr
    | NUMBER                                                #NumberExpr
    | IDENTIFIER                                            #CellRefExpr
    ;

/*
 * Lexer rules
 */
INC   : 'inc' ;
DEC   : 'dec' ;
NOT   : 'not' ;
AND   : 'and' ;
OR    : 'or' ;
TRUE  : 'true' ;
FALSE : 'false' ;

LE : '<=' ;
GE : '>=' ;
NE : '<>' ;

PLUS   : '+' ;
MINUS  : '-' ;
LPAREN : '(' ;
RPAREN : ')' ;

NUMBER     : [0-9]+ ;
IDENTIFIER : [A-Z]+ [1-9] [0-9]* ;

WS : [ \t\r\n]+ -> channel(HIDDEN) ;