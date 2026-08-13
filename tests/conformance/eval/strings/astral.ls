// Indexar por ponto de código significa que `s[i]` nunca devolve metade de uma
// letra (Q35/A2a).
//
// Se `Char` fosse unidade UTF-16 — o `char` de C# —, `astral[1]` daria a
// primeira metade do par substituto de `𝕏`, um valor que não é caractere
// nenhum. É por isso que `Char` é ponto de código: a escolha errada não daria
// erro, daria resposta errada em silêncio.
// expect: output
// 3
// a
// 𝕏
// b
// ---
def astral = "a𝕏b";

print(astral.length);

if astral[0] is Some(c) { print(c); } else { print("?"); }
if astral[1] is Some(c) { print(c); } else { print("?"); }
if astral[2] is Some(c) { print(c); } else { print("?"); }
