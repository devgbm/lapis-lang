// `s.length` conta **pontos de código**, não unidades de armazenamento (Q35/A2a).
//
// A distinção não é acadêmica: `𝕏` ocupa duas unidades UTF-16 na representação
// interna, e contá-las daria 4 para `a𝕏b`. O que a linguagem promete é que
// `length` e a indexação falem da mesma unidade — o que `astral.ls` verifica do
// outro lado.
//
// Ao contrário de `[T;N]`, o tamanho não está no tipo: `Str` não carrega N
// (isso seria A2b), então a resposta é sempre de execução.
// expect: output
// 0
// 3
// 3
// ---
def vazia = "";
def ascii = "abc";
def astral = "a𝕏b";

print(vazia.length);
print(ascii.length);
print(astral.length);
