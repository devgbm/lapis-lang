// `Nada` não é variante de enum nenhum conhecido (plano 25 §25.5).
// expect: error LAP0732
def e = Option<Int>.Some(1);
def b = e is Nada;
