// Result e pattern matching (spec §16, §22).
// Saída esperada:
//   20
//   0

def unwrapOr = fn(result: Result<Int, IndexError>, fallback: Int) Int {
    match result {
        Ok(value) => return value,
        Err(error) => return fallback
    }
};

def values = [10, 20, 30];

print(unwrapOr(values[1], 0));
print(unwrapOr(values[9], 0));
