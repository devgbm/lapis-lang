// Sem dono escrito, o desugar resolve `Some`/`None` contra o prelúdio (plano
// 25 §25.5) — a forma que os exemplos do autor usam.
// expect: output
// 7
// ---
def e = Option<Int>.Some(7);

if e is Some(v) {
    print(v);
} else {
    print(0);
}
