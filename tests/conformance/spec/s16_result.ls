// spec §16 — `Result` vem do prelude e é genérico, então a construção leva os
// argumentos antes do ponto (Q7).
// expect: output
// Result.Ok(10)
// Result.Err(IndexError.OutOfBounds)
// ---
print(Result<Int, IndexError>.Ok(10));
print(Result<Int, IndexError>.Err(IndexError.OutOfBounds));
