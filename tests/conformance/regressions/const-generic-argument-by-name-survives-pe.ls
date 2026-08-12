// Regressão do M12, mas o bug é bem mais antigo.
//
// Um argumento genérico nu é uma **string** no Core (`CoreNameArgument`), não um
// `CoreVariable`. A propagação do partial evaluator não passava por ele — e ainda
// assim o `def n = 3;` era apagado por "ninguém lê o nome". O residual saía
// citando `n`, e não compilava.
//
// A forma nunca tinha aparecido no corpus; foi o caso de `length` como argumento
// const genérico (plano 24 §24.6) que a trouxe. Agora o nome é trocado pelo valor
// junto com o resto da propagação, o que de quebra especializa a instanciação.
//
// A propriedade que pega isto é `Residual_IsReparseableAndWellTyped` — não a
// equivalência de saída, que este arquivo satisfaz de qualquer jeito.
// expect: output
// 15
// 36
// ---
def escala = fn<N: Int>(x: Int) Int {
    return x * N;
};

def n = 3;

print(escala<n>(5));

// A mesma posição na construção de um `type` genérico.
def Caixa = type<T, N: Int> {
    valor: T;
};

def quatro = 4;
def c = .Caixa<Int, quatro> { valor: 36 };

print(c.valor);
