// Q18: um argumento const genérico precisa ser resolvível em tempo de
// compilação. Um `var` nunca é — o valor de hoje não é o de amanhã.
// expect: error LAP0294 at 7:15
var n = 3;
def escalar = fn<N: Int>(x: Int) Int { return x * N; };

print(escalar<n>(2));
