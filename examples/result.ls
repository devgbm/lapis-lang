// Result e pattern matching (spec §16, §22).
//
// Variantes de enum exigem qualificação completa: `Result.Ok`, não `Ok`
// (decisão Q3 em plans/appendix-c-decisions.md).
//
// Saída esperada:
//   20
//   0

def unwrapOr = fn(result: Result<Int, IndexError>, fallback: Int) Int {
    match result {
        Result.Ok(value) => return value,
        Result.Err(error) => return fallback
    }
};

def values = [10, 20, 30];

print(unwrapOr(values[1], 0));
print(unwrapOr(values[9], 0));
