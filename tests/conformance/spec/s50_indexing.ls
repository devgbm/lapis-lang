// spec §50 — os três casos de indexação, incluindo o fora de limites.
// expect: output
// Result.Ok(1)
// Result.Ok(3)
// Result.Err(IndexError.OutOfBounds)
// ---
print([1, 2, 3][0]);
print([1, 2, 3][2]);
print([1, 2, 3][3]);
