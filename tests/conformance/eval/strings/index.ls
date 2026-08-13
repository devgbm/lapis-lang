// `s[i]` devolve `Option<Char>` (Q35/A2a).
//
// É o mesmo regime de um `[T;?]`: o tamanho não está no tipo, então o
// compilador não tem como provar os limites e a falha aparece no **tipo**, e
// não em um abort — a regra da Q31 aplicada a Str.
//
// Nunca há indexação total aqui: mesmo com índice literal, não existe N no tipo
// contra o qual provar.
// expect: output
// Option.Some(a)
// Option.Some(c)
// Option.None
// Option.None
// ---
def s = "abc";

print(s[0]);
print(s[2]);
print(s[3]);
print(s[0 - 1]);
