// Spec §21/§30: indexar nunca lança. Fora de limites é um valor, não um aborto.
// expect: output
// Result.Ok(10)
// Result.Err(IndexError.OutOfBounds)
// Result.Err(IndexError.OutOfBounds)
// ---
def a = [10];

print(a[0]);
print(a[1]);
print(a[-1]);
