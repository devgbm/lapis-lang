// `None` não carrega valor — ligar `v` a ele não faz sentido (plano 25 §25.5).
// expect: error LAP0733
def e: Option<Int> = Option<Int>.None;
if e is None(v) { print(v); };
