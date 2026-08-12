// spec §22 — `unwrapOr` sobre `Option`, com `return` dentro dos braços.
//
// O span é `var` para que o tamanho fique fora do tipo e a indexação devolva
// `Option`; com `def` os dois acessos seriam decididos em compilação.
// expect: output
// 20
// 0
// ---
var numbers = .[10, 20, 30];

def unwrapOr = fn(result: Option<Int>, fallback: Int) Int {
    match result {
        Option.Some(value) => return value,
        Option.None => return fallback
    }
};

print(unwrapOr(numbers[1], 0));
print(unwrapOr(numbers[9], 0));
