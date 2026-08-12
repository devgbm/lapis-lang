// A ligação de `is` só vale em condição de `if` ou à esquerda de `&&` (plano
// 25 §25.3). Fora dali ela é descartada: `v` nunca chega a existir na Core.
// expect: error LAP0730
def e = Option<Int>.Some(1);
def b = e is Some(v);
