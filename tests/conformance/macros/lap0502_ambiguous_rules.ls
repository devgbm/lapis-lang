// Todas as regras são testadas e exatamente uma deve casar. Falhar ruidosamente
// é o que a 0.2 já faz com `a < b < c`.
// expect: error LAP0502 at 9:1
macro foo
    match Expression:e expand { print(e); }
    match Identifier:x expand { print(x); };

def x = 1;
@foo x;
