// `@while` posiciona `condition` no ramo verdadeiro de um `if` (prelude.ls,
// plano 26) — a posição 1 da regra de escopo do `is` (plano 25 §25.3). Por
// isso `@while e is Some(v) { ... }` liga `v` dentro do corpo de graça, sem
// nada especial em `is` nem em `@while`.
// expect: output
// 1
// fim
// ---
var atual = Option<Int>.Some(1);

@while atual is Some(v) {
    print(v);
    atual = Option<Int>.None;
}

print("fim");
