// `e is Some(v)` liga `v` no ramo verdadeiro do `if` — o requisito da Q23,
// entregue sem exceção de runtime e sem análise de fluxo (plano 25 §25.1).
// expect: output
// 42
// ---
def e = Option<Int>.Some(42);

if e is Some(v) {
    print(v);
} else {
    print(0);
}
