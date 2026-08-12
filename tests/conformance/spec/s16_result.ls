// spec §16 — `Option` vem do prelude e é genérico, então a construção leva os
// argumentos antes do ponto (Q7).
// expect: output
// Option.Some(10)
// Option.None
// ---
print(Option<Int>.Some(10));
print(Option<Int>.None);
