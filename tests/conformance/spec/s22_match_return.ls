// spec §22 — `unwrapOr` sobre `Result`, com `return` dentro dos braços.
// expect: output
// 20
// 0
// ---
def numbers = [10, 20, 30];

def unwrapOr = fn(result: Result<Int, IndexError>, fallback: Int) Int {
    match result {
        Result.Ok(value) => return value,
        Result.Err(error) => return fallback
    }
};

print(unwrapOr(numbers[1], 0));
print(unwrapOr(numbers[9], 0));
