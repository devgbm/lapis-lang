// Dois padrões que se cruzam: existe um `Par<Int, Str>` que casa com os dois, e
// nada diz qual roda.
//
// A alternativa seria escolher a mais específica — uma regra que o leitor teria
// de simular de cabeça para saber qual membro executa. Erro é uma regra que ele
// não precisa saber. A 0.2 já faz a mesma escolha em `a < b < c`.
//
// Com `?` escrito, a sobreposição está **na fonte**: não é consequência sutil de
// duas declarações que parecem diferentes, são dois padrões que se cruzam à vista.
// expect: error LAP0720 at 14:5
def Par = type<A, B> { a: A; b: B; };

def Par<?, ?>.descrever = fn(self) Str { return "qualquer"; };
def Par<Int, ?>.descrever = fn(self) Str { return "int"; };
