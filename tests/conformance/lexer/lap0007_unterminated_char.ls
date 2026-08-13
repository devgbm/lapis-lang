// LAP0007 — aspa simples sem par.
//
// A regra é a mesma do literal de string: a lexer não deixa um literal
// atravessar quebra de linha, porque o erro provável é a aspa esquecida, e
// engolir o resto do arquivo transformaria um erro em cem.
// expect: error LAP0007
def c = 'a;
