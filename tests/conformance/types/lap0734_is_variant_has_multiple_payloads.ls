// `is` liga um valor só (plano 25 §25.5) — uma variante com duas ou mais
// cargas pede `match`, que é onde o padrão completo vive.
// expect: error LAP0734
def T = enum { Pair(Int, Int) };
def e = T.Pair(1, 2);
if e is Pair(v) { print(v); };
