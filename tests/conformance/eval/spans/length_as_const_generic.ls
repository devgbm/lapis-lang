// `length` sobre `[T;N]` é uma constante de compilação, e constante de compilação
// serve de argumento const genérico (Q18). É o que fecha o buraco que o plano 09
// abriu no M2 ao prever `array_length` e não entregá-lo.
//
// O `def` intermediário não é cerimônia: `escala<a.length>(5)` não parseia, e
// isso é da Q5, não do span — o `<` só volta a ser genérico quando o argumento
// termina em `,` ou `>`, e `a.length` continua depois do identificador. Vale
// igual para qualquer expressão nessa posição.
// expect: output
// 15
// ---
def escala = fn<N: Int>(x: Int) Int {
    return x * N;
};

def a = .[1, 2, 3];
def n = a.length;

print(escala<n>(5));
