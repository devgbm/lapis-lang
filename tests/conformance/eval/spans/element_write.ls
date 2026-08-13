// `xs[i] = v` sobre um `var` de span (Q36).
//
// Não é mutação no lugar: span é **valor**, e a escrita reconstrói o span e
// religa o slot — o mesmo mecanismo de `u.a.b = 1` (plano 21 §21.3b). É o que
// mantém a premissa da Q25 intacta: nenhuma closure captura `var`, então não há
// aliasing a modelar, nem para o evaluator nem para o partial evaluator.
//
// Semântica de valor é observável, e este caso a fixa: `def copia = s;` guarda o
// span de antes, e escrever em `s` não o alcança.
// expect: output
// [9, 1, 2]
// [0, 1, 2]
// ---
var s = .[0, 1, 2];
def copia = s;

s[0] = 9;

print(s);
print(copia);
