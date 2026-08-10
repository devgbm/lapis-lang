// spec §5 — programa completo com array e indexação.
// expect: output
// Result.Ok(2)
// ---
def add = fn(a: Int, b: Int) Int {
    return a + b;
};

def numbers = [1, 2, 3];

def result = numbers[1];

print(result);
