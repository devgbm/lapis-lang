// Spec §21/§30: indexar nunca lança. Fora de limites é um valor, não um aborto.
//
// O span é `var`, então o tamanho não está no tipo (`[Int;?]`) e toda leitura
// devolve `Option` — inclusive a que estaria dentro dos limites.
// expect: output
// Option.Some(10)
// Option.None
// Option.None
// ---
var a = .[10];

print(a[0]);
print(a[1]);
print(a[0 - 1]);
